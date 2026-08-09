# =====================================================================
#  حذف شاشة «المغادرة» من النظام
#
#  ليه:
#    محتواها كان مكرّرًا حرفيًا في شاشة «تحديث حالة الطالب»، ومفيش أي رابط
#    ليها في القائمة الجانبية ولا في أي صفحة. يعني كود حيّ لشاشة ميتة.
#
#    وفيها كمان نقطة أمنية: DepartureController كان [Authorize] بلا أي
#    صلاحية — يعني أي حساب مسجّل دخوله (حتى حساب طالب) يقدر يفتح /Departure.
#    البيانات نفسها محميّة لأن نداءات الـ API بتفحص الصلاحية، لكن الشاشة
#    كانت مفتوحة. حذفها بيقفل الموضوع من أصله.
#
#  اللي بيتحذف:
#    Views/Departure/                     الشاشة
#    Controllers/Mvc/DepartureController  المسار /Departure
#    wwwroot/js/supervisor-departure-page.js   الجافاسكريبت (مستعمل هنا وحده)
#
#  و٢١ مفتاح ترجمة كانوا بيخدموها وحدها اتشالوا من ملفي .resx (متسلّمين معاه).
#
#  ⚠️ الملفات بتتنقل لمجلد _deleted بتاريخ اليوم — مش بتتمسح نهائيًا.
#     امسح المجلد بنفسك بعد ما تتأكد إن كل حاجة شغالة.
# =====================================================================

$ErrorActionPreference = 'Stop'
$root  = 'D:\NUH-PORTAL\NUH-PORTAL'
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$bin   = Join-Path 'D:\NUH-PORTAL' "_deleted\$stamp"

New-Item -ItemType Directory -Path $bin -Force | Out-Null

$targets = @(
    'Views\Departure',
    'Controllers\Mvc\DepartureController.cs',
    'wwwroot\js\supervisor-departure-page.js'
)

foreach ($rel in $targets) {
    $src = Join-Path $root $rel
    if (Test-Path $src) {
        $dst = Join-Path $bin $rel
        New-Item -ItemType Directory -Path (Split-Path $dst -Parent) -Force | Out-Null
        Move-Item -Path $src -Destination $dst -Force
        Write-Host "نُقل: $rel" -ForegroundColor Yellow
    } else {
        Write-Host "غير موجود (اتشال قبل كده؟): $rel" -ForegroundColor DarkGray
    }
}

Write-Host ""
Write-Host "تم. الملفات في: $bin" -ForegroundColor Green
Write-Host "الخطوة التالية: dotnet build ثم NuhPortalDeploy.bat" -ForegroundColor Cyan
Write-Host "وللتراجع: انقل الملفات من المجلد ده لمكانها." -ForegroundColor DarkGray
