<#
    تنظيف بيانات التجربة — الملفات وحسابات الأكتف دايركتوري
    ---------------------------------------------------------------------------
    ⚠️ مخرجات الشاشة بالإنجليزي عن قصد: كونسول ويندوز القديم مابيعرفش يرسم
       العربي من اليمين للشمال، فالرسائل كانت بتطلع حروف مبعثرة. التعليقات
       هنا بالعربي عادي لأنها بتتقرا في محرر نصوص.

    بيشتغل على مرحلتين: معاينة (الافتراضي) ثم -Apply للتنفيذ.

        .\CleanupTestFiles.ps1                 # preview only
        .\CleanupTestFiles.ps1 -Apply          # execute

    ⚠️ حذف حسابات الأكتف دايركتوري محتاج حساب *دومين* له صلاحية الحذف.
       لو داخل بحساب Administrator محلي هتاخد:
         "The server has rejected the client credentials"
       الحل: -Credential (domain\user) أو نفّذ الجزء ده من الـ Domain Controller.
#>

param(
    [switch]$Apply,

    # حسابات الطلاب التجريبية في الأكتف دايركتوري.
    # ⚠️ راجعها حرف بحرف قبل التنفيذ — السكربت بيسأل تأكيد منفصل لكل حساب.
    [string[]]$TestAdAccounts = @(
        'h456123000',
        'h456202020',
        'h456255500',
        'h456969999',
        'h456985696',
        'h496325699'
    ),

    # مجلدات المرفقات — المرفقات موزّعة على أكتر من مسار
    [string[]]$DataRoots = @('D:\NUH-DATA\Students', 'D:\NUH-DATA\Housing-Transfer-Attachments'),

    # بيانات اعتماد حساب دومين، لو المستخدم الحالي مش حساب دومين
    [System.Management.Automation.PSCredential]$Credential,

    # تخطّي جزء الأكتف دايركتوري تمامًا (لو هتمسح الحسابات يدويًا من الـ DC)
    [switch]$SkipAd
)

$ErrorActionPreference = 'Stop'
$mode = if ($Apply) { 'APPLY (changes will be made)' } else { 'PREVIEW (nothing will be deleted)' }
Write-Host "=== Mode: $mode ===" -ForegroundColor Cyan

# ---------------------------------------------------------------------------
# 1) Attachment files
# ---------------------------------------------------------------------------
foreach ($DataRoot in $DataRoots) {

    Write-Host "`n--- $DataRoot ---" -ForegroundColor Yellow

    if (-not (Test-Path $DataRoot)) {
        Write-Host "  Folder does not exist - skipped." -ForegroundColor DarkGray
        continue
    }

    $entries = Get-ChildItem -Path $DataRoot -Force -ErrorAction SilentlyContinue
    if (-not $entries) {
        Write-Host "  Empty." -ForegroundColor DarkGray
        continue
    }

    foreach ($e in $entries) {
        if ($e.PSIsContainer) {
            $files  = Get-ChildItem -Path $e.FullName -Recurse -File -ErrorAction SilentlyContinue
            $sizeKb = if ($files) { [math]::Round(($files | Measure-Object Length -Sum).Sum / 1KB, 1) } else { 0 }
            Write-Host ("  [DIR ] {0}  -  {1} file(s)  ({2} KB)" -f $e.Name, $files.Count, $sizeKb)
        }
        else {
            Write-Host ("  [FILE] {0}  -  {1} KB" -f $e.Name, [math]::Round($e.Length / 1KB, 1))
        }
    }

    if ($Apply) {
        # بننقلهم لمجلد أرشيف مؤرّخ بدل الحذف المباشر
        $archive = Join-Path 'D:\NUH-DATA' ('_archived-' + (Split-Path $DataRoot -Leaf) + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
        New-Item -ItemType Directory -Path $archive -Force | Out-Null
        Move-Item -Path (Join-Path $DataRoot '*') -Destination $archive -Force
        Write-Host "  -> Moved to archive: $archive" -ForegroundColor Green
        Write-Host "     Review it, then delete it yourself." -ForegroundColor DarkGray
    }
}

# ---------------------------------------------------------------------------
# 2) Active Directory test accounts
# ---------------------------------------------------------------------------
Write-Host "`n--- Active Directory accounts ---" -ForegroundColor Yellow

if ($SkipAd) {
    Write-Host "  Skipped (-SkipAd)." -ForegroundColor DarkGray
}
elseif (-not (Get-Module -ListAvailable -Name ActiveDirectory)) {
    Write-Host "  ActiveDirectory module not installed on this machine." -ForegroundColor Red
    Write-Host "    Windows Server : Install-WindowsFeature RSAT-AD-PowerShell" -ForegroundColor DarkGray
    Write-Host "    Or delete the accounts from ADUC on the domain controller." -ForegroundColor DarkGray
}
else {
    Import-Module ActiveDirectory

    # لو المستخدم الحالي مش حساب دومين، الاستعلام بيترفض. -Credential بيحل ده.
    $adArgs = @{}
    if ($Credential) { $adArgs['Credential'] = $Credential }

    foreach ($sam in $TestAdAccounts) {

        try {
            $user = Get-ADUser -Filter "SamAccountName -eq '$sam'" `
                               -Properties DistinguishedName, DisplayName, Enabled, whenCreated `
                               @adArgs -ErrorAction Stop
        }
        catch {
            Write-Host "  $sam - query failed: $($_.Exception.Message)" -ForegroundColor Red
            Write-Host "    You are probably signed in with a LOCAL account, not a domain account." -ForegroundColor DarkGray
            Write-Host "    Retry with:  .\CleanupTestFiles.ps1 -Apply -Credential (Get-Credential)" -ForegroundColor DarkGray
            break
        }

        if (-not $user) {
            Write-Host "  $sam - not found (already deleted, or never created)" -ForegroundColor DarkGray
            continue
        }

        Write-Host ("  {0}  |  {1}  |  Enabled: {2}  |  Created: {3}" -f `
            $sam, $user.DisplayName, $user.Enabled, $user.whenCreated) -ForegroundColor White
        Write-Host ("     DN: {0}" -f $user.DistinguishedName) -ForegroundColor DarkGray

        if ($Apply) {
            # ⚠️ تأكيد فردي لكل حساب — الحذف من الدومين مالوش رجعة
            $answer = Read-Host "     Delete this account from Active Directory? (type yes)"
            if ($answer -eq 'yes') {
                Remove-ADUser -Identity $user.DistinguishedName -Confirm:$false @adArgs
                Write-Host "     Deleted." -ForegroundColor Green
            }
            else {
                Write-Host "     Kept." -ForegroundColor DarkGray
            }
        }
    }
}

Write-Host "`n=== Done ($mode) ===" -ForegroundColor Cyan
if (-not $Apply) {
    Write-Host "If the numbers above look right, run again with -Apply." -ForegroundColor Yellow
}
