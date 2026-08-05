using Microsoft.AspNetCore.Http;
using NUH_PORTAL.Services.Interfaces;
using System.Text;

namespace NUH_PORTAL.Services
{
    // تخزين مرفقات المستخدمين برّه مجلد النشر — انظر IAttachmentStorage للسبب.
    //
    //  الشكل على الديسك:
    //      {Root}\Students\{الرقم الجامعي}\Housing-Transfer\
    //      {Root}\Students\{الرقم الجامعي}\Status-Change\
    //      {Root}\Students\{الرقم الجامعي}\Request-Documents\
    //
    //  والملف نفسه:
    //      2026-08-03_1015__transfer-12__by-supervisor1__nu-logo.png
    //      └─ التاريخ والوقت ─┘  └─ السجل ─┘  └─ مين رفعه ─┘ └ الاسم الأصلي ┘
    //
    //  الطالب هو وحدة التنظيم مش نوع العملية، لأن ده السؤال اللي بيتسأل فعليًا:
    //  «هات كل مستندات الطالب فلان». والاسم بيحكي القصة من غير ما حد يفتح النظام.
    //
    //  ⚠️ الشكل القديم ({Root}\Housing-Transfer-Attachments\{id}\{guid}.ext) لسه
    //     مدعوم للقراءة — الصفوف القديمة بتفضل شغالة من غير أي ترحيل.
    public sealed class AttachmentStorage : IAttachmentStorage
    {
        // أنواع المرفقات = المجلد الفرعي تحت الطالب
        public const string HousingTransfer  = "Housing-Transfer";    // مرفقات نقل السكن
        public const string StatusChange     = "Status-Change";       // مرفقات تغيير حالة الطالب
        public const string RequestDocuments = "Request-Documents";   // مرفقات الطلبات (صور الهوية)

        // أسماء المجلدات قبل إعادة التنظيم — للقراءة فقط، مش بنكتب فيها تاني
        private static readonly Dictionary<string, string> LegacyFolders = new(StringComparer.OrdinalIgnoreCase)
        {
            [HousingTransfer]  = "Housing-Transfer-Attachments",
            [StatusChange]     = "Student-Status-Attachments",
            [RequestDocuments] = "Request-Attachments"
        };

        // بيتستخدم لو مفيش قيمة في الإعدادات — بيخلّي التطبيق يشتغل على أي جهاز
        // تطوير من غير ما يفترض وجود بارتيشن D:
        private const string FallbackFolderName = "App_Attachments";

        private const int MaxOriginalNameLength = 60;

        private readonly ILogger<AttachmentStorage> _logger;

        public string Root { get; }

        public AttachmentStorage(IConfiguration config, IWebHostEnvironment env, ILogger<AttachmentStorage> logger)
        {
            _logger = logger;

            var configured = config["Storage:AttachmentsRoot"];
            Root = string.IsNullOrWhiteSpace(configured)
                ? Path.Combine(env.ContentRootPath, FallbackFolderName)
                : configured.Trim();

            // ملحوظة: مش بننشئ المجلد هنا عن قصد. لو المسار غلط في الإعدادات،
            // أحسن إن أول رفع يفشل برسالة واضحة من إن التطبيق كله يرفض يقوم.
            _logger.LogInformation("Attachment storage root: {Root}", Root);
        }

        public async Task<string> SaveAsync(
            string category,
            string universityId,
            string recordKind,
            int recordId,
            string? uploadedBy,
            IFormFile file)
        {
            var dir = Path.Combine(Root, "Students", SafeSegment(universityId, "unknown-student"), category);
            Directory.CreateDirectory(dir);

            var baseName = BuildFileName(recordKind, recordId, uploadedBy, file.FileName);
            var ext = SafeExtension(file.FileName);

            // CreateNew بيرمي لو الملف موجود — بنزوّد رقم بدل ما نكتب فوق مرفق تاني
            // (ممكن يحصل لو اترفع ملفين بنفس الاسم في نفس الدقيقة لنفس السجل).
            for (var attempt = 0; ; attempt++)
            {
                var name = attempt == 0 ? baseName + ext : $"{baseName}({attempt}){ext}";
                var full = Path.Combine(dir, name);
                try
                {
                    await using var stream = new FileStream(full, FileMode.CreateNew);
                    await file.CopyToAsync(stream);
                    return Path.GetRelativePath(Root, full);
                }
                catch (IOException) when (File.Exists(full) && attempt < 50)
                {
                    // الاسم محجوز — جرّب اللي بعده
                }
            }
        }

        public string? ResolveExisting(string category, int recordId, string? storedPath)
        {
            if (string.IsNullOrWhiteSpace(storedPath))
                return null;

            var stored = storedPath.Trim();

            // الشكل الجديد: مسار نسبي فيه مجلدات
            if (stored.Contains('\\') || stored.Contains('/'))
            {
                var full = Path.GetFullPath(Path.Combine(Root, stored));

                // حماية: المسار الناتج لازم يفضل تحت الجذر مهما كانت القيمة المخزّنة
                var rootFull = Path.GetFullPath(Root).TrimEnd(Path.DirectorySeparatorChar);
                if (!full.StartsWith(rootFull + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Rejected attachment path outside storage root: {Stored}", stored);
                    return null;
                }

                return File.Exists(full) ? full : null;
            }

            // الشكل القديم: اسم ملف بس، والمجلد بيتحسب من رقم السجل
            if (!LegacyFolders.TryGetValue(category, out var legacyFolder))
                return null;

            var legacy = Path.Combine(Root, legacyFolder, recordId.ToString(), Path.GetFileName(stored));
            return File.Exists(legacy) ? legacy : null;
        }

        // 2026-08-03_1015__transfer-12__by-supervisor1__nu-logo
        private static string BuildFileName(string recordKind, int recordId, string? uploadedBy, string? originalName)
        {
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HHmm");
            var by = SafeSegment(uploadedBy, "unknown");
            var original = SafeSegment(Path.GetFileNameWithoutExtension(originalName), "file");

            if (original.Length > MaxOriginalNameLength)
                original = original[..MaxOriginalNameLength];

            return $"{stamp}__{recordKind}-{recordId}__by-{by}__{original}";
        }

        private static string SafeExtension(string? originalName)
        {
            var ext = Path.GetExtension(originalName ?? "");
            if (string.IsNullOrWhiteSpace(ext)) return "";
            var cleaned = new string(ext.Where(c => !Path.GetInvalidFileNameChars().Contains(c)).ToArray());
            return cleaned.Length > 10 ? cleaned[..10] : cleaned;
        }

        // بيشيل أي حرف ممنوع في أسماء الملفات/المجلدات ويستبدل المسافات بشرطة.
        // العربي بيفضل زي ما هو — ويندوز بيتعامل معاه عادي.
        private static string SafeSegment(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value)) return fallback;

            var invalid = Path.GetInvalidFileNameChars();
            var sb = new StringBuilder(value.Length);
            foreach (var c in value.Trim())
            {
                if (invalid.Contains(c)) continue;
                sb.Append(char.IsWhiteSpace(c) ? '-' : c);
            }

            var result = sb.ToString().Trim('-', '.', ' ');
            return string.IsNullOrWhiteSpace(result) ? fallback : result;
        }
    }
}
