# =============================================================================
#  Grant-FacultyDisplayName.ps1
#  منح حساب خدمة البوابة صلاحية الكتابة على خاصية displayName لحسابات
#  وحدات سكن أعضاء هيئة التدريس.
#
#  ⚠️ ليه السكربت ده موجود:
#     البوابة بقت بتكتب الاسم الإنجليزي للساكن في displayName. الصلاحية في
#     الدليل بتتمنح **لكل خاصية على حدة**، وحساب الخدمة كان مصرَّح له بالخمس
#     خصائص القديمة بس (description و employeeID و mobile و company و
#     department). فأول كتابة بعد التغيير بترجع:
#         The user has insufficient access rights ... INSUFF_ACCESS_RIGHTS
#     والوحدة بتتوسم «بانتظار المزامنة» - البيانات محفوظة عندنا وسليمة،
#     والناقص هو سطر الصلاحية ده.
#
#  يُنفَّذ على وحدة تحكّم الدومين (أو أي جهاز عليه RSAT) بحساب له صلاحية
#  تعديل صلاحيات الوحدات التنظيمية - عادةً Domain Admins.
#
#  ⚠️ بيمنح **أقل** صلاحية تكفي: WP (كتابة خاصية) على displayName وحدها،
#     لكائنات المستخدم وحدها (;user). مافيش إنشاء ولا حذف ولا كلمات مرور.
#
#  ⚠️ الوسيط "/I:S" ضروري: الصلاحية على الكائنات **اللي جوّه** الوحدة التنظيمية
#     لا على الوحدة نفسها. من غيره بتتمنح على كائن الـ OU وحده - والحسابات
#     اللي جوّاه تفضل ممنوعة، فالخطأ مايتغيّرش وتفتكر إن السكربت اشتغل.
# =============================================================================

$ErrorActionPreference = 'Stop'

# --- حساب الخدمة كما هو مضبوط في appsettings.Production.json ---------------
#     (ADServiceAccount:Username - بصيغة SAM المجرّدة)
$Account = 'NUH\nuh.portal'      # ⚠️ غيّر NUH لو اسم الدومين المختصر مختلف

# --- الخاصية المطلوب منح الكتابة عليها -------------------------------------
$Attribute = 'displayName'

# --- الوحدات التنظيمية الثلاث كما هي في appsettings.json --------------------
$Ous = @(
  'OU=MALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa',
  'OU=FEMALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa',
  'OU=Villas,OU=FACULTY,DC=nuh,DC=edu,DC=sa'
)

# =============================================================================
Write-Host ''
Write-Host "=== منح الكتابة على $Attribute ===" -ForegroundColor Cyan
Write-Host "الحساب: $Account"
Write-Host ''

foreach ($ou in $Ous) {
  Write-Host "--> $ou" -ForegroundColor Yellow

  # WP           = Write Property
  # ;displayName = الخاصية دي وحدها
  # ;user        = على كائنات المستخدم فقط
  # /I:S         = يورَّث للكائنات اللي جوّه الوحدة
  & dsacls "$ou" /I:S /G "${Account}:WP;$Attribute;user" | Out-Null

  if ($LASTEXITCODE -ne 0) {
    Write-Host "    فشل المنح (رمز $LASTEXITCODE)" -ForegroundColor Red
  } else {
    Write-Host '    تم' -ForegroundColor Green
  }
}

# =============================================================================
#  التحقّق - بيقرا الصلاحيات الفعلية ويطبع سطور حساب الخدمة على الخاصية دي
# =============================================================================
Write-Host ''
Write-Host '=== التحقّق ===' -ForegroundColor Cyan
$sam = ($Account -split '\\')[-1]

foreach ($ou in $Ous) {
  Write-Host ''
  Write-Host "--> $ou" -ForegroundColor Yellow
  $lines = & dsacls "$ou" | Select-String -SimpleMatch $sam |
           Select-String -SimpleMatch $Attribute
  if ($lines) { $lines | ForEach-Object { Write-Host "    $_" } }
  else        { Write-Host "    مفيش سطر صلاحية على $Attribute للحساب!" -ForegroundColor Red }
}

Write-Host ''
Write-Host 'خلّص. الخطوتان الباقيتان في البوابة:' -ForegroundColor Cyan
Write-Host '  1) سكن أعضاء هيئة التدريس <- فحص صلاحيات الدليل' -ForegroundColor Cyan
Write-Host "     لازم displayName تبقى «مسموح» على الوحدات التلاتة." -ForegroundColor Cyan
Write-Host '  2) الوحدات الموسومة «بانتظار المزامنة» <- زرّ «إعادة المزامنة».' -ForegroundColor Cyan
Write-Host ''

# =============================================================================
#  للتراجع (لو احتجت تشيل الصلاحية دي وحدها):
#
#     dsacls "OU=MALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa"  /I:S /R "NUH\nuh.portal"
#
#  ⚠️ الوسيط "/R" بيشيل **كل** صلاحيات الحساب على الوحدة - مش الخاصية دي بس. يعني
#     الاستيراد والتحديث هيقفوا كمان. متستعملهاش إلا لو ناوي تعيد المنح كله
#     من الأول (Grant-FacultyOuMove.ps1 ومعاه ده).
# =============================================================================
