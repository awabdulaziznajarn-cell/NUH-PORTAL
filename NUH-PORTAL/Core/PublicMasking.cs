namespace NUH_PORTAL.Core
{
    // ========================================================================
    //  إخفاء جزئي للبيانات في الصفحات العامة - قاعدة واحدة لكل الصفحات.
    //
    //  ⚠️ ليه ملف مستقلّ: النظام فيه أكتر من صفحة مفتوحة بلا تسجيل دخول
    //     (تتبّع الطلب، والتحقّق من وثيقة التعهّد). لو كل صفحة أخفت بطريقتها،
    //     الصفحة الأرخم بتفضح اللي الصفحة التانية بتخفيه - والإخفاء بيبقى
    //     بلا معنى، لأن اللي عايز البيانات بياخدها من أضعف باب.
    //
    //  ⚠️ والقاعدة نفسها اتقرّرت في شاشة تتبّع الطلب وليها سبب:
    //     الاسم الأول بيظهر كامل - شائع جدًا فمابيميّزش شخصًا بعينه، لكنه
    //     بيكفي صاحب الورقة إنه يطمّن إن دي ورقته. اسم الأب والجد والعائلة
    //     هي اللي بتربط الرقم بالهوية، وبتفضل مخفية.
    // ========================================================================
    public static class PublicMasking
    {
        public static string? Name(string? name)
        {
            if (string.IsNullOrWhiteSpace(name)) return name;

            var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return string.Join(' ', parts.Select((p, i) =>
                i == 0 || p.Length <= 1 ? p : p[0] + new string('*', Math.Min(p.Length - 1, 4))));
        }

        // ⚠️ آخر ٤ خانات لا أول ٤: أوائل الرقم الجامعي بتقول سنة القبول
        //    والكلية، وهي مشتركة بين آلاف الطلاب - يعني بتضيّق البحث بدل ما
        //    تأكّد الهوية. الأربعة الأخيرة عشوائية عمليًّا، فبتأكّد للي ماسك
        //    الورقة إنها ورقته من غير ما تدّي الرقم لحد.
        public static string? IdTail(string? value, int keep = 4)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            var digits = value.Trim();
            if (digits.Length <= keep) return digits;
            return new string('•', digits.Length - keep) + digits[^keep..];
        }
    }
}
