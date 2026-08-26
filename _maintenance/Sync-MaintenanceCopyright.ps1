# =============================================================================
#  Sync-MaintenanceCopyright.ps1
#  يزامن سطر الحقوق في صفحة الصيانة مع Brand_Copyright في ملف الترجمة.
# =============================================================================
#  ⚠️ ليه السكربت ده موجود:
#     كل نصوص النظام مصدرها واحد: Resources\SharedResource.resx. الشاشات
#     تقرأه من الخادم، وصفحات البوابة تجيبه من /api/ui/i18n.
#
#     وصفحة الصيانة الاستثناء الوحيد الذي لا حيلة فيه: IIS يخدمها والتطبيق
#     **متوقّف**، فلا يُحمَّل معها ملف خارجي ولا تُنادى نقطة ترجمة. نصّها
#     مكتوب داخلها بالضرورة.
#
#     فكانت النتيجة الحتمية: يُغيَّر Brand_Copyright في ملف الترجمة، ويصل
#     التغيير إلى إحدى عشرة شاشة، وتبقى صفحة الصيانة على النصّ القديم -
#     ولا يكتشفها أحد إلا إذا صادف أن دخل الموقع وهو تحت الصيانة.
#
#     السكربت ده بيقفل الفجوة دي: بيقرأ المفتاح من ملف الترجمة وبيكتبه في
#     صفحة الصيانة قبل نشرها. فالمصدر يفضل واحدًا، والنسخ بيتولّد لا يتكتب.
#
#  ⚠️ وبيشتغل على الأصل (_maintenance\app_offline.htm) لا على النسخة المنشورة:
#     النسخ إلى D:\Publish بيحصل بعده في السكربت المنادي، فالاتنين يتطابقوا.
#
#  الاستعمال:
#      powershell -NoProfile -ExecutionPolicy Bypass -File Sync-MaintenanceCopyright.ps1
#      powershell ... -File Sync-MaintenanceCopyright.ps1 -Check     (فحص بلا كتابة)
# =============================================================================
[CmdletBinding()]
param(
    [string] $ResxPath = '',
    [string] $PagePath = '',
    [switch] $Check
)

$ErrorActionPreference = 'Stop'

function Fail($msg) { Write-Host "[ERROR] $msg" -ForegroundColor Red; exit 1 }

# ⚠️ مجلّد السكربت يُحسب هنا في الجسم لا في param.
#    القيم الافتراضية في param تُقيَّم في نطاق لا يكون $PSScriptRoot مضمونًا
#    فيه عند التشغيل بـ powershell -File - يرجع فارغًا فتنهار Join-Path
#    برسالة «Cannot bind argument to parameter 'Path'». وحصل فعلًا في أول نشر.
#    والبدائل الثلاثة مرتّبة: أوثقها أولًا، وآخرها يعمل حتى في PowerShell 2.
$root = $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($root) -and $PSCommandPath) { $root = Split-Path -Parent $PSCommandPath }
if ([string]::IsNullOrWhiteSpace($root)) { $root = Split-Path -Parent $MyInvocation.MyCommand.Definition }
if ([string]::IsNullOrWhiteSpace($root)) { Fail "Could not determine the script folder." }

if ([string]::IsNullOrWhiteSpace($ResxPath)) {
    $ResxPath = Join-Path $root '..\NUH-PORTAL\Resources\SharedResource.resx'
}
if ([string]::IsNullOrWhiteSpace($PagePath)) {
    $PagePath = Join-Path $root 'app_offline.htm'
}

if (-not (Test-Path $ResxPath)) { Fail "Translation file not found: $ResxPath" }
if (-not (Test-Path $PagePath)) { Fail "Maintenance page not found: $PagePath" }

# ---- قراءة المفتاح من ملف الترجمة ----
try {
    [xml]$resx = Get-Content -LiteralPath $ResxPath -Raw -Encoding UTF8
} catch { Fail "Could not parse $ResxPath : $($_.Exception.Message)" }

$node = $resx.root.data | Where-Object { $_.name -eq 'Brand_Copyright' } | Select-Object -First 1
if (-not $node) { Fail "Key 'Brand_Copyright' not found in $ResxPath" }

$copyright = [string]$node.value
if ([string]::IsNullOrWhiteSpace($copyright)) { Fail "Key 'Brand_Copyright' is empty." }

# ⚠️ القيمة سطران في ملف الترجمة، وتُكتب في الصفحة سطرين كما هي:
#    وسم <footer> في app_offline.htm عليه white-space:pre-line - نفس ما تفعله
#    .nuh-foot-copy في js/site-footer.js. فتقسيم السطر يبقى في ملف الترجمة
#    وحده ولا يُكتب هنا.
$copyright = $copyright -replace "`r`n", "`n"
$copyright = $copyright.Trim()

# ⚠️ التهريب لازم: النصّ يدخل داخل HTML. مصدره ملفاتنا لا مستخدم، لكن قوسًا
#    زاويًّا واحدًا فيه بالغلط كان يكسر بنية الصفحة بلا أن يشتكي أحد.
$escaped = $copyright.Replace('&','&amp;').Replace('<','&lt;').Replace('>','&gt;')

# ---- استبدال محتوى <footer> ----
$page = Get-Content -LiteralPath $PagePath -Raw -Encoding UTF8

# ⚠️ (?s) عشان المحتوى سطران. والنمط غير جشِع (.*?) حتى لا يبتلع ما بعده
#    لو أُضيف وسم footer ثانٍ يومًا.
$pattern = '(?s)<footer>.*?</footer>'
if ($page -notmatch $pattern) { Fail "No <footer> element found in $PagePath" }

$current = [regex]::Match($page, $pattern).Value
$desired = "<footer>$escaped</footer>"

if ($current -eq $desired) {
    Write-Host "[OK] Maintenance page copyright already matches SharedResource.resx." -ForegroundColor Green
    exit 0
}

if ($Check) {
    Write-Host "[DIFF] Maintenance page copyright is OUT OF DATE." -ForegroundColor Yellow
    Write-Host "       page : $($current -replace '<[^>]+>','' -replace "`n",' | ')"
    Write-Host "       resx : $($copyright -replace "`n",' | ')"
    exit 2
}

$updated = [regex]::Replace($page, $pattern, { $desired })

# ⚠️ UTF8 **بلا** علامة ترتيب البايتات: الصفحة تحمل <meta charset="utf-8">
#    في ترويستها، والملف الأصلي بلا BOM ويُعرض سليمًا اليوم. إضافتها هنا
#    كانت ستغيّر أول بايت في ملف يعمل - تغييرٌ بلا سبب.
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText((Resolve-Path $PagePath), $updated, $enc)

Write-Host "[OK] Maintenance page copyright synced from SharedResource.resx." -ForegroundColor Green
Write-Host "     $($copyright -replace "`n",' | ')"
exit 0
