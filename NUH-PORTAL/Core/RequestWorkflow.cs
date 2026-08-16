using System.Text.Json;

namespace NUH_PORTAL.Core
{
    // ============================================================================
    //  جدول انتقالات مسار الطلب — التعريف الوحيد في النظام.
    //
    //  ⚠️ كان مكتوبًا في ثلاثة مواضع: RequestService.ReviewAsync (الخادم)، ودالة
    //     actionsFor في شاشة قائمة الطلبات، وشاشة تفاصيل الطلب (نسختان داخل
    //     الملف الواحد: واحدة لبناء الأزرار وأخرى لإرسال القرار).
    //
    //     وتفارقت فعلًا: الحالة «submitted» كانت مفقودة من سلسلة الشروط في شاشة
    //     التفاصيل، فيبقى اسم الزر بلا قيمة ويُطبع في الشاشة نصًّا حرفيًّا
    //     «undefined»، ويسقط إرسال القرار في فرع فارغ فلا يحدث شيء عند الضغط —
    //     بينما نفس الطلب يُعتمد بنجاح من شاشة القائمة.
    //
    //  ⚠️ والنظام فيه مجموعتا مصطلحات لنفس المراحل، لأن للطلب مسارين:
    //
    //       مسار تسجيل الطالب  →  pending_supervisor / pending_cyber / rejected
    //                             ويمرّ على /api/Workflow/{id}/{approve|reject}
    //       مسار طلب الموظف    →  submitted / cyber_review / housing_rejected
    //                             ويمرّ على /api/Requests/{id}/review
    //
    //     المجموعة الأولى كانت خارج هذا الجدول تمامًا، فطلب الطالب في
    //     «بانتظار الأمن السيبراني» يظهر في تبويب «يحتاج إجراءك» ويُحسب في شارة
    //     القائمة الجانبية، ثم يظهر في الصف بلا زر إجراء وبلا تمييز لوني — يعني
    //     المراجع يرى أن عليه طلبًا ولا يجد في الشاشة ما يفعله به.
    //     المجموعتان الآن في هذا الجدول، ولكلٍّ مسار الـ API الخاص به في Api.
    // ============================================================================
    public static class RequestWorkflow
    {
        // مسار الـ API الذي يُرسَل إليه القرار في هذه المرحلة.
        public static class Apis
        {
            // PUT /api/Requests/{id}/review  ← الحالة الهدف في جسم الطلب
            public const string Review = "review";

            // POST /api/Workflow/{id}/{approve|reject|request-info}  ← المرحلة تحدّد الإجراء
            public const string Workflow = "workflow";
        }

        public class Transition
        {
            // حالة الطلب الحالية
            public string Status { get; set; } = "";

            // الصلاحيات التي تسمح بالتصرّف في هذه المرحلة (واحدة منها تكفي)
            public string[] Permissions { get; set; } = Array.Empty<string>();

            // الحالة الجديدة عند الاعتماد، ومفتاح نص الزر في SharedResource
            public string ApproveTo { get; set; } = "";
            public string ApproveKey { get; set; } = "";

            // الحالة الجديدة عند الرفض — فارغة يعني لا يوجد رفض في هذه المرحلة
            public string? RejectTo { get; set; }
            public string? RejectKey { get; set; }

            // أي مسار API يُرسَل إليه القرار — انظر Apis أعلاه
            public string Api { get; set; } = Apis.Review;

            // هل تقبل هذه المرحلة «طلب معلومات إضافية» من الطالب؟
            public bool AllowMoreInfo { get; set; }

            // المرحلة المكافئة لها في المسار الآخر — تُستعمل للعدّادات والتبويبات
            // حتى لا ينقسم عدّاد المرحلة الواحدة على مسمّيين.
            public string? SameStageAs { get; set; }
        }

        public static readonly List<Transition> Transitions = new()
        {
            // ---------- مسار تسجيل الطالب (/api/Workflow) ----------
            new Transition
            {
                Status = "pending_supervisor",
                Permissions = new[] { "requests.reviewHousing" },
                ApproveTo = "pending_cyber", ApproveKey = "req_act_housing_approved",
                RejectTo = "rejected",       RejectKey = "req_act_housing_rejected",
                Api = Apis.Workflow, AllowMoreInfo = true, SameStageAs = "submitted"
            },
            new Transition
            {
                Status = "pending_cyber",
                Permissions = new[] { "requests.reviewCyber" },
                ApproveTo = "ready_for_provisioning", ApproveKey = "req_act_cyber_approved",
                RejectTo = "rejected",                RejectKey = "req_act_cyber_rejected",
                Api = Apis.Workflow, SameStageAs = "cyber_review"
            },
            new Transition
            {
                Status = "ready_for_provisioning",
                Permissions = new[] { "requests.complete" },
                ApproveTo = "completed", ApproveKey = "req_act_completed",
                RejectTo = "rejected",   RejectKey = "req_act_rejected",
                Api = Apis.Workflow
            },

            // ---------- مسار طلب الموظف (/api/Requests/{id}/review) ----------
            new Transition
            {
                Status = "submitted",
                Permissions = new[] { "requests.reviewHousing" },
                ApproveTo = "housing_approved", ApproveKey = "req_act_housing_approved",
                RejectTo = "housing_rejected",  RejectKey = "req_act_housing_rejected",
                Api = Apis.Review
            },
            new Transition
            {
                Status = "cyber_review",
                Permissions = new[] { "requests.reviewCyber" },
                ApproveTo = "cyber_approved", ApproveKey = "req_act_cyber_approved",
                RejectTo = "cyber_rejected",  RejectKey = "req_act_cyber_rejected",
                Api = Apis.Review
            },

            // ⚠️ انتقالات قديمة: الطلبات التي توقّفت في المرحلتين الوسيطتين قبل
            //    التوحيد يجب أن تبقى قابلة للتحريك، وإلا بقيت عالقة للأبد.
            new Transition
            {
                Status = "housing_approved",
                Permissions = new[] { "requests.complete" },
                ApproveTo = "cyber_review", ApproveKey = "req_act_cyber_review",
                Api = Apis.Review
            },
            new Transition
            {
                Status = "cyber_approved",
                Permissions = new[] { "requests.reviewCyber", "requests.complete" },
                ApproveTo = "ready_for_provisioning", ApproveKey = "req_act_ready_for_provisioning",
                Api = Apis.Review
            }
        };

        // ====================================================================
        //  المراحل والصلاحيات — تُشتقّ من الجدول أعلاه، ولا تُكتب مرة ثانية.
        //
        //  ⚠️ كانت هنا قاموسًا مستقلًّا (PermissionStages) بجانب الجدول، وكان
        //     ذلك مصدرين في ملف واحد: القاموس يعرف pending_supervisor و
        //     pending_cyber والجدول لا يعرفهما، فيُحسب الطلب في العدّاد ويظهر
        //     في التبويب بلا زر. الاشتقاق يمنع تكرار الخطأ من أصله.
        // ====================================================================
        public static string[] StagesFor(bool canHousing, bool canCyber, bool canComplete)
        {
            bool Has(string p) =>
                (p == "requests.reviewHousing" && canHousing) ||
                (p == "requests.reviewCyber" && canCyber) ||
                (p == "requests.complete" && canComplete);

            return Transitions
                .Where(t => t.Permissions.Any(Has))
                .Select(t => t.Status)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        // كل الصلاحيات التي تخوّل التصرّف في هذه المرحلة. فارغة = مرحلة مقفولة.
        public static string[] PermissionsForStage(string? status)
            => Find(status)?.Permissions ?? Array.Empty<string>();

        // الصلاحية الأولى — للاستعمالات التي تحتاج واحدة فقط.
        public static string? PermissionForStage(string? status)
            => PermissionsForStage(status).FirstOrDefault();

        // ====================================================================
        //  المرحلة الواحدة باسمين: مسمّى مسار الطالب ومسمّى مسار الموظف.
        //  العدّادات والتبويبات تتعامل معهما كمرحلة واحدة، وإلا ظهر العدّاد
        //  صفرًا بينما الطلب واقف فعلًا في تلك المرحلة تحت الاسم الآخر.
        // ====================================================================
        private static readonly Dictionary<string, string[]> ExtraAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // تبويب «مرفوض» يجمع الرفض من المسارين معًا
            ["rejected"] = new[] { "rejected", "housing_rejected", "cyber_rejected" }
        };

        // كل الحالات التي يشملها هذا التبويب/العدّاد.
        public static string[] Aliases(string status)
        {
            if (string.IsNullOrWhiteSpace(status)) return Array.Empty<string>();

            if (ExtraAliases.TryGetValue(status, out var fixedList)) return fixedList;

            var list = new List<string> { status };
            foreach (var t in Transitions)
            {
                if (t.SameStageAs != null
                    && string.Equals(t.SameStageAs, status, StringComparison.OrdinalIgnoreCase))
                    list.Add(t.Status);
            }
            return list.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        public static Transition? Find(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return null;
            foreach (var t in Transitions)
            {
                if (string.Equals(t.Status, status, StringComparison.OrdinalIgnoreCase))
                    return t;
            }
            return null;
        }

        // هل الانتقال (الحالة الحالية → الحالة الجديدة) مسموح لصاحب هذه الصلاحيات؟
        // ⚠️ هذا هو الفحص الحقيقي على الخادم لمسار /api/Requests/{id}/review. ما
        //    تعرضه الواجهة تسهيل للمستخدم لا حماية: كل قرار يمرّ من هنا مهما
        //    أرسلت الواجهة.
        // ⚠️ ومراحل مسار /api/Workflow مرفوضة هنا عمدًا: اعتمادها يمرّ على
        //    RegistrationService الذي يكتب سجل المراحل ويُنشئ الإشعار للمكتب
        //    التالي. لو قبلناها هنا، لَمرّ الطلب بلا سجل وبلا إشعار.
        public static bool IsAllowed(string? current, string? next, bool canHousing, bool canCyber, bool canComplete)
        {
            var t = Find(current);
            if (t == null || t.Api != Apis.Review || string.IsNullOrWhiteSpace(next)) return false;

            var matchesTarget =
                string.Equals(t.ApproveTo, next, StringComparison.OrdinalIgnoreCase) ||
                (t.RejectTo != null && string.Equals(t.RejectTo, next, StringComparison.OrdinalIgnoreCase));

            if (!matchesTarget) return false;

            foreach (var p in t.Permissions)
            {
                if (p == "requests.reviewHousing" && canHousing) return true;
                if (p == "requests.reviewCyber" && canCyber) return true;
                if (p == "requests.complete" && canComplete) return true;
            }
            return false;
        }

        // نفس الجدول بصيغة JSON لتقرأه الواجهة — يُبنى مرة واحدة عند أول طلب.
        private static string? _json;
        public static string ToJson()
        {
            return _json ??= JsonSerializer.Serialize(Transitions, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        }
    }
}
