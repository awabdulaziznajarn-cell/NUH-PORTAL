# =============================================================================
#  Verify-FacultyDisplayName.ps1
#  التحقّق من أن حساب خدمة البوابة يقدر يكتب في الخصائص الستّ على وحدات
#  سكن أعضاء هيئة التدريس - وأهمّها displayName.
#
#  ⚠️ طريقة التشغيل: من **داخل** نافذة PowerShell مفتوحة، مش بالدبل كليك.
#     الدبل كليك بيفتح نافذة وبيقفلها فور ما السكربت يخلّص أو يقع بخطأ،
#     فالنتيجة بتختفي قبل ما تتقرا. من نافذة مفتوحة:
#
#         powershell -ExecutionPolicy Bypass -File D:\NUH-PORTAL\Verify-FacultyDisplayName.ps1
#
#  ⚠️ قراءة فقط. مابيغيّرش أي صلاحية ولا أي بيانات.
#
#  ⚠️ فحصان لا فحص واحد، والفرق بينهما هو كل الفايدة:
#
#     [1] ‏dsacls  =  «هل السطر اتكتب في قائمة الصلاحيات؟»
#         بيقرا الـ ACL المكتوب على الوحدة التنظيمية. بيجاوب على «السكربت
#         اشتغل ولا لأ» - ومابيجاوبش على «الحساب يقدر يكتب فعلًا»، لأن
#         الصلاحية الفعلية بتتحسب من الوراثة والحظر والعضويات كلها مع بعض.
#
#     [2] allowedAttributesEffective  =  «هل الحساب يقدر يكتب فعلًا؟»
#         خاصية **محسوبة** بيرجّعها الدليل نفسه لكل كائن حسب صلاحيات
#         الحساب المتصل. ودي بالظبط اللي البوابة بتقراها في «فحص صلاحيات
#         الدليل»، فنتيجتها هنا هي نفس نتيجتها هناك.
#
#     الفحص [2] هو الحاسم. لازم يتنفّذ بحساب **الخدمة** لا بحسابك الإداري -
#     السؤال عن صلاحية حساب الخدمة لا عن صلاحيتك. حسابك الإداري بيرجّع
#     «مسموح» دايمًا وde مايقولش حاجة عن المشكلة.
# =============================================================================

# ⚠️ Continue لا Stop: السكربت ده تشخيصي. مع Stop أي خطأ في أول وحدة كان
#    بيوقف الباقي كله - والنافذة بتقفل والمستخدم مايشوفش أي نتيجة. كل خطوة
#    هنا بتتعامل مع فشلها بنفسها وبتكمّل.
$ErrorActionPreference = 'Continue'

# --- حساب الخدمة كما هو مضبوط في appsettings.Production.json ---------------
#     (ADServiceAccount:Username = nuh.portal)
$Account = 'NUH\nuh.portal'      # ⚠️ راجع NUH: شوف الاسم الصح بـ (Get-ADDomain).NetBIOSName

# --- الوحدات التنظيمية الثلاث كما هي في appsettings.json --------------------
$Ous = @(
  'OU=MALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa',
  'OU=FEMALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa',
  'OU=Villas,OU=FACULTY,DC=nuh,DC=edu,DC=sa'
)

# --- الخصائص الستّ اللي البوابة بتكتبها (FacultyHousingService.ManagedAttributes)
$Managed = @('description','displayName','employeeID','mobile','company','department')

# =============================================================================
#  [0] المتطلّبات - بتتفحص الأول عشان الفشل يبان كسطر مفهوم لا كنافذة بتقفل
# =============================================================================
Write-Host ''
Write-Host '=== [0] المتطلّبات ===' -ForegroundColor Cyan

$ok = $true

if (-not (Get-Command dsacls -ErrorAction SilentlyContinue)) {
  Write-Host '  [-] dsacls مش موجود - نفّذ السكربت على وحدة تحكّم الدومين أو جهاز عليه RSAT.' -ForegroundColor Red
  $ok = $false
} else { Write-Host '  [+] dsacls' -ForegroundColor Green }

if (-not (Get-Module -ListAvailable -Name ActiveDirectory)) {
  Write-Host '  [-] وحدة ActiveDirectory مش متثبّتة - ثبّت RSAT: Active Directory.' -ForegroundColor Red
  $ok = $false
} else {
  Import-Module ActiveDirectory -ErrorAction SilentlyContinue
  Write-Host '  [+] ActiveDirectory module' -ForegroundColor Green
}

try {
  $nb = (Get-ADDomain -ErrorAction Stop).NetBIOSName
  $mine = ($Account -split '\\')[0]
  if ($nb -ne $mine) {
    Write-Host "  [-] اسم الدومين المختصر الصح هو '$nb' مش '$mine' - عدّل `$Account فوق." -ForegroundColor Red
    Write-Host "      (وde بيخلّي dsacls يفشل بصمت لو النافذة اتقفلت بسرعة)" -ForegroundColor Red
    $ok = $false
  } else { Write-Host "  [+] الدومين المختصر: $nb" -ForegroundColor Green }
} catch {
  Write-Host "  [-] تعذّر قراءة الدومين: $($_.Exception.Message)" -ForegroundColor Red
  $ok = $false
}

if (-not $ok) {
  Write-Host ''
  Write-Host 'صلّح اللي فوق وأعِد التشغيل.' -ForegroundColor Yellow
  Read-Host 'اضغط Enter للإغلاق'
  return
}

$sam = ($Account -split '\\')[-1]

# =============================================================================
#  [1] الـ ACL المكتوب على الوحدات
# =============================================================================
Write-Host ''
Write-Host '=== [1] السطر اتكتب في قائمة الصلاحيات؟ (dsacls) ===' -ForegroundColor Cyan

foreach ($ou in $Ous) {
  Write-Host ''
  Write-Host "--> $ou" -ForegroundColor Yellow
  $raw = & dsacls $ou 2>&1
  if ($LASTEXITCODE -ne 0) {
    Write-Host "    dsacls فشل (رمز $LASTEXITCODE) - غالبًا المسار غلط أو مفيش صلاحية قراءة." -ForegroundColor Red
    continue
  }
  $hit = $raw | Select-String -SimpleMatch $sam | Select-String -SimpleMatch 'displayName'
  if ($hit) {
    $hit | ForEach-Object { Write-Host "    $($_.ToString().Trim())" -ForegroundColor Green }
  } else {
    Write-Host '    مفيش سطر على displayName للحساب - شغّل Grant-FacultyDisplayName.ps1' -ForegroundColor Red
  }
}

# =============================================================================
#  [2] الصلاحية الفعلية - بحساب الخدمة نفسه
# =============================================================================
Write-Host ''
Write-Host '=== [2] الحساب يقدر يكتب فعلًا؟ (allowedAttributesEffective) ===' -ForegroundColor Cyan
Write-Host ''
Write-Host "اكتب كلمة مرور حساب الخدمة ($Account) - بتتستعمل للاتصال بالدليل فقط:" -ForegroundColor Yellow
$cred = Get-Credential -UserName $Account -Message 'حساب خدمة البوابة'
if (-not $cred) { Write-Host 'اتلغى.' -ForegroundColor Yellow; Read-Host 'اضغط Enter للإغلاق'; return }
$pwd = $cred.GetNetworkCredential().Password

foreach ($ou in $Ous) {
  Write-Host ''
  Write-Host "--> $ou" -ForegroundColor Yellow

  # ⚠️ الفحص على حساب **حقيقي** جوّه الوحدة لا على الوحدة نفسها: الصلاحية
  #    الفعلية بتتحسب لكل كائن على حدة، وحسابات الوحدات هي اللي بتتكتب.
  $sample = $null
  try {
    $sample = Get-ADUser -SearchBase $ou -SearchScope Subtree -Filter * -ResultSetSize 1 -ErrorAction Stop |
              Select-Object -First 1
  } catch {
    Write-Host "    تعذّر قراءة حسابات الوحدة: $($_.Exception.Message)" -ForegroundColor Red
    continue
  }
  if (-not $sample) {
    Write-Host '    مفيش أي حساب جوّه الوحدة دي - اتخطّينا الفحص.' -ForegroundColor DarkGray
    continue
  }

  Write-Host "    الحساب المفحوص: $($sample.SamAccountName)" -ForegroundColor DarkGray

  $eff = $null
  try {
    $entry = New-Object System.DirectoryServices.DirectoryEntry(
               "LDAP://$($sample.DistinguishedName)", $cred.UserName, $pwd)
    $entry.RefreshCache(@('allowedAttributesEffective'))
    $eff = @($entry.Properties['allowedAttributesEffective'])
  } catch {
    Write-Host "    تعذّر الاتصال بالدليل بحساب الخدمة: $($_.Exception.Message)" -ForegroundColor Red
    continue
  }

  $missing = @()
  foreach ($a in $Managed) {
    if ($eff -contains $a) { Write-Host "    [+] $a" -ForegroundColor Green }
    else { Write-Host "    [-] $a" -ForegroundColor Red; $missing += $a }
  }

  if ($missing.Count -eq 0) {
    Write-Host '    النتيجة: 6 / 6 - تمام.' -ForegroundColor Green
  } else {
    Write-Host "    النتيجة: $(6 - $missing.Count) / 6 - الناقص: $($missing -join ', ')" -ForegroundColor Red
  }
}

# =============================================================================
Write-Host ''
Write-Host '=== الخلاصة ===' -ForegroundColor Cyan
Write-Host 'لازم الفحص [2] يقول 6 / 6 على الوحدات التلاتة.' -ForegroundColor Cyan
Write-Host ''
Write-Host 'لو الفحص [1] بيقول السطر موجود والفحص [2] لسه ناقص:' -ForegroundColor Yellow
Write-Host '  - الوراثة متوقّفة على الحسابات: افحص "Include inheritable permissions"' -ForegroundColor Yellow
Write-Host '    على حساب واحد منهم في Active Directory Users and Computers.' -ForegroundColor Yellow
Write-Host '  - أو المنح اتعمل على وحدة تحكّم غير اللي البوابة بتكلّمها - استنّى' -ForegroundColor Yellow
Write-Host '    دورة النسخ المتماثل أو نفّذ repadmin /syncall.' -ForegroundColor Yellow
Write-Host ''
Write-Host 'وبعد ما يقول 6 / 6:' -ForegroundColor Cyan
Write-Host '  البوابة <- سكن أعضاء هيئة التدريس <- فحص صلاحيات الدليل (لازم 6 / 6)' -ForegroundColor Cyan
Write-Host '  ثم الوحدات الموسومة «بانتظار المزامنة» <- إعادة المزامنة.' -ForegroundColor Cyan
Write-Host ''
Read-Host 'اضغط Enter للإغلاق'
