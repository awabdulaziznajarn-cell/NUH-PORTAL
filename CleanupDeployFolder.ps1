<#
    تنظيف مجلد النشر D:\Deploy
    ---------------------------------------------------------------------------
    ملف النشر NuhPortalDeploy.bat بيعمل مع كل تشغيل مجلدين:

      NUH-PORTAL-<التاريخ>-Production   نسخة البناء (publish) اللي اتنشرت
      _backup-<التاريخ>                 نسخة من الموقع الحيّ قبل الاستبدال

    ومحدش بيمسحهم، فبيتراكموا. السكربت ده بيسيب أحدث نسخ (افتراضيًا ٣ من كل نوع)
    ويمسح الباقي.

    ⚠️ النسخ دي هي طوق النجاة لو نشرة طلعت غلط ومحتاج ترجّع القديم بسرعة.
       ماتخليش العدد أقل من ٢، وماتشغّلش السكربت ده وإنت لسه بتختبر نشرة جديدة.

    ⚠️ مخرجات الشاشة بالإنجليزي عن قصد: كونسول ويندوز مابيرسمش العربي صح.

    الاستخدام:
        .\CleanupDeployFolder.ps1                # معاينة، بيسيب أحدث ٣
        .\CleanupDeployFolder.ps1 -Keep 5        # معاينة، بيسيب أحدث ٥
        .\CleanupDeployFolder.ps1 -Apply         # تنفيذ
#>

param(
    [string]$DeployRoot = 'D:\Deploy',
    [int]$Keep = 3,
    [switch]$Apply
)

$ErrorActionPreference = 'Stop'

if ($Keep -lt 2) {
    Write-Host "-Keep must be at least 2 - you always need one copy to roll back to." -ForegroundColor Red
    return
}

if (-not (Test-Path $DeployRoot)) {
    Write-Host "Folder $DeployRoot does not exist." -ForegroundColor Red
    return
}

$mode = if ($Apply) { 'APPLY (folders will be deleted)' } else { 'PREVIEW (nothing will be deleted)' }
Write-Host "=== Cleaning $DeployRoot - Mode: $mode - keeping newest $Keep of each kind ===" -ForegroundColor Cyan

function Get-FolderSizeMb {
    param([string]$Path)
    $files = Get-ChildItem -Path $Path -Recurse -File -ErrorAction SilentlyContinue
    if (-not $files) { return 0 }
    return [math]::Round(($files | Measure-Object Length -Sum).Sum / 1MB, 1)
}

$groups = @(
    @{ Name = 'Site backups taken before each deploy'; Pattern = '_backup-*' },
    @{ Name = 'Publish output packages';               Pattern = 'NUH-PORTAL-*-Production' }
)

$totalFreed = 0
$totalToDelete = 0

foreach ($g in $groups) {

    $folders = Get-ChildItem -Path $DeployRoot -Directory -Filter $g.Pattern -ErrorAction SilentlyContinue |
               Sort-Object Name -Descending

    Write-Host "`n--- $($g.Name)  ($($folders.Count) folder(s)) ---" -ForegroundColor Yellow

    if ($folders.Count -le $Keep) {
        Write-Host "  Only $($folders.Count) - at or below the keep limit of $Keep, nothing to delete." -ForegroundColor DarkGray
        continue
    }

    $keepList   = $folders | Select-Object -First $Keep
    $deleteList = $folders | Select-Object -Skip  $Keep

    Write-Host "  KEEP:" -ForegroundColor Green
    foreach ($f in $keepList) {
        Write-Host ("    {0}   ({1} MB)" -f $f.Name, (Get-FolderSizeMb $f.FullName))
    }

    Write-Host "  DELETE ($($deleteList.Count)):" -ForegroundColor Red
    foreach ($f in $deleteList) {
        $sizeMb = Get-FolderSizeMb $f.FullName
        $totalFreed += $sizeMb
        $totalToDelete++
        Write-Host ("    {0}   ({1} MB)" -f $f.Name, $sizeMb)

        if ($Apply) {
            try {
                Remove-Item -Path $f.FullName -Recurse -Force
                Write-Host "      deleted." -ForegroundColor DarkGray
            }
            catch {
                Write-Host "      delete failed: $($_.Exception.Message)" -ForegroundColor Red
            }
        }
    }
}

Write-Host ""
if ($Apply) {
    Write-Host ("=== Done - deleted {0} folder(s), freed about {1} MB ===" -f $totalToDelete, [math]::Round($totalFreed,1)) -ForegroundColor Cyan
}
else {
    Write-Host ("=== Preview - would delete {0} folder(s), freeing about {1} MB ===" -f $totalToDelete, [math]::Round($totalFreed,1)) -ForegroundColor Cyan
    Write-Host "If the numbers look right, run again with -Apply." -ForegroundColor Yellow
}
