# =============================================================================
#  ArchiveLegacyWwwroot.ps1
#
#  ينقل الملفات القديمة/المكرّرة من wwwroot إلى مجلد أرشيف خارج الموقع.
#  لا يحذف شيئًا — النقل قابل للتراجع بنسخ الملفات من مجلد الأرشيف.
#
#  الاستخدام:
#     .\ArchiveLegacyWwwroot.ps1            معاينة فقط (لا ينقل شيئًا)
#     .\ArchiveLegacyWwwroot.ps1 -Apply     التنفيذ الفعلي
#
#  (مخرجات الشاشة بالإنجليزية لأن كونسول ويندوز لا يعرض العربية بشكل صحيح)
# =============================================================================
param([switch]$Apply)

$ErrorActionPreference = 'Stop'
$root    = 'D:\NUH-PORTAL\NUH-PORTAL\wwwroot'
$archive = 'D:\NUH-PORTAL\_archive\wwwroot-legacy'

# --- 1) ملفات لا يشير إليها أي كود في المشروع -------------------------------
$unreferenced = @(
    '1.png',              # 1.4 MB - not referenced anywhere
    '374837.png',         # 481 KB - not referenced anywhere
    'pic-1-1.jpeg',       # 85 KB  - not referenced anywhere
    'inactivity.js',
    'notifications.js',
    'lookup-admin.css',
    'lookup-admin.js',
    'VersionInfo.js'
)

# --- 2) شاشات ما قبل MVC — Program.cs يحوّل روابطها بالفعل -------------------
$legacyPages = @(
    'dashboard.html',
    'students.html',
    'register_student.html',
    'bulk-registration.html',
    'student-status.html',
    'requests.html',
    'request-details.html',
    'housing-management.html',
    'reports.html',
    'auditlog.html',
    'supervisor-departure.html',
    'login.html',
    'login-v2.html',
    'login-lang.html',
    'sidebar.html',
    'sidebar.js'
)

# --- 3) نسخ مكرّرة في الجذر، النسخة الحيّة في wwwroot\register --------------
$duplicates = @(
    'register-form.html',
    'register-confirmation.html',
    'track-request.html'
)

# --- ملفات يجب ألا تُلمس (للتوثيق فقط - مستخدمة فعليًا) ---------------------
#   index.html          -> صفحة البداية (DefaultFiles)
#   i18n.js             -> كل شاشات البوابة
#   housing-management.js -> Views\Housing\Index.cshtml
#   lookups.js          -> Views\Register + Views\Students
#   nni1.jpg            -> Views\Account\Login.cshtml
#   policy.pdf          -> register-declarations.html
#   nu-logo.png, favicon.ico, css\, fonts\, js\, register\

$groups = @(
    @{ Name = 'Unreferenced files'; Items = $unreferenced },
    @{ Name = 'Pre-MVC pages (already redirected in Program.cs)'; Items = $legacyPages },
    @{ Name = 'Duplicate copies (live version is in wwwroot\register)'; Items = $duplicates }
)

Write-Host ''
Write-Host '=== Legacy wwwroot cleanup ===' -ForegroundColor Cyan
Write-Host ("Source : {0}" -f $root)
Write-Host ("Archive: {0}" -f $archive)
if (-not $Apply) { Write-Host 'MODE   : PREVIEW (nothing will be moved)' -ForegroundColor Yellow }
else             { Write-Host 'MODE   : APPLY' -ForegroundColor Green }
Write-Host ''

if ($Apply -and -not (Test-Path $archive)) {
    New-Item -ItemType Directory -Path $archive -Force | Out-Null
}

$totalCount = 0
$totalBytes = 0

foreach ($g in $groups) {
    Write-Host ("-- {0}" -f $g.Name) -ForegroundColor Cyan
    foreach ($f in $g.Items) {
        $src = Join-Path $root $f
        if (-not (Test-Path $src)) {
            Write-Host ("   [skip]  {0}  (not found)" -f $f) -ForegroundColor DarkGray
            continue
        }
        $size = (Get-Item $src).Length
        $totalCount++
        $totalBytes += $size
        $kb = [math]::Round($size / 1KB, 1)

        if ($Apply) {
            $dest = Join-Path $archive $f
            if (Test-Path $dest) {
                # لا نطمس نسخة أرشيف سابقة
                $dest = Join-Path $archive ("{0}.{1}" -f $f, (Get-Random -Maximum 9999))
            }
            Move-Item -Path $src -Destination $dest -Force
            Write-Host ("   [moved] {0}  ({1} KB)" -f $f, $kb) -ForegroundColor Green
        }
        else {
            Write-Host ("   [would] {0}  ({1} KB)" -f $f, $kb)
        }
    }
    Write-Host ''
}

$mb = [math]::Round($totalBytes / 1MB, 2)
Write-Host ("Total: {0} files, {1} MB" -f $totalCount, $mb) -ForegroundColor Cyan

if (-not $Apply) {
    Write-Host ''
    Write-Host 'Run again with -Apply to move them.' -ForegroundColor Yellow
}
else {
    Write-Host ''
    Write-Host 'Done. Now redeploy so D:\Publish matches:' -ForegroundColor Green
    Write-Host '   cd D:\NUH-PORTAL ; .\NuhPortalDeploy.bat'
    Write-Host ''
    Write-Host 'To restore any file:' -ForegroundColor DarkGray
    Write-Host ("   Copy-Item '{0}\<file>' '{1}\<file>'" -f $archive, $root) -ForegroundColor DarkGray
}
