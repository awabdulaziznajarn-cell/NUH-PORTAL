# =====================================================================
#  مين واخد الذاكرة على السيرفر — قراءة فقط
# =====================================================================
$os = Get-CimInstance Win32_OperatingSystem
$totalMB = [math]::Round($os.TotalVisibleMemorySize/1KB,0)
$freeMB  = [math]::Round($os.FreePhysicalMemory/1KB,0)

Write-Host ""
Write-Host "الذاكرة الكلية : $totalMB MB" -ForegroundColor Cyan
Write-Host "الفاضي         : $freeMB MB   ($([math]::Round($freeMB/$totalMB*100,0))%)" -ForegroundColor Cyan
Write-Host "المستخدم       : $($totalMB-$freeMB) MB" -ForegroundColor Cyan
Write-Host ""
Write-Host "أعلى ١٥ عملية استهلاكًا:" -ForegroundColor Yellow

Get-Process | Sort-Object WorkingSet64 -Descending | Select-Object -First 15 |
  Format-Table @{L='العملية';E={$_.ProcessName}},
               @{L='MB';E={[math]::Round($_.WorkingSet64/1MB,0)};Align='right'},
               @{L='PID';E={$_.Id}} -AutoSize

Write-Host "تجميع حسب النوع:" -ForegroundColor Yellow
Get-Process | Group-Object ProcessName |
  Select-Object @{L='العملية';E={$_.Name}},
                @{L='عدد';E={$_.Count}},
                @{L='MB';E={[math]::Round((($_.Group | Measure-Object WorkingSet64 -Sum).Sum)/1MB,0)}} |
  Sort-Object MB -Descending | Select-Object -First 10 | Format-Table -AutoSize

Write-Host "عمّال IIS (كل تطبيق وذاكرته):" -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction Stop
    Get-ChildItem IIS:\AppPools | ForEach-Object {
        $pool = $_.Name
        $wp = Get-WmiObject Win32_Process -Filter "Name='w3wp.exe'" |
              Where-Object { $_.CommandLine -match [regex]::Escape($pool) }
        [pscustomobject]@{
            'التطبيق' = $pool
            'الحالة'  = $_.State
            'MB'      = if ($wp) { [math]::Round(($wp | Measure-Object WorkingSet -Sum).Sum/1MB,0) } else { 0 }
        }
    } | Format-Table -AutoSize
} catch { Write-Host "  (WebAdministration غير متاح)" -ForegroundColor DarkGray }
