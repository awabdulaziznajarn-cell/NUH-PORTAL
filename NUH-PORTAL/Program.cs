using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using NUH_PORTAL.Data;
using Microsoft.AspNetCore.Identity;
using NUH_PORTAL.Core;
using NUH_PORTAL.Models;
using NUH_PORTAL.Services;
using NUH_PORTAL.Services.Interfaces;
using NUH_PORTAL.Repositories;
using NUH_PORTAL.Repositories.Interfaces;
using NUH_PORTAL.Data.Interfaces;
using NUH_PORTAL.Core.Middleware;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseWindowsService();
// test for push
// ✅ Validate DB connection string
var connStr = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connStr))
{
    throw new InvalidOperationException("Database connection string 'DefaultConnection' is missing.");
}
if (connStr.Contains("#{DB_PASSWORD}#"))
{
    Console.WriteLine("WARNING: Database connection string still contains the default password placeholder. Replace #{DB_PASSWORD}# with the actual password in production.");
}

// ✅ Localization (.resx) - العربية هي اللغة المحايدة/الافتراضية + الإنجليزية
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ✅ Controllers with JSON options + توطين الـ Views و DataAnnotations
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        // gender يفضل "male"/"female" في الـ JSON رغم إنه بقى enum
        options.JsonSerializerOptions.Converters.Add(new NUH_PORTAL.Common.Json.GenderJsonConverter());
        // باقي الـ enums (أسماؤها snake_case مطابقة للنص) تتسلسل بالاسم - فالـ JSON يفضل زي ما هو
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ✅ الثقافات المدعومة - العربية افتراضيًا، واختيار المستخدم (كوكي) يتغلّب على لغة المتصفح
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("ar"), new CultureInfo("en") };
    options.DefaultRequestCulture = new RequestCulture("ar");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // الترتيب الافتراضي للمزوّدات: QueryString ثم Cookie ثم Accept-Language - الكوكي بيتغلّب على المتصفح، وده المطلوب.
});

// ✅ Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "اكتب: Bearer {token}"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ✅ SQL Connection
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Validate JWT config
var jwtKey = builder.Configuration["Jwt:Key"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];
if (string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
{
    throw new InvalidOperationException("JWT configuration is missing. Check 'Jwt:Issuer' and 'Jwt:Audience' in appsettings.json.");
}
if (string.IsNullOrEmpty(jwtKey) || jwtKey.Length < 32)
{
    throw new InvalidOperationException("JWT key is missing or too short. 'Jwt:Key' must be at least 32 characters for HMAC-SHA256.");
}
if (jwtKey == "#{JWT_SECRET}#")
{
    Console.WriteLine("WARNING: JWT secret is still set to the default placeholder. Replace #{JWT_SECRET}# with a real secret in production.");
}


// ✅ Authentication - سكيم ذكي بيختار تلقائيًا: كوكي MVC (NUH.Auth) لو موجود، وإلا JWT (للـ API/الأدوات).
// بكده كل الـ [Authorize] - العادية واللي عليها Roles - بتتوثّق بالكوكي من صفحات الـ MVC حتى لو توكن الـ JWT
// (staffToken) قديم/منتهي. (الإصلاح القديم بالـ DefaultPolicy كان بيمسك [Authorize] العادية بس، مش اللي عليها Roles.)
// ✅ ASP.NET Identity (Core) - مخزن المستخدمين/الأدوار/الصلاحيات (زي الـ permit).
// المصادقة نفسها فاضلة على JWT/Cookie تحت؛ Identity بيوفّر UserManager/RoleManager + الهاشر.
builder.Services.AddIdentityCore<User>(opt =>
{
    // ⚠️ كانت ٦ أحرف بلا أي شرط - يعني "123456" مقبولة. الحسابات المحلية دي
    //    مسار احتياطي بيشتغل لما الأكتف دايركتوري ما يردّش، وساعتها هي الحارس
    //    الوحيد على النظام كله. الشروط دي حد أدنى معقول مش تشديد.
    opt.Password.RequireDigit = true;
    opt.Password.RequireNonAlphanumeric = false;   // رمز إجباري بيدفع الناس تكتبها على ورقة
    opt.Password.RequireUppercase = false;         // لا معنى له مع كلمات المرور العربية/المختلطة
    opt.Password.RequiredLength = 10;
    opt.User.RequireUniqueEmail = false;

    // ====================================================================
    //  ⚠️ قفل الحساب - ماكانش موجود خالص.
    //
    //     النتيجة اللي كانت قائمة: محاولات تخمين بلا أي حد ولا قفل على
    //     نموذج دخول الموظفين. ومسار المصادقة كان بيستخدم
    //     UserManager.CheckPasswordAsync وهي **لا** بتزوّد عدّاد الفشل
    //     ولا بتفحص القفل - فحتى لو كان مفعّلًا ماكانش هيشتغل.
    //
    //     تلات محاولات وربع ساعة: بتوقّف التخمين الآلي عمليًا، وبتسيب مجالًا
    //     للموظف اللي بيغلط في كلمة مروره من غير ما يتقفل عليه يوم كامل.
    //     ⚠️ مقايضة مقصودة: القفل بالاسم معناه إن حد يقدر يقفل حساب موظف
    //        عمدًا. ولذلك القفل قصير ومعاه حد معدل على مستوى الـ IP تحت.
    // ====================================================================
    opt.Lockout.AllowedForNewUsers = true;
    opt.Lockout.MaxFailedAccessAttempts = 3;
    opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
    .AddRoles<Role>()
    .AddEntityFrameworkStores<AppDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "NUH_Smart";
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme; // تحدّي 401 نظيف (مش redirect) لو الاتنين مش صالحين
})
    .AddPolicyScheme("NUH_Smart", "Cookie or Bearer", options =>
    {
        // ⚠️ الترتيب هنا مهم جدًا: هيدر Authorization: Bearer له الأولوية على الكوكي.
        //
        //    قبل كده كان الكوكي بيكسب دايمًا. النتيجة: موظف داخل بحسابه في نفس
        //    المتصفح (كوكي NUH.Auth موجود) يفتح صفحة تتبع الطالب - الصفحة بتبعت
        //    توكن الطالب في الهيدر، لكن السيرفر كان بيتجاهله ويتعامل معاه كأدمن.
        //    فبيرجّعله كل طلبات النظام بدل طلبات صاحب الجوال اللي اتحقق منه.
        //
        //    الطلب اللي فيه Bearer صريح لازم يتقيّم بالتوكن ده هو، مهما كان في
        //    المتصفح كوكيز تانية.
        options.ForwardDefaultSelector = context =>
        {
            var authHeader = context.Request.Headers.Authorization.ToString();
            if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;

            return context.Request.Cookies.ContainsKey("NUH.Auth")
                ? Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme
                : JwtBearerDefaults.AuthenticationScheme;
        };
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };

    })
    // ✅ Cookie auth لصفحات الـ MVC - جنب الـ JWT (الـ API فاضل زي ما هو على الـ JWT)
    .AddCookie(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        // ⚠️ ماتخليهاش صفحة الدخول تاني. لما كانت /Account/Login كان أي فشل تصريح
        //    بيعمل لوب: صفحة محمية -> 403 -> صفحة الدخول -> بتلاقي الكوكي صالح
        //    فبتحوّل على /Home -> 403 -> ... والمستخدم بيشوفه كأنه بيتسجّل خروج فورًا.
        options.AccessDeniedPath = "/Account/Denied";
        // ٣٠ دقيقة خمول متجددة: كل نداء بيجدّد المدة، فالموظف اللي بيشتغل
        // مايتسجّلش خروج، واللي سايب الشاشة مفتوحة بتتقفل لوحدها.
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "NUH.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        // كوكي الدخول ماتتبعتش إلا على HTTPS. المنصة مربوطة على :80 و:443 والـ :80
        // بيحوّل على HTTPS، لكن من غير السطر ده أول نداء http قبل التحويل
        // بيطلّع الكوكي على الشبكة بالنص الواضح وتبقى جلسة كاملة مكشوفة.
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;

        // نداءات /api لازم ترجع كود حالة، مش صفحة HTML. الافتراضي في مصادقة الكوكي
        // إنها تحوّل على صفحة الدخول، فالـ JS كان بيستلم HTML بدل JSON ويفشل بصمت.
        options.Events.OnRedirectToLogin = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 401; return Task.CompletedTask; }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = ctx =>
        {
            if (ctx.Request.Path.StartsWithSegments("/api")) { ctx.Response.StatusCode = 403; return Task.CompletedTask; }
            ctx.Response.Redirect(ctx.RedirectUri);
            return Task.CompletedTask;
        };
    });

// ملاحظة: السكيم الذكي "NUH_Smart" فوق بيوثّق كل الـ [Authorize] (العادية واللي
// عليها Roles) بالكوكي أو الـ JWT حسب الطلب - ده جزء *التوثيق* (إنت مين).
// أما جزء *التصريح* (هل مسموح لك) فله DefaultPolicy مخصّصة تحت، وسببها مشروح
// عندها: التوثيق بيقول إن الكوكي صالح، وما بيقولش إن الحساب لسه نشط.

// ✅ Authorization - policy لكل صلاحية (permission)، الكنترولر بيستخدم [Authorize(Policy = "users.manage")]
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in ApplicationPermissions.All)
        options.AddPolicy(permission.Value, policy => policy.RequireClaim(ClaimConstants.Permission, permission.Value));

    // ========================================================================
    //  ⚠️ السياسة الافتراضية - كل [Authorize] مكتوب بلا Policy بيمرّ من هنا.
    //
    //     العيب اللي بتقفله: إيقاف الموظف بقى بيشيل أدواره وصلاحياته
    //     (PermissionClaimsTransformation)، فأي شاشة محميّة بصلاحية بترفضه.
    //     لكن عشر كنترولرات في النظام محميّة بـ [Authorize] مجرّد - يعني
    //     «أي حد مسجّل دخول» - وأهمها HomeController: لوحة التحكم بتقرا
    //     إحصائيات الطلاب وآخر ٦ طلاب بأسمائهم وآخر ٦ طلبات وبترندرهم من
    //     السيرفر. فالموظف الموقوف كان لسه بيشوف الأسماء دي، والقائمة
    //     الجانبية فاضية فمفيش حتى إشارة إنه المفروض مطرود.
    //
    //     ومكانها هنا لا على كل كنترولر: أي كنترولر جديد بـ [Authorize] مجرّد
    //     بياخد الفحص ده تلقائيًا. لو كتبناها على العشرة، الحادي عشر هيتنسى.
    // ========================================================================
    options.DefaultPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .RequireClaim(ClaimConstants.AccountActive)
        .Build();

    // ⚠️ الاستثناء الوحيد، وهو مقصود مش ثغرة: دي الصفحتان اللي *لازم* الموظف
    //    الموقوف يوصلهم وهو بلا أي سلطة -
    //      • /Account/Denied  لو اتقفلت، رفضها بيحوّل عليها هي نفسها = لوب
    //        تحويل لا نهائي، والمستخدم بيشوفه كأن الصفحة بتعيد تحميل نفسها.
    //      • /Account/Logout  لو اتقفلت، مايقدرش يمسح الكوكي بنفسه ويفضل
    //        عالق في شاشة الرفض لحد ما يقفل المتصفح خالص.
    //    الاتنين مالهمش أي بيانات، فمفيش حاجة تتسرّب منهم.
    options.AddPolicy("signedIn", policy => policy.RequireAuthenticatedUser());

    // ⚠️ السياسة المركّبة الوحيدة في النظام - دونات «حسابات شبكة السكن» في لوحة
    //    التحكم. كل ما فوق صلاحية = سياسة، وده لا يعبّر عن "أو". والدونات دي
    //    أداة الأمن السيبراني: هو من يُنشئ الحساب ويعطّله، لكنه لا يملك
    //    housing.view (شاشة إدارة الحسابات)، فكانت البطاقة تظهر له والطلب
    //    يرجع 403 فيبقى مكانها فارغًا.
    //    الردّ ثلاثة مجاميع لا بيانات طالب، ومن يملك أيًّا من الثلاثة يراها اليوم
    //    في الشاشة نفسها. مضاف هنا لا كصلاحية جديدة في قاعدة البيانات: صلاحية
    //    جديدة كانت تحتاج إسنادًا يدويًا لكل دور، وتُنسى مع أول دور يُضاف.
    options.AddPolicy("dashboard.accountStats", policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(ClaimConstants.Permission, "housing.view") ||
        ctx.User.HasClaim(ClaimConstants.Permission, "requests.reviewCyber") ||
        ctx.User.HasClaim(ClaimConstants.Permission, "requests.complete")));

    // ⚠️ شاشة «التقارير» بتقرا *كل* بياناتها من AuditLogsController، فاللي
    //    معاه reports.view وحدها كان بيفتحها ويلاقي نصّها شغّال ونصّها
    //    «تعذر الاتصال بالخادم». والمفارقة إن الأجزاء اللي كانت بتشتغل هي
    //    بالظبط الـ endpoints المفتوحة بلا صلاحية، واللي كان بيفشل هو
    //    المحميّ صح - يعني الشاشة كانت «شغّالة» بقدر الثغرة فيها.
    //    الشرطان معًا: إما تشتغل كاملة أو ما تظهرش أصلًا.
    //    ومكانها هنا لا كصلاحية جديدة في قاعدة البيانات: صلاحية جديدة تحتاج
    //    إسنادًا يدويًا لكل دور وتُنسى مع أول دور يُضاف.
    options.AddPolicy("reports.page", policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(ClaimConstants.Permission, "reports.view") &&
        ctx.User.HasClaim(ClaimConstants.Permission, "auditLogs.view")));

    // ====================================================================
    //  تسجيل طباعة وثيقة التعهّد (api/PledgeDoc).
    //
    //  ⚠️ «أو» لا «و»: الوثيقة بتتطبع من شاشتين - تفاصيل الطلب (requests.view)
    //     وملف الطالب (students.investigate) - والموظف عنده واحدة منهم مش
    //     الاتنين بالضرورة. لو طلبنا الاتنين، الأمن السيبراني بيطبع الورقة
    //     والطباعة مابتتسجّلش، وسجل التحقيق بيبقى ناقص من غير ما حد يلاحظ.
    //
    //  ⚠️ ومكانها هنا لا كصلاحية جديدة في قاعدة البيانات: هي مشتقّة من
    //     صلاحيات موجودة - «اللي بيقدر يفتح الوثيقة يقدر يسجّل طباعتها».
    //     صلاحية مستقلة كانت هتحتاج إسنادًا يدويًا لكل دور وتُنسى مع أول دور
    //     جديد. (نفس منطق reports.page فوق.)
    // ====================================================================
    options.AddPolicy("pledge.print", policy => policy.RequireAssertion(ctx =>
        ctx.User.HasClaim(ClaimConstants.Permission, "requests.view") ||
        ctx.User.HasClaim(ClaimConstants.Permission, "students.investigate")));
});

// ✅ CORS
var allowedOrigin = builder.Configuration.GetValue<string>("AllowedOrigin") ?? "*";
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (allowedOrigin == "*")
            policy.AllowAnyOrigin();
        else
            policy.WithOrigins(allowedOrigin);
        policy.AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// ✅ Data Protection - تشفير كوكي الجلسة ورموز مكافحة التزوير (AntiForgery)
// من غير الإعداد ده، الـ IIS بيحاول يخزّن المفاتيح في سجل ويندوز تحت ملف تعريف
// حساب الـ Application Pool. النتيجة:
//   • لو Load User Profile مقفول → استثناء عند توليد رمز AntiForgery
//     (صفحة /Account/Login بترمي "حدث خطأ غير متوقع")
//   • لو شغّال → المفاتيح مؤقتة، وكل recycle للـ pool بيسجّل خروج كل المستخدمين
// الحل: نحفظ المفاتيح في مجلد ثابت جنب التطبيق.
var keysPath = Path.Combine(builder.Environment.ContentRootPath, "keys");
Directory.CreateDirectory(keysPath);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    // اسم ثابت - عشان المفاتيح تفضل صالحة حتى لو اتغيّر مسار التطبيق
    .SetApplicationName("NUH-PORTAL");

// ✅ Rate Limiting
builder.Services.AddMemoryCache();

// ⚠️ الصلاحيات تُقرأ من الدور في كل طلب بدل أن تُطبع في الكوكي/التوكن لحظة
//    الدخول. بدونها: إضافة صلاحية لا تصل لمن هو مسجَّل دخوله (403 حتى يخرج
//    ويدخل)، وسحب صلاحية لا يُطبَّق فورًا - وهذه ثغرة لا مجرد إزعاج.
builder.Services.AddScoped<Microsoft.AspNetCore.Authentication.IClaimsTransformation,
                           NUH_PORTAL.Core.PermissionClaimsTransformation>();

// ✅ ضغط الردود - الموقع كان بيبعت كل حاجة بدون ضغط.
//    صفحة الموظف الواحدة فيها قاموس ترجمة محقون بالـ inline، وده لوحده كان
//    ~142 كيلوبايت خام لكل تنقّل. مع Brotli بينزل لأقل من 15.
//    EnableForHttps = true مطلوب لأن الموقع كله HTTPS - من غيرها الضغط
//    مبيشتغلش أصلاً. (خطر BREACH نظري هنا: توكن الـ antiforgery في
//    ASP.NET Core متعشّى عشوائيًا في كل رد، والنظام داخلي خلف جدار الجامعة.)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/json", "application/javascript", "text/javascript", "image/svg+xml" });
});
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.GeneralRules = new List<RateLimitRule>
    {
        // تسجيل الدخول - مكافحة تخمين كلمة المرور
        // ⚠️ القاعدة كانت على /api/Auth/Login بس، ومفيش أي واجهة بتستخدم المسار ده.
        //    نموذج دخول الموظفين الفعلي بيرسل على POST /Account/Login (Views/Account/Login.cshtml)
        //    وكان بلا أي حد - يعني الحماية كانت على باب مقفول والباب المفتوح جنبه.
        // ⚠️ القاعدة على /api/Auth/Login اتشالت مع المسار نفسه - الحدّ على مسار
        //    مش موجود بيوهم إن الحماية مضاعفة وهي على باب متشال أصلًا.
        new RateLimitRule { Endpoint = "POST:/Account/Login", Period = "1m", Limit = 10 },
        // إرسال OTP - مكافحة سبام الرسائل وتعداد الأرقام (الخدمة كمان بتمنع طلب قبل مرور دقيقة)
        new RateLimitRule { Endpoint = "POST:/api/Otp/send", Period = "10m", Limit = 5 },
        // التحقق من OTP - مكافحة التخمين (الخدمة كمان بتقفل بعد 3 محاولات)
        new RateLimitRule { Endpoint = "POST:/api/Otp/verify", Period = "10m", Limit = 10 },
        // التتبع العام - مكافحة تعداد الطلبات بالأرقام المتسلسلة أو بالموبايل
        new RateLimitRule { Endpoint = "*:/api/RequestTracking/*", Period = "1m", Limit = 20 },
        // ====================================================================
        //  التحقّق من وثيقة التعهّد - صفحة عامة بتقرا رمزًا مطبوعًا.
        //
        //  ⚠️ الحدّ ده جزء أصيل من قوة الرمز لا إضافة احترازية: بصمة الرمز
        //     ٣٥ بت، يعني التخمين محتاج مليارات المحاولات - وde آمن **لأن**
        //     المحاولات محدودة. من غير الحدّ، الصفحة بتبقى أداة تخمين شغّالة
        //     على مدار الساعة.
        //  ⚠️ والحدّ على المسار كله (GET الصفحة): مفيش مسار API منفصل عن قصد،
        //     عشان مايبقاش فيه باب تاني لنفس التحقّق بلا نفس الحدّ.
        // ====================================================================
        new RateLimitRule { Endpoint = "*:/Verify*", Period = "1m", Limit = 12 },
        // بدء التسجيل الذاتي - مكافحة السبام
        new RateLimitRule { Endpoint = "POST:/api/Registration/start", Period = "1h", Limit = 10 },
        // الفحص المبكر: سخيّ بما يكفي للتصحيح الطبيعي، وضيّق بما يمنع التجريب بالجملة
        new RateLimitRule { Endpoint = "POST:/api/Registration/check-duplicate", Period = "1h", Limit = 30 },
        // ⚠️ نقاط الفحص الصحي: مفتوحة بلا مصادقة، وكل نداء عليها **يفتح اتصالًا
        //    بقاعدة البيانات ويستدعي وحدة التحكم بالنطاق** (CheckSqlHealthAsync
        //    و CheckHealthAsync معًا في /api/Health و /api/Health/ready). يعني
        //    طلب واحد رخيص على المهاجم يكلّف الخادم استعلامًا وجلسة LDAP -
        //    وهذا تضخيم، لا مجرد حِمل. ولا يناديها إنسان أصلًا: مراقبة آلية
        //    وحدها، وأي مراقب معقول يفحص كل دقيقة أو أقل، فعشرون في الدقيقة
        //    سقف واسع لها وضيّق على من يكرّرها بالآلاف.
        new RateLimitRule { Endpoint = "*:/api/Health*", Period = "1m", Limit = 20 },
        // ⚠️ شبكة الأمان: حدّ عام على كل واجهات الـ API.
        //
        //    القواعد فوق تغطّي ست نقاط **بالاسم**. وأي نقطة تُضاف بعد شهر
        //    تولد بلا حدّ ولن يتذكّرها أحد - وهذا ما حدث فعلًا مع نقاط الفحص
        //    الصحي، ومع /Account/Login قبلها. القاعدة دي بتغطّي الموجود
        //    والقادم معًا، والقواعد الأضيق فوقها تبقى هي الحاكمة في نقاطها.
        //
        //    ⚠️ /api/* لا *: الثانية تشمل ملفات CSS والخطوط والصور، وتحديث
        //       صفحة واحد يجلب عشرات منها - فكان المطوّر نفسه يصطدم بالحدّ
        //       وهو يضغط Ctrl+Shift+R. أما نداءات الـ API فصفحة الموظف تصدر
        //       خمسة إلى ثمانية عند التحميل، فثلاثمائة في الدقيقة تساوي نحو
        //       أربعين تحميل صفحة من الجهاز الواحد.
        //
        //    ⚠️ والعدّ لكل عنوان IP. تحقّقنا من سجل الدخول: الأجهزة تظهر
        //       بعناوين مستقلة عبر عدّة شبكات فرعية (10.65.35.x و10.65.36.x
        //       و10.65.38.x)، فلا NAT يجمع الموظفين في عنوان واحد. لو تغيّر
        //       ذلك يومًا ووُضع proxy أمام المنصة، فراجع الحدّ ده أول شيء.
        new RateLimitRule { Endpoint = "*:/api/*", Period = "1m", Limit = 300 },
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// ✅ Active Directory
var adConfig = builder.Configuration.GetSection("ActiveDirectory").Get<ActiveDirectoryConfig>();
if (adConfig == null || string.IsNullOrEmpty(adConfig.Domain) || string.IsNullOrEmpty(adConfig.DomainController))
{
    throw new InvalidOperationException("ActiveDirectory configuration is missing. Check 'ActiveDirectory:Domain' and 'ActiveDirectory:DomainController' in appsettings.json.");
}
if (adConfig.Port != 636)
{
    Console.WriteLine("WARNING: ActiveDirectory port is set to {0}. LDAPS (port 636) is required for production security. Port 389 (plain LDAP) has been rejected.", adConfig.Port);
    adConfig.Port = 636;
}
builder.Services.Configure<ActiveDirectoryConfig>(builder.Configuration.GetSection("ActiveDirectory"));
builder.Services.Configure<ADServiceAccountConfig>(builder.Configuration.GetSection("ADServiceAccount"));
// سكن أعضاء هيئة التدريس - مسارات الـ OU. في الإعدادات مش في الكود عشان نقل
// أو تصحيح اسم OU في الدومين يبقى تعديل إعداد، مش build ونشر.
builder.Services.Configure<FacultyHousingConfig>(builder.Configuration.GetSection("FacultyHousing"));
builder.Services.AddSingleton<ActiveDirectoryService>();
// أماكن حسابات الطلاب في الدليل - مصدر واحد لخدمة الإنشاء ولأدوات التشخيص.
// Scoped لأنه بيقرا من ADConfigurations (AppDbContext).
builder.Services.AddScoped<AdDirectoryLayout>();
builder.Services.AddScoped<ADProvisioningService>();
builder.Services.AddScoped<FacultyHousingService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

// ✅ Layered architecture (Repository + UnitOfWork + Mapster) - نمط permits
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(Mapster.TypeAdapterConfig.GlobalSettings);
builder.Services.AddScoped<MapsterMapper.IMapper, MapsterMapper.ServiceMapper>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IUnitOfWork, HttpUnitOfWork>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IRequestTrackingService, RequestTrackingService>();
builder.Services.AddScoped<IRequestService, RequestService>();
builder.Services.AddScoped<IStudentStatusService, StudentStatusService>();
builder.Services.AddScoped<IWorkflowActionService, WorkflowActionService>();
builder.Services.AddScoped<ILookupService, LookupService>();
// بيانات إقلاع الواجهة (قاموس الترجمة + خريطة القوائم) - مخزّنة في IMemoryCache
// بدل ما تتبني من الأول في كل طلب صفحة. راجع UiBootstrapService للتفاصيل.
builder.Services.AddScoped<IUiBootstrapService, UiBootstrapService>();
builder.Services.AddScoped<ILookupAdminService, LookupAdminService>();
builder.Services.AddScoped<ILookupResolver, LookupResolver>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRoleAdminService, RoleAdminService>();
builder.Services.AddScoped<IHousingAccountService, HousingAccountService>();
builder.Services.AddScoped<ISupervisorHousingTransferService, SupervisorHousingTransferService>();
// خريطة إشغال المباني - قراءة فقط، بتشتقّ الإشغال من صفوف الطلاب في كل نداء.
builder.Services.AddScoped<IHousingOccupancyService, HousingOccupancyService>();
// حارس سعة الغرفة - نفس القاعدة في مسارات الكتابة الخمسة كلها.
builder.Services.AddScoped<IHousingCapacityGuard, HousingCapacityService>();
// مكان تخزين المرفقات - Singleton لأنه بيقرأ الإعدادات مرة واحدة وبعدها بيحسب مسارات بس.
// المسار بيتظبط من Storage:AttachmentsRoot في appsettings.
builder.Services.AddSingleton<IAttachmentStorage, AttachmentStorage>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<ILogQueryService, LogQueryService>();
builder.Services.AddScoped<IPledgeService, PledgeService>();
builder.Services.AddScoped<IStudentFileService, StudentFileService>();
builder.Services.AddScoped<IRegistrationFlowService, RegistrationFlowService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IOtpFlowService, OtpFlowService>();
builder.Services.AddScoped<IBulkRegistrationService, BulkRegistrationService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IADSetupService, ADSetupService>();

var svcAcct = builder.Configuration.GetSection("ADServiceAccount").Get<ADServiceAccountConfig>();
if (svcAcct == null || string.IsNullOrEmpty(svcAcct.Username) || svcAcct.Username == "#{AD_SERVICE_USERNAME}#")
{
    Console.WriteLine("WARNING: ADServiceAccount is not configured. AD management operations (OU validation, account creation) will be unavailable until a service account is configured.");
}

// ✅ Forwarded Headers - مطلوب عشان يكون الشغل ورا IIS ARR
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);
});

var app = builder.Build();

// ⚠️ مسح ترويسات عنوان العميل اللي بتجي من برّا - لازم يكون أول حاجة خالص.
//
// التطبيق مش ورا أي reverse proxy: IIS مربوط عليه in-process والـ bindings
// مباشرة (‎:80 و:443)، يعني مفيش حد موثوق بيضيف X-Real-IP أو X-Forwarded-For.
// ومع كده AspNetCoreRateLimit مضبوط على RealIpHeader = "X-Real-IP" وبيصدّق الترويسة
// زي ما هي. فأي حد يبعت X-Real-IP بقيمة مختلفة مع كل محاولة يبقى عميل
// جديد في نظر الحد، وحد الـ 10 محاولات/دقيقة على الدخول يبقى بلا معنى - وكذلك
// حدود OTP والتتبع والتسجيل الذاتي. بنمسحهم فيبقى المصدر الوحيد للعنوان
// هو عنوان الاتصال الحقيقي (Connection.RemoteIpAddress) اللي ماينفعش يتزوّر.
//
// لو اتحط قدّام المنصة proxy فعلي يوم من الأيام، امسح الـ middleware دي
// وضيف عنوان الـ proxy في KnownProxies فوق - ماتسيبهمش مفتوحين للكل.
app.Use(async (ctx, next) =>
{
    ctx.Request.Headers.Remove("X-Real-IP");
    ctx.Request.Headers.Remove("X-Forwarded-For");
    ctx.Request.Headers.Remove("X-Forwarded-Proto");
    ctx.Request.Headers.Remove("X-Forwarded-Host");
    await next();
});

// ✅ Forwarded Headers - لازم يكون أول Middleware
app.UseForwardedHeaders();

// ====================================================================
//  ⏱️ قياس زمن الخادم - يُكتب في ترويسة كل رد.
//
//  ⚠️ ليه ده موجود:
//     «الصفحة بطيئة» جملة مش قابلة للإصلاح لوحدها. الوقت اللي بيحسّه المستخدم
//     مجموع ثلاث حاجات مختلفة تمامًا في العلاج: زمن الخادم (استعلام/كود)،
//     وزمن الشبكة، وزمن المتصفح (رسم وجافاسكريبت). ومن غير رقم، أي إصلاح
//     بيبقى تخمين - وبيتصلّح الجزء الغلط.
//
//     الترويسة دي بتفصل الجزء الأول عن الباقي: تفتح F12 ← Network ← أي نداء،
//     ولو Server-Timing قال ٢٠ مللي والنداء واخد ثانيتين، فالمشكلة مش في
//     الخادم. Server-Timing ترويسة قياسية والمتصفح بيعرضها في تبويب Timing.
//
//     وأي طلب بيعدّي الحد المسموح بيتسجّل في **سجل الأخطاء** برسالة عربية
//     تقول المدة والسبب المرجّح - فالبطء بيبان لوحده بدل ما المستخدم يقول
//     «في تأخير» ونفضل نخمّن. الحد من الإعدادات: Diagnostics:SlowRequestMs.
//
//  ⚠️ أول middleware عمليًا (بعد ترويسات البروكسي) عشان يقيس كل اللي بعده.
// ====================================================================
// الحد الفاصل بين «بطيء» و«طبيعي» - من الإعدادات، الافتراضي ٣ ثوانٍ.
var slowRequestMs = builder.Configuration.GetValue("Diagnostics:SlowRequestMs", 3000);

app.Use(async (context, next) =>
{
    var started = System.Diagnostics.Stopwatch.GetTimestamp();

    // الترويسة لازم تتكتب قبل ما يبدأ الرد يتبعت - OnStarting هي اللحظة دي.
    context.Response.OnStarting(() =>
    {
        var ms = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;
        context.Response.Headers["Server-Timing"] =
            "app;dur=" + ms.ToString("F1", System.Globalization.CultureInfo.InvariantCulture);
        return Task.CompletedTask;
    });

    await next();

    var total = System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds;

    if (total > slowRequestMs)
    {
        app.Logger.LogWarning("SLOW {Method} {Path} - {Ms:F0} ms",
            context.Request.Method, context.Request.Path.Value, total);

        // يُكتب في سجل الأخطاء برسالة عربية. مؤجّل عن مسار الرد فلا يؤخّر المستخدم.
        await NUH_PORTAL.Core.Diagnostics.RequestDiagnostics
            .RecordSlowRequestAsync(context, total, slowRequestMs);
    }
});

// ====================================================================
//  ضغط الردود - قابل للإطفاء من الإعدادات بلا إعادة بناء.
//
//  ⚠️ السبب: السجل أثبت إن النداءات ذات الرد الكبير (سجل العمليات، سجل
//     الأخطاء، سجل الدخول، المستخدمون) بتدخل التطبيق (REQ-IN) وما بتخرجش
//     أبدًا (لا REQ-OUT)، بينما النداءات ذات الرد الصغير (الإشعارات،
//     الأدوار، قائمة مستخدمي الفلتر) بترجع في ٤-٣٤ مللي. الحجم هو الفيصل
//     الوحيد - لا الاستعلام ولا الدور ولا الشاشة.
//
//     ودي بصمة تعارض في الضغط: التطبيق بيضغط الرد و IIS بيضغطه كمان،
//     فالرد الصغير بيعدّي (تحت عتبة IIS) والكبير بيقف. إطفاء طبقة واحدة
//     بيحسم السبب، ولو اتأكد يفضل الضغط عند IIS وحده - وده أكفأ أصلًا
//     لأنه بيحصل خارج عملية التطبيق.
//
//     ⚠️ النتيجة بعد التجربة: الضغط لم يكن السبب. السبب كان منحة ذاكرة
//        ضخمة في SQL Server (RESOURCE_SEMAPHORE) من وسيط قائمة يُترجَم إلى
//        OPENJSON. فعاد الضغط للعمل داخل التطبيق، وأُطفئ الضغط الديناميكي في
//        IIS من web.config - طبقة واحدة تضغط، لا اثنتان.
//
//     المفتاح: "ResponseCompression:Enabled" في appsettings - الافتراضي true.
// ====================================================================
if (builder.Configuration.GetValue("ResponseCompression:Enabled", true))
    app.UseResponseCompression();

// ✅ معالجة الأخطاء المركزية (UserFriendlyException → JSON نظيف، وأي خطأ تاني → 500 من غير تسريب تفاصيل)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ملاحظة: مخطط قاعدة البيانات يُدار عبر EF Migrations (مجلد Migrations + سكربتات deployment/migrations).
// التعديلات اللي كانت بتتنفّذ هنا وقت التشغيل (drop CHECK / add bulk_request_id / varchar→nvarchar)
// أصبحت متضمّنة في الـ migrations واتشالت. لتهيئة قاعدة بيانات جديدة استخدم: dotnet ef database update

// ✅ الأدوار وصلاحياتها - في كل البيئات، لأنها بيانات أساسية مش بيانات تطوير.
// من غيرها جدول AspNetRoleClaims بيفضل فاضي، فكل [Authorize(Policy = "...")] بيفشل،
// وبما إن AccessDeniedPath تحت هو نفسه صفحة الدخول، المستخدم بيبان كأنه بيتسجّل
// خروج فور ما يدخل - وهو في الحقيقة داخل بس بصفر صلاحيات.
// try/catch عشان لو الـ migration لسه ماتطبّقتش مايكسرش الإقلاع.
try
{
    using var roleScope = app.Services.CreateScope();
    var roleMgr = roleScope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
    await DbSeeder.SeedRolesAndPermissionsAsync(roleMgr, app.Logger);

    // إنذار مبكر: دور بلا صلاحيات معناه إن كل صفحاته هترفض المستخدم. ده بالظبط
    // اللي كان بيحصل ومحدش واخد باله، فبنسجّله في السجل بدل ما نستنى شكوى.
    foreach (var roleName in RoleNames.Staff)
    {
        var role = await roleMgr.FindByNameAsync(roleName);
        if (role == null) { app.Logger.LogWarning("Role {Role} is missing entirely.", roleName); continue; }
        var count = (await roleMgr.GetClaimsAsync(role)).Count(c => c.Type == ClaimConstants.Permission);
        if (count == 0)
            app.Logger.LogWarning("Role {Role} has ZERO permission claims - every page will deny its users.", roleName);
        else
            app.Logger.LogInformation("Role {Role}: {Count} permissions.", roleName, count);
    }
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Role/permission seeding skipped - run 'dotnet ef database update' first.");
}

// ✅ Seed dev users (Development فقط) - للدخول عبر local fallback من غير AD
// بينشئ: admin / cyber / supervisor / user - كلهم بالباسورد Test@123
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedUserManager = seedScope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var seedRoleManager = seedScope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
    await DbSeeder.SeedDevUsersAsync(seedUserManager, seedRoleManager);
    DbSeeder.SeedDevData(seedDb);
}

// ⚠️ كان هنا كتلة تانية بتمنح دور admin كل الصلاحيات في كل إقلاع.
//
//    اتشالت لأنها كانت **نسخة تانية من نفس القاعدة** اللي في
//    DbSeeder.SeedRolesAndPermissionsAsync، وده اللي خلّى العطل يعيش بعد ما
//    اتصلّح: الصلاحية اللي بيشيلها مدير النظام من شاشة الأدوار كانت بترجع مع
//    أول إعادة تدوير للـ application pool، وإصلاح مكان واحد مكانش بيكفّي لأن
//    التاني لسه بيمنحها.
//
//    والتعليق اللي كان فوقها بيوضّح إزاي حصل ده: هي اتكتبت لمّا كان الـ seeder
//    بيشتغل في بيئة التطوير بس، فكان لازم حاجة تستكمل صلاحيات admin في
//    الإنتاج. بعدين الـ seeder اتنقل يشتغل في كل البيئات - والكتلة دي فضلت
//    مكانها، وبقت الصلاحية بتتمنح مرتين من مكانين مالهمش علاقة ببعض.
//
//    والحاجة اللي كانت بتحلّها لسه متحلّة: صلاحية جديدة في نسخة أحدث لازم
//    توصل admin وإلا الشاشة الجديدة تفضل مقفولة على الكل - بس دي بقت في
//    GrantNewPermissionsToAdminAsync، وبطريقة بتفرّق بين «صلاحية جديدة على
//    النظام» و«صلاحية اتشالت بإيد مدير النظام».

// ✅ زرع القوائم المرجعية (lookups) - في كل البيئات لأنها بيانات أساسية.
// try/catch عشان لو الـ migration الخاصة بالقوائم لسه ماتطبّقتش مايكسرش الإقلاع.
try
{
    using var lookupScope = app.Services.CreateScope();
    var lookupDb = lookupScope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.SeedLookups(lookupDb);
    DbSeeder.BackfillStudentLookups(lookupDb);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Lookup seeding skipped - run 'dotnet ef database update' first (lookup tables may not exist yet).");
}

// ====================================================================
//  🔤 فحص النصوص المعروضة عند الإقلاع.
//
//  ⚠️ العيب اللي بيعالجه:
//     الأكواد (زي ad_provisioning أو user_created) بتتولد في الكود C#،
//     ونصوصها المعروضة عايشة في ملف ترجمة منفصل. مفيش رابط بين الاتنين،
//     فلو كود اتضاف من غير مفتاح ترجمة، الشاشة بتعرض الكود الخام للمستخدم
//     - وماحدش بيعرف غير لما حد يشوفه بعينه في الإنتاج. وده اللي حصل فعلًا.
//
//     الفحص ده بيقرأ الأكواد *الموجودة فعلًا في قاعدة البيانات* (مش من قراءة
//     الكود، عشان ما يفوتوش حاجة)، ويقارنها بملف الترجمة، ويكتب تحذيرًا في
//     السجل بأي كود بلا نص. فالنقص بيبان في سجل الأخطاء بدل شاشة المستخدم.
//
//  استعلامان صغيران مرة واحدة عند الإقلاع - بلا أي تكلفة على الطلبات.
// ====================================================================
try
{
    using var textScope = app.Services.CreateScope();
    var textDb = textScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var loc = textScope.ServiceProvider
        .GetRequiredService<Microsoft.Extensions.Localization.IStringLocalizer<NUH_PORTAL.SharedResource>>();

    var known = loc.GetAllStrings(true).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);

    // (بادئة المفتاح، وصف المكان، الأكواد الموجودة في قاعدة البيانات)
    var families = new (string Prefix, string Where, List<string?> Codes)[]
    {
        ("aud_action_", "سجل العمليات",
            await textDb.AuditLogs.AsNoTracking()
                .Select(a => a.action).Distinct().ToListAsync()),

        ("ss_status_", "تحديث حالة الطالب",
            await textDb.StudentStatusActions.AsNoTracking()
                .Select(a => a.StatusType).Distinct().ToListAsync())
    };

    var missing = families
        .SelectMany(f => f.Codes
            .Where(c => !string.IsNullOrWhiteSpace(c) && !known.Contains(f.Prefix + c))
            .Select(c => $"{f.Prefix}{c} ({f.Where})"))
        .OrderBy(x => x, StringComparer.Ordinal)
        .ToList();

    if (missing.Count > 0)
        app.Logger.LogWarning(
            "MISSING UI TEXT - {Count} code(s) appear on screen as raw codes because they have no key in SharedResource.resx: {Keys}",
            missing.Count, string.Join(", ", missing));
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "UI text check skipped.");
}

// ====================================================================
//  🔐 فحص أسماء الصلاحيات عند الإقلاع.
//
//  ⚠️ العيب اللي بيعالجه:
//     كل [Authorize(Policy = "...")] بيشاور على اسم نصّي، والأسماء دي
//     بتتسجّل من ApplicationPermissions.All فوق. لو الاسم في الكنترولر
//     مش موجود في القائمة، ASP.NET بيرمي استثناء وقت الطلب - يعني الشاشة
//     بترجّع 500 والمستخدم مايعرفش السبب، والمطوّر مايكتشفش غير بالصدفة.
//
//     وده حصل فعلًا: "requests.process" و "students.manage" كانوا مكتوبين
//     في أربع كنترولرات وهما مش من الصلاحيات المعرّفة، فمرفقات الطلبات
//     وكل عمليات الكتابة على الطلاب كانت واقفة بـ 500 من غير ما حد يلاحظ.
//
//     الفحص ده بيقرأ كل سمات التفويض في التجميعة ويقارنها بالقائمة، ويكتب
//     تحذيرًا واضحًا في السجل لحظة الإقلاع. غلطة الطباعة بتبان في ثانية
//     بدل ما تفضل مستخبية لشهور.
// ====================================================================
try
{
    var known = new HashSet<string>(
        NUH_PORTAL.Core.ApplicationPermissions.All.Select(p => p.Value),
        StringComparer.Ordinal);

    var unknown = typeof(Program).Assembly.GetTypes()
        .Where(t => typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)
                 || typeof(Microsoft.AspNetCore.Mvc.Controller).IsAssignableFrom(t))
        .SelectMany(t => t.GetCustomAttributes(typeof(AuthorizeAttribute), true)
            .Concat(t.GetMethods().SelectMany(m => m.GetCustomAttributes(typeof(AuthorizeAttribute), true))))
        .Cast<AuthorizeAttribute>()
        .Select(a => a.Policy)
        .Where(pol => !string.IsNullOrWhiteSpace(pol))
        .Select(pol => pol!)
        // أسماء مخططات المصادقة مش سياسات صلاحيات
        .Where(pol => pol != "NUH_Smart")
        .Distinct(StringComparer.Ordinal)
        .Where(pol => !known.Contains(pol))
        .OrderBy(x => x, StringComparer.Ordinal)
        .ToList();

    if (unknown.Count > 0)
        app.Logger.LogError(
            "UNKNOWN AUTHORIZATION POLICY - {Count} policy name(s) used in controllers do not exist in ApplicationPermissions.All. Every endpoint using them returns 500: {Names}",
            unknown.Count, string.Join(", ", unknown));
    else
        app.Logger.LogInformation("Authorization policy check passed - {Count} permissions registered.", known.Count);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Authorization policy check skipped.");
}

// ============================================================================
//  ترويسات الأمان - على كل ردّ.
//
//  ⚠️ سياسة المحتوى (CSP) اتضافت لأن الواجهة بتبني HTML بـ innerHTML في ١٢٣
//     موضع. الهروب (escHtml) مطبَّق في كل موضع منهم - اتفحصوا واحدًا واحدًا -
//     لكن ده بيعتمد على إن كل تعديل جاي يفتكر يستعمله. الـ CSP هي الشبكة اللي
//     بتمسك أول سهو، والفرق بينها وبين الهروب إنها بتشتغل من غير ما حد يفتكرها.
//
//  ⚠️ السياسة المطبَّقة فيها 'unsafe-inline' للسكربتات **مؤقتًا**: التخطيط
//     والشاشات فيها سكربتات مكتوبة جوّه الصفحات، ومنعها النهاردة بيكسر النظام.
//     ومع ذلك هي مش بلا قيمة - من غير أي تعديل تاني هي بتمنع:
//       • تحميل سكربت من دومين تاني (فحقن XSS مايقدرش يجيب حمولته من بره)
//       • إرسال أي نموذج لدومين تاني (سرقة بيانات النماذج)
//       • حقن <base> اللي بيحوّل كل الروابط النسبية لدومين المهاجم
//       • الإضافات والكائنات المدمجة (object/embed)
//       • تضمين الصفحة في إطار من دومين تاني
//
//  ⚠️ والترويسة التانية (Report-Only) هي السياسة **المستهدَفة** بلا
//     'unsafe-inline': مابتمنعش حاجة، بتسجّل المخالفات في وحدة تحكّم المتصفح
//     بس. تشغّل النظام يومين، تشوف قدّ إيه سكربت داخلي محتاج نقل لملف أو
//     nonce، وبعدين تتحوّل السياسة الأولى للمستهدَفة وتتشال دي.
//     من غير الخطوة دي كان لازم نختار بين سياسة بتكسر شاشات لا حد يعرف
//     أنهي واحدة، وسياسة مابتحميش - والاتنين غلط.
//
//  ⚠️ ومفيش أي دومين خارجي مسموح: النظام كله بيحمّل من نفسه. كان فيه استثناء
//     واحد لـ cdn.jsdelivr.net في شاشة التقارير واتشال مع التحميل الاحتياطي
//     نفسه (الشرح في Views/Reports/Index.cshtml). القاعدة دي هي أقوى حاجة في
//     السياسة: حقن XSS مايقدرش يجيب حمولته من بره مهما كان.
// ============================================================================

// المطبَّقة دلوقتي - مافيهاش أي احتمال كسر
const string CspEnforced =
    "default-src 'self'; " +
    "script-src 'self' 'unsafe-inline'; " +
    "style-src 'self' 'unsafe-inline'; " +
    // ⚠️ blob: مطلوبة لمعاينة المرفق قبل رفعه: الشاشة بتعمل
    //    URL.createObjectURL للملف اللي المستخدم اختاره وبتعرضه في <img>،
    //    والعنوان ده blob: لا 'self'. من غيرها الصورة كانت بتتحجب بصمت -
    //    نافذة المعاينة بتفتح وفيها اسم الملف بس (نصّ alt لصورة مكسورة)،
    //    ومفيش أي رسالة تقول إن السبب سياسة المحتوى.
    // ⚠️ وهي مش توسعة للسياسة: عناوين blob: بتتولّد جوّه الصفحة نفسها من
    //    ملف في يد المستخدم، فمالهاش مصدر خارجي أصلًا.
    "img-src 'self' data: blob:; " +
    "font-src 'self' data:; " +
    "connect-src 'self'; " +
    "object-src 'none'; " +
    "base-uri 'self'; " +
    "form-action 'self'; " +
    "frame-ancestors 'self'";

// المستهدَفة - بتتسجّل بس، والفرق الوحيد إن السكربتات الداخلية ممنوعة
const string CspTarget =
    "default-src 'self'; " +
    "script-src 'self'; " +
    "style-src 'self' 'unsafe-inline'; " +
    "img-src 'self' data: blob:; " +
    "font-src 'self' data:; " +
    "connect-src 'self'; " +
    "object-src 'none'; " +
    "base-uri 'self'; " +
    "form-action 'self'; " +
    "frame-ancestors 'self'";

app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";                  // منع المتصفح من تخمين نوع المحتوى
    h["X-Frame-Options"] = "SAMEORIGIN";                      // منع تضمين الصفحات في إطارات خارجية (clickjacking)
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";

    // ⚠️ الصلاحيات دي النظام مابيستعملهاش، وقفلها بيمنع أي سكربت محقون
    //    يطلبها من المستخدم باسم الموقع.
    h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";

    // ⚠️ مسار المرفقات بيحطّ سياسته الأشدّ (default-src 'none') بعد كده
    //    وبيستبدل دي - وde مقصود: ملف مرفوع من مستخدم مالوش يشغّل أي حاجة.
    h["Content-Security-Policy"] = CspEnforced;
    h["Content-Security-Policy-Report-Only"] = CspTarget;

    await next();
});

// ✅ HSTS في الإنتاج بس (يفرض HTTPS على المتصفح)
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// ✅ Swagger في Development بس
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ✅ HttpsRedirection - يتفعّل فقط لو التطبيق مش ورا Proxy بيعمل TLS termination
var httpsRedirectEnabled = builder.Configuration.GetValue<bool?>("HttpsRedirect:Enabled");
if (httpsRedirectEnabled == true)
{
    app.UseHttpsRedirection();
}

// ✅ Rate Limiting قبل كل حاجة
app.UseIpRateLimiting();

app.UseCors("AllowFrontend");

// ✅ توطين الطلب - يحدد ثقافة الطلب من الكوكي قبل ترندرة أي صفحة MVC (لازم قبل الـ endpoints)
app.UseRequestLocalization();

// ✅ الترتيب مهم
app.UseAuthentication();

// ✅ Session activity middleware - bقفل نداءات الـ API بعد 15 دقيقة خمول
//
// ⚠️⚠️ باج أقفل النظام على كل المستخدمين (٤ أغسطس ٢٠٢٦):
//    الكود القديم كان بيرجّع 401 ويعمل return **قبل** ما يحدّث أو يمسح القيمة
//    المخزّنة، والقيمة دي عمرها كان ٨ ساعات. النتيجة: أول ما مستخدم يعدّي ١٥ دقيقة
//    خمول، القيمة القديمة بتفضل مكانها وكل نداء بعد كده بيقع في نفس الشرط -
//    حتى بعد تسجيل دخول جديد بكوكي جديد، لأن المفتاح مربوط برقم المستخدم مش
//    بالجلسة. يعني المستخدم بيتقفل ٨ ساعات كاملة أو لحد ما التطبيق يعيد التشغيل.
//
//    الأعراض كانت مضلّلة: صفحات الـ MVC بتفتح 200 عادي (الفحص ده على /api بس)
//    وكل نداء API بيرجّع 401، والجافاسكريبت بيحوّل على صفحة الدخول - فالمستخدم
//    بيشوف نفسه «بيدخل ويطلع» وهو في الحقيقة داخل وجلسته سليمة.
//
//    الإصلاح: امسح القيمة وقت انتهاء الجلسة (فتسجيل الدخول التالي يشتغل عادي)،
//    وقلّل عمر التخزين لساعة بدل ٨ ساعات عشان أي خلل مستقبلي يتصحّح لوحده.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        context.User.Identity?.IsAuthenticated == true &&
        !context.Request.Path.StartsWithSegments("/api/Auth/Ping"))
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        // ⚠️ TryParse لا Parse: العلامة دي جاية من التوكن/الكوكي، وأي قيمة مش
        //    رقمية كانت هترمي استثناء في الميدلوير - يعني 500 على كل نداء API.
        if (int.TryParse(userId, out var activityUserId))
        {
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
            var cacheKey = NUH_PORTAL.Core.SessionPolicy.ActivityKey(activityUserId);
            // لو فيه نشاط سابق مسجّل وعدّت عليه مهلة الخمول → اقفل الجلسة.
            // الرقم من Core/SessionPolicy.cs - نفس اللي بتقراه الواجهة، فما يفترقوش.
            if (cache.TryGetValue(cacheKey, out DateTime lastActivity) &&
                (DateTime.UtcNow - lastActivity).TotalMinutes > NUH_PORTAL.Core.SessionPolicy.IdleTimeoutMinutes)
            {
                // امسح العدّاد الأول - من غير السطر ده المستخدم يفضل مقفول حتى بعد
                // ما يسجّل دخول من جديد، وده بالظبط اللي كان بيحصل.
                cache.Remove(cacheKey);

                // انتهت الجلسة بعدم النشاط - نسجّل خروج كوكي الـ MVC كمان عشان صفحة الدخول
                // متردّش المستخدم على الصفحة تاني (كسر لوب التحويل)، ونرجّع 401 للنداء الحالي.
                await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.StatusCode = 401;
                return;
            }
            // حدّث آخر نشاط على كل طلب مُصادَق عليه (نافذة منزلقة) - مش معتمد على /Ping بس.
            // العمر ساعة: أطول من نافذة الخمول بكتير فالفحص شغّال، وقصير كفاية إن أي
            // قيمة عالقة تختفي لوحدها بدل ما تقفل مستخدم يوم شغل كامل.
            //
            // ⚠️ استثناء النداءات الخلفية: جرس الإشعارات بينادي كل ٣٠ ثانية وشارة
            //    الطلبات كل ٦٠ ثانية، وهما شغّالين والمستخدم سايب الشاشة. لو جدّدنا
            //    وقت النشاط منهم، «آخر نشاط» عمره ما هيعدّي ٣٠ ثانية ومهلة الخمول
            //    عمرها ما هتتحقّق ما دام التبويب مفتوح - وده اللي كان بيمنع الخروج
            //    التلقائي أصلًا. النداء الخلفي بيتفحص عادي (فبياخد 401 لما تنتهي
            //    الجلسة) لكنه ما بيمدّهاش.
            var isBackgroundPoll = context.Request.Headers
                .ContainsKey(NUH_PORTAL.Core.SessionPolicy.BackgroundPollHeader);
            if (!isBackgroundPoll)
                cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromHours(1));
        }
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();

// ✅ تحويلات الصفحات القديمة → صفحات الـ MVC الجديدة
// الملفات القديمة لسه موجودة على الديسك كباك أب، بس أي رابط ليها بيترمي على الجديد -
// كده السايدبار والشكل موحدين في كل الشاشات مهما كان مصدر الرابط (هيستوري/بوكمارك/لينك جوه صفحة قديمة).
// (index.html بوابة الدخول بره الخريطة دي عمدًا - هي نقطة البداية زي ما هي)
var legacyPageMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
    ["/dashboard.html"] = "/Home",
    ["/students.html"] = "/Students",
    ["/register_student.html"] = "/Register",
    ["/bulk-registration.html"] = "/Bulk",
    ["/student-status.html"] = "/StudentStatus",
    ["/requests.html"] = "/Requests",
    ["/housing-management.html"] = "/Housing",
    ["/reports.html"] = "/Reports",
    ["/auditlog.html"] = "/AuditLog",
    // شاشة المغادرة اتشالت بالكامل (محتواها كان مكرّرًا حرفيًا في /StudentStatus)
    // ومحدش كان بيستخدم روابطها، فمفيش تحويل - /Departure بترجع 404.
    ["/login.html"] = "/Account/Login",
    ["/login-v2.html"] = "/Account/Login",
    ["/login-lang.html"] = "/Account/Login",
    ["/sidebar.html"] = "/Home",

    // ⚠️ نسخ قديمة من صفحات بوابة الطالب كانت لسه متاحة على جذر الموقع
    //    (/register-form.html) وبتقدّم كودًا أقدم من النسخة الحيّة في /register/.
    //    الطالب اللي يفتح رابطًا قديمًا كان بيملأ نموذجًا مختلفًا عن اللي في البوابة.
    ["/register-form.html"] = "/register/register-form.html",
    ["/register-confirmation.html"] = "/register/register-confirmation.html",
    ["/track-request.html"] = "/register/track-request.html"
};
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // تفاصيل الطلب القديمة بتشيل الـ id من الكويري - ننقله للراوت الجديد
    if (path.Equals("/request-details.html", StringComparison.OrdinalIgnoreCase))
    {
        var id = context.Request.Query["id"].ToString();
        context.Response.Redirect(string.IsNullOrEmpty(id) ? "/Requests" : $"/Requests/Details/{id}");
        return;
    }

    if (legacyPageMap.TryGetValue(path, out var target))
    {
        context.Response.Redirect(target);
        return;
    }

    await next();
});

// 🔒 مرفقات الطلاب (هويات، مستندات نقل، قرارات فصل) بتتخزّن تحت wwwroot/uploads،
//    يعني UseStaticFiles كان بيقدّمها لأي حد معاه الرابط من غير تسجيل دخول.
//    الوصول المشروع كله بيعدّي من كنترولرات بتتحقق من الصلاحية:
//      /api/supervisor/housing-transfer/{id}/attachment
//      /api/student-status/{actionId}/attachment
//    فالمسار المباشر مقفول هنا. 404 مش 403 - عشان ما نأكّدش وجود الملف أصلًا.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/uploads", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

var defaultFilesOptions = new DefaultFilesOptions();
defaultFilesOptions.DefaultFileNames.Clear();
defaultFilesOptions.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path;
        // "no-store" كانت بتمنع المتصفح من تخزين أي ملف - يعني site.css وكل ملفات
        // الـ JS بتتحمّل من الأول مع كل تنقّل بين الصفحات (request-details-page.js
        // لوحده 47 كيلوبايت). ده كان السبب الظاهر لإحساس "الصفحة بتعمل load".
        //
        // "no-cache" لوحدها بتخلّي المتصفح يخزّن الملف *و* يسأل السيرفر كل مرة
        // بـ If-None-Match؛ لو الملف ما اتغيّرش السيرفر بيرد 304 من غير جسم
        // (~200 بايت بدل 47 كيلوبايت). يعني نفس أمان النشر بالظبط - مفيش نسخة
        // قديمة بتتقدّم بعد أي publish - بس من غير إعادة التحميل.
        if (path.HasValue && (path.Value.EndsWith(".html") || path.Value.EndsWith(".js") || path.Value.EndsWith(".css")))
        {
            // الـ .html فاضلة "no-store" زي ما كانت: دي كمان بتعطّل الـ bfcache، يعني
            // زر Back بعد تسجيل الخروج ما يقدرش يرجّع صفحة محمية من ذاكرة التنقّل.
            // مفيش مكسب أداء ضايع هنا - صفحات الموظفين MVC مش ملفات ثابتة أصلاً.
            var isHtml = path.Value.EndsWith(".html");
            ctx.Context.Response.Headers.Append("Cache-Control",
                isHtml ? "no-cache, no-store, must-revalidate" : "no-cache, must-revalidate");
            if (isHtml)
            {
                ctx.Context.Response.Headers.Append("Pragma", "no-cache");
                ctx.Context.Response.Headers.Append("Expires", "0");
            }
        }
        // الخطوط والصور مابتتغيّرش - بنخلّي المتصفح يخزّنها بدل ما يسألنا عنها كل مرة.
        // خطوط IBM Plex Arabic لوحدها ~٩٠ كيلوبايت، ولحد ما تتحمّل النص العربي بيبان
        // بخط بديل أو مايبانش خالص - وde شكله للمستخدم "الصفحة بتعمل load".
        // ملحوظة: Ctrl+F5 بيتخطّى الكاش دايمًا، فالقياس الصح يبقى بـ F5 عادية.
        if (path.HasValue)
        {
            var p2 = path.Value;
            var isFont = p2.EndsWith(".woff2") || p2.EndsWith(".woff") || p2.EndsWith(".ttf") || p2.EndsWith(".otf");
            var isImage = p2.EndsWith(".png") || p2.EndsWith(".jpg") || p2.EndsWith(".jpeg")
                       || p2.EndsWith(".gif") || p2.EndsWith(".svg") || p2.EndsWith(".ico");

            // الخطوط: ٣٠ يوم + immutable (عمرها ما بتتغيّر).
            // الصور: ٧ أيام - لو غيّرت الشعار هيتحدّث خلال أسبوع أو مع أول Ctrl+F5.
            if (isFont)
                ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=2592000, immutable");
            else if (isImage)
                ctx.Context.Response.Headers.Append("Cache-Control", "public, max-age=604800");
        }

        // Force UTF-8 charset for all static files so Arabic renders correctly in all browsers
        if (path.HasValue)
        {
            var contentType = ctx.Context.Response.Headers["Content-Type"].ToString();
            if (!string.IsNullOrEmpty(contentType) && !contentType.Contains("charset", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Context.Response.Headers["Content-Type"] = contentType + "; charset=utf-8";
            }
        }
    }
});

app.Run();

public class UtcDateTimeConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.GetDateTime().ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime());
    }
}
