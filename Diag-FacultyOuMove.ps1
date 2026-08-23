# =============================================================================
#  Diag-FacultyOuMove.ps1
#  تشخيص «Access is denied» عند نقل حساب وحدة بين OU=MALE و OU=FEMALE.
#
#  يُنفَّذ على وحدة تحكّم الدومين (أو جهاز عليه RSAT) بحساب Domain Admin.
#
#  ⚠️ السكربت ده **بيقرا ويشخّص بس** - مابيغيّرش أي صلاحية.
#     أوامر التصليح مطبوعة في الآخر عشان تنفّذها بقرار منك.
# =============================================================================

$ErrorActionPreference = 'Continue'

$Account = 'NUH\nuh.portal'
$Sam     = 'nuh.portal'
$MaleOu   = 'OU=MALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa'
$FemaleOu = 'OU=FEMALE,OU=Buludings,OU=FACULTY,DC=nuh,DC=edu,DC=sa'
$TestUser = 'bu8ap08'      # الحساب اللي فشل نقله

function Head($t) { Write-Host ''; Write-Host "=== $t ===" -ForegroundColor Cyan }

# -----------------------------------------------------------------------------
Head '1) هل وصلت صلاحية Create/Delete Child فعلًا للوحدتين؟'
# ⚠️ الفحص في البوابة بيسأل عن **الإنشاء** بس (allowedChildClassesEffective).
#    الحذف مالوش خاصية محسوبة، فبنقراه من الـ ACL هنا.
foreach ($ou in @($MaleOu, $FemaleOu)) {
    Write-Host ''
    Write-Host "--> $ou" -ForegroundColor Yellow
    $acl = & dsacls "$ou" | Select-String -SimpleMatch $Sam
    if ($acl) { $acl | ForEach-Object { Write-Host "    $_" } }
    else      { Write-Host '    !! مفيش أي ACE للحساب على الوحدة دي' -ForegroundColor Red }
}

# -----------------------------------------------------------------------------
Head '2) الحماية من الحذف العارض (أشهر سبب لفشل النقل رغم وجود الصلاحية)'
# ⚠️ لما «Protect object from accidental deletion» مفعّلة، ويندوز بيحطّ
#    ACE من نوع **Deny** على الحذف - والـ Deny بيغلب أي Allow.
#    والنقل في الدليل عملية حذف من مكان وإنشاء في مكان، فبيتمنع.
try {
    $u = Get-ADUser -Identity $TestUser -Properties ProtectedFromAccidentalDeletion, DistinguishedName
    Write-Host "    الحساب  $TestUser : محمي من الحذف = $($u.ProtectedFromAccidentalDeletion)" `
        -ForegroundColor $(if ($u.ProtectedFromAccidentalDeletion) { 'Red' } else { 'Green' })
    Write-Host "    مساره الحالي: $($u.DistinguishedName)"
} catch { Write-Host "    تعذّر قراءة $TestUser : $_" -ForegroundColor Red }

foreach ($ou in @($MaleOu, $FemaleOu)) {
    try {
        $o = Get-ADOrganizationalUnit -Identity $ou -Properties ProtectedFromAccidentalDeletion
        Write-Host "    الوحدة  $($o.Name) : محمية من الحذف = $($o.ProtectedFromAccidentalDeletion)" `
            -ForegroundColor $(if ($o.ProtectedFromAccidentalDeletion) { 'Yellow' } else { 'Green' })
    } catch { Write-Host "    تعذّر قراءة $ou : $_" -ForegroundColor Red }
}

# -----------------------------------------------------------------------------
Head '3) أي ACE من نوع Deny على الحساب نفسه'
try {
    $dn  = (Get-ADUser -Identity $TestUser).DistinguishedName
    $sd  = (Get-Acl "AD:$dn").Access | Where-Object { $_.AccessControlType -eq 'Deny' }
    if ($sd) {
        $sd | Select-Object IdentityReference, ActiveDirectoryRights |
              Format-Table -AutoSize | Out-String | Write-Host
    } else { Write-Host '    مفيش أي Deny على الحساب.' -ForegroundColor Green }
} catch { Write-Host "    $_" -ForegroundColor Red }

# -----------------------------------------------------------------------------
Head '4) أوامر التصليح — نفّذ اللي يناسب النتيجة فوق'
Write-Host @"

  (أ) لو الحماية من الحذف العارض مفعّلة على الحساب:

      Set-ADObject -Identity '$TestUser' -ProtectedFromAccidentalDeletion `$false

      -- ولكل حسابات الوحدتين مرة واحدة:
      Get-ADUser -SearchBase '$MaleOu'   -Filter * | Set-ADObject -ProtectedFromAccidentalDeletion `$false
      Get-ADUser -SearchBase '$FemaleOu' -Filter * | Set-ADObject -ProtectedFromAccidentalDeletion `$false


  (ب) لو ACE الحذف مش ظاهر في الخطوة (1)، امنح صلاحية الحذف
      على كائنات المستخدم **جوّه** الوحدة (وارثة للأبناء):

      dsacls "$MaleOu"   /I:S /G "${Account}:SD;;user"
      dsacls "$FemaleOu" /I:S /G "${Account}:SD;;user"

      ⚠️ دي غير CCDC اللي على الوحدة نفسها. CCDC بتقول «تقدر تحذف ابنًا من
         الوحدة»، وSD بتقول «تقدر تحذف الكائن ده». بعض بيئات الدليل بتطلب
         الاتنين حسب الـ ACEs الموروثة.


  (ج) بعد أي تصليح، اتأكد من الدليل مباشرةً قبل ما ترجع للبوابة:

      Move-ADObject -Identity '$TestUser' -TargetPath '$FemaleOu' `
                    -Credential (Get-Credential '$Account')

      -- لو الأمر ده نجح، النقل من البوابة هينجح. ولو فشل، الرسالة هتقول السبب.
      -- وللرجوع:  Move-ADObject -Identity '$TestUser' -TargetPath '$MaleOu'
"@
Write-Host ''
