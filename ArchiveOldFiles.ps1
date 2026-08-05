<#
    أرشفة الملفات اللي خلص دورها من D:\NUH-PORTAL
    ---------------------------------------------------------------------------
    ⚠️ مفيش حذف نهائي: الملفات بتتنقل لمجلد _archive بتاريخ. وكلها متتبّعة في git
       أصلاً، فحتى لو مسحت المجلد تقدر ترجّعها بـ git checkout.

    مخرجات الشاشة بالإنجليزي: كونسول ويندوز مابيرسمش العربي صح.

        .\ArchiveOldFiles.ps1            # preview
        .\ArchiveOldFiles.ps1 -Apply     # execute
#>

param(
    [string]$Root = 'D:\NUH-PORTAL',
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

# ملفات انتهى دورها — كل واحد ومعاه سبب الأرشفة
$items = @(
    @{ Name = '_diag_Account.txt';             Why = 'temp diagnostic copy, created for troubleshooting' },
    @{ Name = '_diag_deploy.txt';              Why = 'temp diagnostic copy, created for troubleshooting' },
    @{ Name = '_removed-departure';            Why = 'old Departure screen, removed from the app' },
    @{ Name = 'CleanupTestData.sql';           Why = 'superseded by CleanupTestData-1-Report / -2-Delete' },
    @{ Name = 'AssignRoles.sql';               Why = 'roles + permissions are seeded by DbSeeder now' },
    @{ Name = 'BackfillRequestNumbers.sql';    Why = 'one-off fix, already applied' },
    @{ Name = 'FixRemainingRequestNumbers.sql';Why = 'one-off fix, already applied' },
    @{ Name = 'FixBuildings.sql';              Why = 'one-off fix, already applied' },
    @{ Name = 'CleanupStudentLogins.sql';      Why = 'one-off cleanup, already applied' },
    @{ Name = 'policy.pdf';                    Why = 'duplicate - the served copy is wwwroot\policy.pdf' },
    @{ Name = 'ResetDatabase.cmd';             Why = 'DROPS AND RECREATES THE DATABASE - must not sit on a live server' }
)

$mode = if ($Apply) { 'APPLY (files will be moved)' } else { 'PREVIEW (nothing will be moved)' }
Write-Host "=== Archiving obsolete files in $Root - Mode: $mode ===" -ForegroundColor Cyan

$archive = Join-Path $Root ('_archive-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
$found = 0

foreach ($i in $items) {
    $path = Join-Path $Root $i.Name
    if (-not (Test-Path $path)) {
        Write-Host ("  [skip] {0} - not found" -f $i.Name) -ForegroundColor DarkGray
        continue
    }

    $found++
    Write-Host ("  [move] {0}" -f $i.Name) -ForegroundColor Yellow
    Write-Host ("         reason: {0}" -f $i.Why) -ForegroundColor DarkGray

    if ($Apply) {
        if (-not (Test-Path $archive)) { New-Item -ItemType Directory -Path $archive -Force | Out-Null }
        Move-Item -Path $path -Destination $archive -Force
    }
}

Write-Host ""
if ($Apply) {
    Write-Host ("=== Done - moved $found item(s) to $archive ===") -ForegroundColor Cyan
    Write-Host "Review it, then delete the folder yourself. Everything is in git history too." -ForegroundColor DarkGray
}
else {
    Write-Host ("=== Preview - would move $found item(s) ===") -ForegroundColor Cyan
    Write-Host "If the list looks right, run again with -Apply." -ForegroundColor Yellow
}
