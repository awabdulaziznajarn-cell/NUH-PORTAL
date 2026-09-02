using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NUH_PORTAL.Data;
using NUH_PORTAL.Models;
using NUH_PORTAL.Models.Enums;

namespace NUH_PORTAL.Services
{
    // ============================================================================
    //  أماكن حسابات الطلاب في الأكتف دايركتوري - التعريف الوحيد في النظام.
    //
    //  ⚠️ كانت القاعدة مكتوبة مرتين: مرة في ADProvisioningService (اللي بينشئ
    //     الحسابات فعلًا) ومرة في ADSetupService (أدوات التشخيص). واتفارقوا:
    //     الأولى اتصلّحت وبقت تبني المسار من الدومين المضبوط في الإعدادات،
    //     والتانية فضلت مكتوب فيها بالإيد مسار دومين بيئة قديمة مالوش وجود.
    //
    //     والنتيجة مش تجميلية: أداة «فحص الجاهزية» كانت بتدوّر على المجلدات
    //     والمجموعات في دومين تاني خالص، فترجع «غير موجودة» - يعني بتقول إن
    //     الأكتف دايركتوري بايظ وهو سليم تمامًا. وأسوأ أداة تشخيص هي اللي
    //     بتكدب عليك في اتجاه الخطأ.
    //
    //  ⚠️ ومحدش بيكتب دومين في الكود بعد كده. المصدر:
    //       الدومين      ← ActiveDirectoryConfig.Domain (الإعدادات)
    //       مسار المجلد  ← جدول ADConfigurations، والاحتياطي مبني من الدومين
    //       المجموعة     ← جدول ADConfigurations، والاحتياطي مبني من الدومين
    // ============================================================================
    public class AdDirectoryLayout
    {
        private readonly AppDbContext _db;
        private readonly ActiveDirectoryConfig _config;
        private readonly ILogger<AdDirectoryLayout> _logger;

        public AdDirectoryLayout(AppDbContext db, IOptions<ActiveDirectoryConfig> config, ILogger<AdDirectoryLayout> logger)
        {
            _db = db;
            _config = config.Value;
            _logger = logger;
        }

        public string Domain => _config.Domain ?? "";

        // nuh.edu.sa → DC=nuh,DC=edu,DC=sa
        public string BaseDn() =>
            string.Join(",", Domain.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(p => $"DC={p}"));

        // اسم الدخول الكامل - نفس دومين الإعدادات، مش دومين مكتوب في الكود.
        public string UpnFor(string samAccountName) => $"{samAccountName}@{Domain}";

        // اسم حساب الطالب من رقمه الجامعي - قاعدة واحدة للإنشاء وللتشخيص.
        // ====================================================================
        //  كلمة مرور مؤقّتة لحساب جديد في الدليل.
        //
        //  ⚠️ عشوائية دايمًا. أداة التشخيص كانت بتستعمل "NUH@{الرقم الجامعي}"،
        //     والرقم الجامعي مطبوع على كل ورقة في النظام - يعني كلمة مرور
        //     الحساب معروفة سلفًا لأي حد شاف الرقم. ومسار الإنشاء الحقيقي كان
        //     بيولّدها عشوائية، فالأداة اللي المفروض «تجرّب نفس المسار» كانت
        //     بتجرّب مسارًا أضعف - وهي أخطر حالة في أداة تشخيص: بتطمّنك على
        //     سلوك مش هو اللي بيحصل فعلًا.
        //
        //  ⚠️ والقاعدة هنا لا في كل خدمة: الإنشاء والتشخيص لازم يولّدوا بنفس
        //     الطريقة، وإلا فحص سياسة كلمات المرور في الأداة بيقول «مقبولة»
        //     على شكل مش هو اللي هيتكتب.
        //
        //  اللاحقة بتضمن شروط التعقيد (حرف كبير ورقم ورمز) مهما طلع الترميز.
        // ====================================================================
        public static string NewTempPassword() =>
            Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16)) + "!x1";

        public static string SamAccountNameFor(string? studentId)
        {
            var id = (studentId ?? "").Trim();
            return id.StartsWith("h", StringComparison.OrdinalIgnoreCase) ? id : "h" + id;
        }

        // ====================================================================
        //  مجلد الطالب حسب القسم.
        //
        //  ⚠️ المقارنة بتتجاهل حالة الحروف: المسار في الأكتف دايركتوري متكتب
        //     OU=NEW بحروف كبيرة، والفحص الحرفي كان بينتج مسارًا فيه OU=New
        //     مكررة (OU=Male,OU=New,OU=NEW,OU=STUDENTS,...) وده مسار مش موجود.
        //     أسماء الـ DN في LDAP مش حسّاسة لحالة الحروف أصلًا.
        // ====================================================================
        public async Task<string> StudentOuAsync(Gender? gender)
        {
            var config = await _db.ADConfigurations
                .FirstOrDefaultAsync(c => c.ConfigKey == ADConfigurationKeys.StudentOuPath);

            var baseOu = config?.ConfigValue;
            if (string.IsNullOrWhiteSpace(baseOu))
            {
                baseOu = $"OU=New,OU=Students,{BaseDn()}";
                _logger.LogWarning("ADConfigurations['{Key}'] غير مضبوط - استخدام المسار الافتراضي {Ou}",
                    ADConfigurationKeys.StudentOuPath, baseOu);
            }

            var genderOu = gender == Gender.Female ? "Female" : "Male";

            return baseOu.Contains("OU=New", StringComparison.OrdinalIgnoreCase)
                ? $"OU={genderOu},{baseOu}"
                : $"OU={genderOu},OU=New,{baseOu}";
        }

        // مجموعة الطالب حسب القسم - من الإعدادات، والاحتياطي مبني من الدومين.
        public async Task<string> StudentGroupAsync(Gender? gender)
        {
            var isMale = gender != Gender.Female;
            var key = isMale ? "male_group_dn" : "female_group_dn";

            var configured = await _db.ADConfigurations
                .Where(c => c.ConfigKey == key)
                .Select(c => c.ConfigValue)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(configured))
                return configured;

            var fallback = isMale
                ? $"CN=NUH-Student-B,OU=Groups,{BaseDn()}"
                : $"CN=NUH-Student-G,OU=Groups,{BaseDn()}";

            _logger.LogWarning("مجموعة الطلاب ({Gender}) غير مضبوطة في ADConfigurations - استخدام {Group}",
                isMale ? "male" : "female", fallback);

            return fallback;
        }
    }
}
