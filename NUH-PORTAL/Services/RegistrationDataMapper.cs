using System.Text.Json;
using NUH_PORTAL.Models;

namespace NUH_PORTAL.Services
{
    // ========================================================================
    //  المصدر الوحيد لتحويل «بيانات التسجيل» (JSON) إلى سجل الطالب.
    //
    //  قاعدة المعمار المتفق عليها:
    //    • جدول Students هو مصدر الحقيقة الوحيد لبيانات الطالب. كل شاشة وكل
    //      عملية (المراجعة، النقل، تغيير الحالة، إنشاء حساب الشبكة) تقرأ منه.
    //    • Requests.registration_data ليس بياناتٍ حيّة، بل *أرشيف تقديم*:
    //      صورة ممّا أقرّ به الطالب في لحظة التقديم. يُكتب ولا يُقرأ كقيمة
    //      حالية أبدًا — قيمته في الإثبات والمقارنة فقط.
    //
    //  ولأن مسار التقديم الأول ومسار إعادة التقديم كانا يحملان نسختين منفصلتين
    //  من نفس التحويل، كان أي حقل جديد يُضاف في أحدهما ويُنسى في الآخر. التحويل
    //  كله صار هنا في مكان واحد: أي حقل جديد يُضاف إلى TrackedFields و Apply
    //  فيعمل في المسارين معًا، ويظهر تلقائيًا في ملخّص التعديلات.
    // ========================================================================
    public static class RegistrationDataMapper
    {
        // الحقول التي يملك الطالب تعديلها، وأسماؤها كما تظهر للمراجع.
        // الرقم الجامعي غير مدرج عن قصد: هو مفتاح هوية الطلب، وتغييره من شاشة
        // الطالب يعني أن الطلب صار لشخص آخر. تصحيحه يكون برفض الطلب وإعادة تقديمه.
        public static readonly (string Key, string Label)[] TrackedFields =
        {
            ("full_name",         "الاسم بالعربية"),
            ("full_name_english", "الاسم بالإنجليزية"),
            ("national_id",       "رقم الهوية"),
            ("mobile",            "رقم الجوال"),
            ("phone",             "رقم الجوال"),
            ("gender",            "الجنس"),
            ("college",           "الكلية"),
            ("department",        "القسم"),
            ("academic_level",    "المستوى الدراسي"),
            ("housing_building",  "رقم المبنى"),
            ("floor_number",      "الدور"),
            ("apartment_number",  "رقم الشقة"),
            ("room_number",       "رقم الغرفة")
        };

        // ⚠️ القائمة اللي المراجع بيعلّم منها على الخانات المطلوب تصحيحها.
        //    مشتقّة من TrackedFields فوق ومترتّبة زي ترتيب نموذج الطالب،
        //    ومن غير التكرار: mobile و phone نفس الخانة باسمين، ولو ظهروا
        //    الاتنين في القائمة المراجع هيشوف «رقم الجوال» مرتين ومايعرفش
        //    الفرق. الترتيب هنا مقصود عشان القائمة تقرا زي الفورم.
        //  Pickable = هل المراجع يقدر يطلب من الطالب تصحيح الخانة دي؟
        //
        //  ⚠️ رقم الجوال Pickable = false عن قصد. الرقم ده هو اللي وصله رمز
        //     التحقق في الخطوة الأولى، وخانته مقفولة في نموذج الطالب لهذا
        //     السبب من قبل ميزة تحديد الخانات أصلًا. فلو المراجع علّم عليه،
        //     الطالب بيلاقي مطلوب منه تصحيح خانة ما يقدرش يفتحها فيقف.
        //     تغيير الجوال بيتم بالرجوع لخطوة التحقق بالرمز لا من هنا.
        //
        //  ⚠️ الخانة فاضلة في القائمة رغم إنها مش قابلة للاختيار، عشان LabelOf
        //     تفضل ترجّع «رقم الجوال» في ملخّص التعديلات وسجل المسار. شيلها
        //     من القائمة كان هيخلّي الملخّص يكتب "phone" للمراجع.
        public static readonly (string Key, string Label, bool Pickable)[] EditableFields =
        {
            // ⚠️ الرقم الجامعي Pickable = false: مش خانة عادية، ده هوية الطلب
            //    نفسه. تغييره في إعادة التقديم بيحوّل الطلب لطالب تاني بعد ما
            //    المراجع راجعه. مذكور هنا عشان LabelOf ترجّع اسمه بالعربي في
            //    رسالة الرفض وملخّص التعديلات بدل "student_id".
            ("student_id",        "الرقم الجامعي",     false),
            ("full_name",         "الاسم بالعربية",    true),
            ("full_name_english", "الاسم بالإنجليزية", true),
            ("national_id",       "رقم الهوية",        true),
            ("phone",             "رقم الجوال",        false),
            ("gender",            "الجنس",             true),
            ("college",           "الكلية",            true),
            ("department",        "القسم",             true),
            ("academic_level",    "المستوى الدراسي",   true),
            ("housing_building",  "رقم المبنى",        true),
            ("floor_number",      "الدور",             true),
            ("apartment_number",  "رقم الشقة",         true),
            ("room_number",       "رقم الغرفة",        true)
        };

        // mobile و phone نفس الخانة — التطبيع ده بيمنع رفض تعديل مشروع
        // لمجرد إن المراجع علّم على اسم والفورم بيبعت الاسم التاني.
        public static string NormalizeFieldKey(string key)
            => string.Equals(key, "mobile", StringComparison.OrdinalIgnoreCase) ? "phone" : key;

        //  ⚠️ بترجّع true للخانات القابلة للاختيار بس. المستدعي الوحيد هو
        //     الفلتر اللي بيحدد إيه اللي يتخزّن في info_fields، فالخانة غير
        //     القابلة للاختيار لازم تتصدّ هنا كمان لا في الواجهة وحدها.
        public static bool IsEditableField(string key)
        {
            var k = NormalizeFieldKey(key);
            foreach (var f in EditableFields)
                if (string.Equals(f.Key, k, StringComparison.OrdinalIgnoreCase)) return f.Pickable;
            return false;
        }

        public static string LabelOf(string key)
        {
            var k = NormalizeFieldKey(key);
            foreach (var f in EditableFields)
                if (string.Equals(f.Key, k, StringComparison.OrdinalIgnoreCase)) return f.Label;
            return k;
        }

        // ------------------------------------------------------------------
        //  قراءة حقل واحد من الـ JSON
        // ------------------------------------------------------------------
        public static string? Read(string? json, string field)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                using var doc = JsonDocument.Parse(json);
                return ReadField(doc, field);
            }
            catch { return null; }
        }

        private static string? ReadField(JsonDocument doc, string name)
        {
            if (!doc.RootElement.TryGetProperty(name, out var p)) return null;
            return p.ValueKind switch
            {
                JsonValueKind.String => p.GetString()?.Trim(),
                JsonValueKind.Number => p.ToString(),
                _ => null
            };
        }

        // كائن مجهول النوع (يأتي من DTO التقديم الأول) → نص JSON
        public static string Serialize(object? data)
            => JsonSerializer.Serialize(data ?? new { });

        // ------------------------------------------------------------------
        //  مسار الكتابة الوحيد: من بيانات التسجيل إلى سجل الطالب
        //  الحقل الغائب من الحمولة لا يُمسح — يُترك كما هو.
        //  ملاحظة: حالة الطالب (active / left) ليست ضمن ما يكتبه هذا المسار؛
        //  الطالب لا يحدّد حالة سكنه، وهي تُدار من شاشة «تحديث حالة الطالب».
        // ------------------------------------------------------------------
        public static void Apply(Student student, string? json)
        {
            if (student == null || string.IsNullOrWhiteSpace(json)) return;

            JsonDocument doc;
            try { doc = JsonDocument.Parse(json); }
            catch { return; }

            using (doc)
            {
                void Set(string key, Action<string> apply)
                {
                    var v = ReadField(doc, key);
                    if (!string.IsNullOrWhiteSpace(v)) apply(v.Trim());
                }

                Set("full_name",         v => student.full_name = v);
                Set("full_name_english", v => student.full_name_english = v);
                Set("national_id",       v => student.national_id = v);
                Set("phone",             v => student.phone = v);
                Set("mobile",            v => student.phone = v);   // بعد phone: الحمولة الحالية ترسل mobile
                Set("gender",            v => student.gender = GenderHelper.Parse(v));
                Set("college",           v => student.college = v);
                Set("department",        v => student.department = v);
                Set("academic_level",    v => student.academic_level = v);
                Set("housing_building",  v => student.housing_building = v);
                Set("floor_number",      v => student.floor_number = v);
                Set("room_number",       v => student.room_number = v);
                Set("apartment_number",  v => student.apartment_number = v);
            }
        }

        // نتيجة مقارنة حقل واحد — تُخزَّن بصيغة JSON في WorkflowHistory.changes_json
        public sealed class FieldChange
        {
            public string Field { get; set; } = "";
            public string Label { get; set; } = "";
            public string? Old { get; set; }
            public string? New { get; set; }
        }

        // ------------------------------------------------------------------
        //  المقارنة الفعلية — مصدر واحد لكلٍّ من النص المقروء والـ JSON المنظّم.
        //  أي حقل يُضاف إلى TrackedFields يظهر في الاثنين تلقائيًا.
        // ------------------------------------------------------------------
        public static List<FieldChange> Compare(string? oldJson, string? newJson)
        {
            var changes = new List<FieldChange>();
            if (string.IsNullOrWhiteSpace(newJson)) return changes;

            var seenLabels = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                using var oldDoc = JsonDocument.Parse(string.IsNullOrWhiteSpace(oldJson) ? "{}" : oldJson);
                using var newDoc = JsonDocument.Parse(newJson);

                foreach (var (key, label) in TrackedFields)
                {
                    var oldVal = ReadField(oldDoc, key);
                    var newVal = ReadField(newDoc, key);
                    if (string.Equals(oldVal, newVal, StringComparison.Ordinal)) continue;

                    // mobile و phone حقل واحد في سجل الطالب — لا يُسجَّل التغيير مرتين
                    if (!seenLabels.Add(label)) continue;

                    changes.Add(new FieldChange { Field = key, Label = label, Old = oldVal, New = newVal });
                }
            }
            catch { return new List<FieldChange>(); }

            return changes;
        }

        public static string? SerializeChanges(List<FieldChange> changes)
            => changes.Count == 0 ? null : JsonSerializer.Serialize(changes);

        public static List<FieldChange> DeserializeChanges(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<FieldChange>();
            try { return JsonSerializer.Deserialize<List<FieldChange>>(json) ?? new List<FieldChange>(); }
            catch { return new List<FieldChange>(); }
        }

        // ------------------------------------------------------------------
        //  النص المقروء الذي يظهر في سجل المراجعات
        //  فتبقى محفوظة حتى لو أُعيد التعديل بعد ذلك.
        // ------------------------------------------------------------------
        public static string BuildChangeSummary(string? oldJson, string? newJson)
            => BuildChangeSummary(Compare(oldJson, newJson));

        public static string BuildChangeSummary(List<FieldChange> changes)
        {
            if (changes.Count == 0)
                return "إعادة تقديم بعد طلب معلومات - دون تعديل في البيانات";

            var lines = changes.Select(c => $"{c.Label}: من «{Show(c.Old)}» إلى «{Show(c.New)}»");
            var text = "التعديلات التي أجراها الطالب: " + string.Join("، ", lines);
            return text.Length > 1800 ? text[..1800] + "…" : text;
        }

        private static string Show(string? v) => string.IsNullOrWhiteSpace(v) ? "فارغ" : v.Trim();
    }
}
