// اسم الـ Assembly هو "NUH-PORTAL" (بشرطة) لأن اسم ملف المشروع كده، بينما RootNamespace هو "NUH_PORTAL" (بسفلية).
// مكتبة التوطين بتبني اسم مورد الـ .resx من اسم الـ Assembly افتراضيًا، فبتدوّر على اسم غلط ومبتلاقيش الملفات
// (فبترجّع المفتاح خام زي "Page_Home"). السطر ده بيقول للمكتبة تستخدم NUH_PORTAL — نفس RootNamespace اللي
// MSBuild بيسجّل بيه ملفات الـ resx — فتتطابق الأسماء وتلاقي الترجمات.
[assembly: Microsoft.Extensions.Localization.RootNamespace("NUH_PORTAL")]

namespace NUH_PORTAL
{
    // نوع علامة (marker) للترجمة المشتركة عبر ملفات .resx
    // ResourcesPath = "Resources" → الملفات: Resources/SharedResource.resx (المحايد = عربي)
    //                                        Resources/SharedResource.en.resx (إنجليزي)
    // يُحقن في الـ Views عبر IHtmlLocalizer<SharedResource> ويُستخدم للنصوص المشتركة (السايدبار/التوب بار/الإشعارات).
    public class SharedResource
    {
    }
}
