# =============================================================================
#  Grant-FacultyOuMove.ps1
#  منح حساب خدمة البوابة صلاحية نقل حسابات وحدات سكن أعضاء هيئة التدريس
#  بين قسمَي الدليل (MALE / FEMALE).
#
#  يُنفَّذ على وحدة تحكّم الدومين (أو أي جهاز عليه RSAT) بحساب له صلاحية
#  تعديل صلاحيات الوحدات التنظيمية — عادةً Domain Admins.
#
#  ⚠️ السكربت ده بيمنح **أقل** صلاحية تكفي للنقل:
#       Create Child + Delete Child — لكائنات المستخدم وحدها (;user)
#     ومابيمنحش تغيير كلمات مرور ولا إنشاء مجموعات ولا أي حاجة تانية.
#
#  ⚠️ الصلاحيتين على **القسمين** لا على واحد: النقل بيروح ويجي.
#     عضو هيئة تدريس محلّه عضوة = نقل من MALE لـ FEMALE،
#     والعكس بيحصل بردو. فكل قسم لازم يكون مصدرًا وهدفًا.
# =============================================================================

$ErrorActionPreference = 'Stop'

# --- حساب الخدمة كما هو مضبوط في appsettings.Production.json ---------------
#     (ADServiceAccount:Username — بصيغة SAM المجرّدة)
$Account = 'NUH\nuh.portal'      # ⚠️ غيّر NUH لو اسم الدومين المختصر مختلف

# --- الوحدتان التنظيميتان كما هما في appsettings.Production.json ------------
$Ous = @(
  'OU=MALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa',
  'OU=FEMALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa'
)

# =============================================================================
Write-Host ''
Write-Host '=== منح صلاحية النقل ===' -ForegroundColor Cyan
Write-Host "الحساب: $Account"
Write-Host ''

foreach ($ou in $Ous) {
  Write-Host "--> $ou" -ForegroundColor Yellow

  # CC = Create Child   (إنشاء كائن في الوحدة  = النقل إليها)
  # DC = Delete Child   (حذف كائن من الوحدة    = النقل منها)
  # ;user = لكائنات المستخدم فقط
  & dsacls "$ou" /G "${Account}:CCDC;user" | Out-Null

  if ($LASTEXITCODE -ne 0) {
    Write-Host "    فشل المنح (رمز $LASTEXITCODE)" -ForegroundColor Red
  } else {
    Write-Host '    تم' -ForegroundColor Green
  }
}

# =============================================================================
#  التحقّق — بيقرا الصلاحيات الفعلية ويطبع سطور حساب الخدمة
# =============================================================================
Write-Host ''
Write-Host '=== التحقّق ===' -ForegroundColor Cyan
$sam = ($Account -split '\\')[-1]

foreach ($ou in $Ous) {
  Write-Host ''
  Write-Host "--> $ou" -ForegroundColor Yellow
  $lines = & dsacls "$ou" | Select-String -SimpleMatch $sam
  if ($lines) { $lines | ForEach-Object { Write-Host "    $_" } }
  else        { Write-Host '    مفيش أي صلاحية مسجّلة للحساب!' -ForegroundColor Red }
}

Write-Host ''
Write-Host 'خلّص. افتح البوابة: سكن أعضاء هيئة التدريس ← فحص الصلاحية،' -ForegroundColor Cyan
Write-Host 'ولازم يقول «النقل إليها: مسموح» على القسمين.' -ForegroundColor Cyan
Write-Host ''

# =============================================================================
#  للتراجع (لو احتجت تشيل الصلاحية):
#
#     dsacls "OU=MALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa"  /R "NUH\nuh.portal"
#     dsacls "OU=FEMALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa" /R "NUH\nuh.portal"
#
#  ⚠️ /R بتشيل **كل** صلاحيات الحساب على الوحدة — بما فيها القراءة والكتابة
#     اللي الاستيراد والتحديث محتاجينها. متستعملهاش إلا لو ناوي تعيد المنح
#     من الأول.
# =============================================================================
