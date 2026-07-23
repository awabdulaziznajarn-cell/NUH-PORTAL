using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
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

// ✅ Localization (.resx) — العربية هي اللغة المحايدة/الافتراضية + الإنجليزية
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ✅ Controllers with JSON options + توطين الـ Views و DataAnnotations
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new UtcDateTimeConverter());
        // gender يفضل "male"/"female" في الـ JSON رغم إنه بقى enum
        options.JsonSerializerOptions.Converters.Add(new NUH_PORTAL.Common.Json.GenderJsonConverter());
        // باقي الـ enums (أسماؤها snake_case مطابقة للنص) تتسلسل بالاسم — فالـ JSON يفضل زي ما هو
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ✅ الثقافات المدعومة — العربية افتراضيًا، واختيار المستخدم (كوكي) يتغلّب على لغة المتصفح
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("ar"), new CultureInfo("en") };
    options.DefaultRequestCulture = new RequestCulture("ar");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    // الترتيب الافتراضي للمزوّدات: QueryString ثم Cookie ثم Accept-Language — الكوكي بيتغلّب على المتصفح، وده المطلوب.
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


// ✅ Authentication — سكيم ذكي بيختار تلقائيًا: كوكي MVC (NUH.Auth) لو موجود، وإلا JWT (للـ API/الأدوات).
// بكده كل الـ [Authorize] — العادية واللي عليها Roles — بتتوثّق بالكوكي من صفحات الـ MVC حتى لو توكن الـ JWT
// (staffToken) قديم/منتهي. (الإصلاح القديم بالـ DefaultPolicy كان بيمسك [Authorize] العادية بس، مش اللي عليها Roles.)
// ✅ ASP.NET Identity (Core) — مخزن المستخدمين/الأدوار/الصلاحيات (زي الـ permit).
// المصادقة نفسها فاضلة على JWT/Cookie تحت؛ Identity بيوفّر UserManager/RoleManager + الهاشر.
builder.Services.AddIdentityCore<User>(opt =>
{
    opt.Password.RequireDigit = false;
    opt.Password.RequireNonAlphanumeric = false;
    opt.Password.RequireUppercase = false;
    opt.Password.RequiredLength = 6;
    opt.User.RequireUniqueEmail = false;
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
        options.ForwardDefaultSelector = context =>
            context.Request.Cookies.ContainsKey("NUH.Auth")
                ? Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme
                : JwtBearerDefaults.AuthenticationScheme;
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
    // ✅ Cookie auth لصفحات الـ MVC — جنب الـ JWT (الـ API فاضل زي ما هو على الـ JWT)
    .AddCookie(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme, options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/Login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        options.Cookie.Name = "NUH.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });

// ملاحظة: مبقناش محتاجين DefaultPolicy مخصّصة — السكيم الذكي "NUH_Smart" فوق بيوثّق
// كل الـ [Authorize] (العادية واللي عليها Roles) بالكوكي أو الـ JWT حسب الطلب.

// ✅ Authorization — policy لكل صلاحية (permission)، الكنترولر بيستخدم [Authorize(Policy = "users.manage")]
builder.Services.AddAuthorization(options =>
{
    foreach (var permission in ApplicationPermissions.All)
        options.AddPolicy(permission.Value, policy => policy.RequireClaim(ClaimConstants.Permission, permission.Value));
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

// ✅ Rate Limiting
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests = false;
    options.HttpStatusCode = 429;
    options.RealIpHeader = "X-Real-IP";
    options.GeneralRules = new List<RateLimitRule>
    {
        // تسجيل الدخول — مكافحة تخمين كلمة المرور
        new RateLimitRule { Endpoint = "POST:/api/Auth/Login", Period = "1m", Limit = 20 },
        // إرسال OTP — مكافحة سبام الرسائل وتعداد الأرقام (الخدمة كمان بتمنع طلب قبل مرور دقيقة)
        new RateLimitRule { Endpoint = "POST:/api/Otp/send", Period = "10m", Limit = 5 },
        // التحقق من OTP — مكافحة التخمين (الخدمة كمان بتقفل بعد 3 محاولات)
        new RateLimitRule { Endpoint = "POST:/api/Otp/verify", Period = "10m", Limit = 10 },
        // التتبع العام — مكافحة تعداد الطلبات بالأرقام المتسلسلة أو بالموبايل
        new RateLimitRule { Endpoint = "*:/api/RequestTracking/*", Period = "1m", Limit = 20 },
        // بدء التسجيل الذاتي — مكافحة السبام
        new RateLimitRule { Endpoint = "POST:/api/Registration/start", Period = "1h", Limit = 10 },
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
if (adConfig.RoleMappings == null || adConfig.RoleMappings.Count == 0)
{
    Console.WriteLine("WARNING: No ActiveDirectory RoleMappings configured. Using default mapping (all users get 'User' role).");
    adConfig.RoleMappings = new List<AdRoleMapping>
    {
        new AdRoleMapping { AdGroup = "Housing_Admin", ApplicationRole = "Admin" },
        new AdRoleMapping { AdGroup = "Housing_Supervisor", ApplicationRole = "Supervisor" }
    };
}
builder.Services.Configure<ActiveDirectoryConfig>(builder.Configuration.GetSection("ActiveDirectory"));
builder.Services.Configure<ADServiceAccountConfig>(builder.Configuration.GetSection("ADServiceAccount"));
builder.Services.AddSingleton<ActiveDirectoryService>();
builder.Services.AddScoped<ADProvisioningService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<SmsService>();
builder.Services.AddScoped<IWorkflowService, WorkflowService>();
builder.Services.AddScoped<IRegistrationService, RegistrationService>();

// ✅ Layered architecture (Repository + UnitOfWork + Mapster) — نمط permits
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
builder.Services.AddScoped<ILookupAdminService, LookupAdminService>();
builder.Services.AddScoped<ILookupResolver, LookupResolver>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IRoleAdminService, RoleAdminService>();
builder.Services.AddScoped<IHousingAccountService, HousingAccountService>();
builder.Services.AddScoped<ISupervisorHousingTransferService, SupervisorHousingTransferService>();
builder.Services.AddScoped<IAttachmentService, AttachmentService>();
builder.Services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
builder.Services.AddScoped<ILogQueryService, LogQueryService>();
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

// ✅ Forwarded Headers — مطلوب عشان يكون الشغل ورا IIS ARR
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownProxies.Add(System.Net.IPAddress.Loopback);
});

var app = builder.Build();

// ✅ Forwarded Headers — لازم يكون أول Middleware
app.UseForwardedHeaders();

// ✅ معالجة الأخطاء المركزية (UserFriendlyException → JSON نظيف، وأي خطأ تاني → 500 من غير تسريب تفاصيل)
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ملاحظة: مخطط قاعدة البيانات يُدار عبر EF Migrations (مجلد Migrations + سكربتات deployment/migrations).
// التعديلات اللي كانت بتتنفّذ هنا وقت التشغيل (drop CHECK / add bulk_request_id / varchar→nvarchar)
// أصبحت متضمّنة في الـ migrations واتشالت. لتهيئة قاعدة بيانات جديدة استخدم: dotnet ef database update

// ✅ Seed dev users (Development فقط) — للدخول عبر local fallback من غير AD
// بينشئ: admin / cyber / supervisor / user — كلهم بالباسورد Test@123
if (app.Environment.IsDevelopment())
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    var seedUserManager = seedScope.ServiceProvider.GetRequiredService<UserManager<User>>();
    var seedRoleManager = seedScope.ServiceProvider.GetRequiredService<RoleManager<Role>>();
    await DbSeeder.SeedDevUsersAsync(seedUserManager, seedRoleManager);
    DbSeeder.SeedDevData(seedDb);
}

// ✅ زرع القوائم المرجعية (lookups) — في كل البيئات لأنها بيانات أساسية.
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
    app.Logger.LogWarning(ex, "Lookup seeding skipped — run 'dotnet ef database update' first (lookup tables may not exist yet).");
}

// ✅ Security headers — حماية أساسية على مستوى كل الردود
app.Use(async (context, next) =>
{
    var h = context.Response.Headers;
    h["X-Content-Type-Options"] = "nosniff";                  // منع المتصفح من تخمين نوع المحتوى
    h["X-Frame-Options"] = "SAMEORIGIN";                      // منع تضمين الصفحات في إطارات خارجية (clickjacking)
    h["Referrer-Policy"] = "strict-origin-when-cross-origin";
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

// ✅ HttpsRedirection — يتفعّل فقط لو التطبيق مش ورا Proxy بيعمل TLS termination
var httpsRedirectEnabled = builder.Configuration.GetValue<bool?>("HttpsRedirect:Enabled");
if (httpsRedirectEnabled == true)
{
    app.UseHttpsRedirection();
}

// ✅ Rate Limiting قبل كل حاجة
app.UseIpRateLimiting();

app.UseCors("AllowFrontend");

// ✅ توطين الطلب — يحدد ثقافة الطلب من الكوكي قبل ترندرة أي صفحة MVC (لازم قبل الـ endpoints)
app.UseRequestLocalization();

// ✅ الترتيب مهم
app.UseAuthentication();

// ✅ Session activity middleware — blocks stale API calls after 15 min inactivity
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") &&
        context.User.Identity?.IsAuthenticated == true &&
        !context.Request.Path.StartsWithSegments("/api/Auth/Ping"))
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (userId != null)
        {
            var cache = context.RequestServices.GetRequiredService<IMemoryCache>();
            var cacheKey = "activity_" + userId;
            // لو فيه نشاط سابق مسجّل وعدّى عليه 15 دقيقة → اقفل الجلسة
            if (cache.TryGetValue(cacheKey, out DateTime lastActivity) &&
                (DateTime.UtcNow - lastActivity).TotalMinutes > 15)
            {
                // انتهت الجلسة بعدم النشاط — نسجّل خروج كوكي الـ MVC كمان عشان صفحة الدخول
                // متردّش المستخدم على الصفحة تاني (كسر لوب التحويل)، ونرجّع 401 للنداء الحالي.
                await context.SignOutAsync(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme);
                context.Response.StatusCode = 401;
                return;
            }
            // حدّث آخر نشاط على كل طلب مُصادَق عليه (نافذة منزلقة) — مش معتمد على /Ping بس
            cache.Set(cacheKey, DateTime.UtcNow, TimeSpan.FromHours(8));
        }
    }
    await next();
});

app.UseAuthorization();

app.MapControllers();

// ✅ تحويلات الصفحات القديمة → صفحات الـ MVC الجديدة
// الملفات القديمة لسه موجودة على الديسك كباك أب، بس أي رابط ليها بيترمي على الجديد —
// كده السايدبار والشكل موحدين في كل الشاشات مهما كان مصدر الرابط (هيستوري/بوكمارك/لينك جوه صفحة قديمة).
// (index.html بوابة الدخول بره الخريطة دي عمدًا — هي نقطة البداية زي ما هي)
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
    ["/supervisor-departure.html"] = "/Departure",
    ["/login.html"] = "/Account/Login",
    ["/login-v2.html"] = "/Account/Login",
    ["/login-lang.html"] = "/Account/Login",
    ["/sidebar.html"] = "/Home"
};
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;

    // تفاصيل الطلب القديمة بتشيل الـ id من الكويري — ننقله للراوت الجديد
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

var defaultFilesOptions = new DefaultFilesOptions();
defaultFilesOptions.DefaultFileNames.Clear();
defaultFilesOptions.DefaultFileNames.Add("index.html");
app.UseDefaultFiles(defaultFilesOptions);
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path;
        if (path.HasValue && (path.Value.EndsWith(".html") || path.Value.EndsWith(".js") || path.Value.EndsWith(".css")))
        {
            ctx.Context.Response.Headers.Append("Cache-Control", "no-cache, no-store, must-revalidate");
            ctx.Context.Response.Headers.Append("Pragma", "no-cache");
            ctx.Context.Response.Headers.Append("Expires", "0");
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
