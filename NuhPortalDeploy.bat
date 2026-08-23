@echo off
REM ============================================================
REM  NUH-PORTAL deploy script  -  ASP.NET Core 8 (Web API + static wwwroot)
REM
REM  THIS SCRIPT RUNS ON THE SERVER (10.100.100.21).
REM  All paths below are server paths, not developer-machine paths.
REM
REM  Requires on this machine:
REM    - .NET SDK 8.x                 -> dotnet publish
REM    - ASP.NET Core Hosting Bundle  -> AspNetCoreModuleV2 for IIS
REM  Visual Studio is NOT required.
REM
REM  What it does, in order:
REM    1) dotnet publish  ->  D:\Deploy\NUH-PORTAL-<stamp>-<env>\app
REM    2) verifies the publish output (config present, secrets absent)
REM    3) optional: builds efbundle.exe for EF migrations
REM    4) optional: backs up the live site, drops app_offline.htm in
REM       D:\Publish, copies app\* over it, then removes app_offline.htm
REM
REM  الخطوة 4 كانت بتوقّف الـ app pool. ساعتها IIS بيردّ 503 من نفسه بصفحته
REM  البيضاء "Service Unavailable" قبل ما التطبيق يشتغل، فمفيش أي طريقة
REM  نعرض بيها صفحة صيانة بشكل النظام. app_offline.htm بيحلّ الاتنين مرة
REM  واحدة: بيقفل التطبيق بهدوء (فتنفتح ملفات الـ DLL للنسخ) وبيرجّع محتوى
REM  الصفحة دي لأي طلب بحالة 503 لحد ما نمسحها.
REM
REM  The copy in step 4 does NOT delete existing files, so the live
REM  folders  keys\  and  logs\  survive a deploy. That is intentional:
REM  wiping keys\ invalidates every DataProtection-encrypted value.
REM ============================================================
:Begin
setlocal EnableDelayedExpansion
call :setESC
cls

FOR /F "delims=" %%i IN ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HH-mm-ss"') DO SET DATETIME=%%i

REM ===================== SETTINGS =====================
set projectName=NUH-PORTAL

REM Git working copy on this server
set MainProjectPath=D:\NUH-PORTAL

REM Build packages and site backups live here. NOT the live site.
set stagingRoot=D:\Deploy

REM Live IIS site physical path
set siteRoot=D:\Publish

REM  صفحة الصيانة الرسمية. الأصل هنا، والسكربت بينسخها لجذر النشر قبل ما
REM  يبدأ النسخ وبيمسحها بعده. متعمَّد إنها برّه مجلد المشروع عشان
REM  dotnet publish ما يشيلهاش معاه للنشر بالغلط.
set maintenancePage=%MainProjectPath%\_maintenance\app_offline.htm

REM Fallback IIS application pool name, used only if auto-detection below fails.
REM Do not rely on this being right - it was wrong once already. The script asks
REM IIS which pool actually serves siteRoot instead of trusting this value.
set fallbackAppPool=Housing

REM Health endpoint used for the post-deploy check
set healthUrl=https://housing.nuh.edu.sa/api/Health

set ApiProjectName=%projectName%
set ApiProjectPath=%MainProjectPath%\%ApiProjectName%
set csprojFile=%ApiProjectPath%\%ApiProjectName%.csproj

REM Extra files shipped beside the app (SQL scripts, deployment guide).
set serverFilesSource=%ApiProjectPath%\deployment
REM ====================================================

echo Deploy %ESC%[93m[%projectName%]%ESC%[0m  -  ASP.NET Core 8
echo   repo : %MainProjectPath%
echo   site : %siteRoot%
echo.

REM ---------------------- preflight ----------------------
where dotnet >nul 2>&1
IF ERRORLEVEL 1 (
  echo %ESC%[91mERROR: dotnet SDK not found in PATH.%ESC%[0m
  echo   Install dotnet-sdk-8.0.423-win-x64.exe from D:\prog, then open a NEW cmd window.
  GOTO TheEnd
)

IF NOT EXIST "%csprojFile%" (
  echo %ESC%[91mERROR: project file not found:%ESC%[0m
  echo   %csprojFile%
  GOTO TheEnd
)

IF NOT EXIST "%ApiProjectPath%\appsettings.Production.json" (
  echo %ESC%[91mERROR: appsettings.Production.json is missing from the project.%ESC%[0m
  echo   Expected: %ApiProjectPath%\appsettings.Production.json
  echo   It is git-ignored on purpose, so a fresh clone never has it.
  echo   Without it the app starts against the WRONG domain and database.
  GOTO TheEnd
)

IF NOT EXIST "%stagingRoot%" mkdir "%stagingRoot%"

set isAdmin=0
net session >nul 2>&1
IF NOT ERRORLEVEL 1 set isAdmin=1

REM ---------------------- detect the IIS application pool ----------------------
REM  Ask IIS which application serves siteRoot, then which pool that application
REM  runs under. Hardcoding the name breaks silently: stop/start just fail and the
REM  files get copied while the app is still running and holding the DLLs.
set "appPoolName="
set "detectedApp="
FOR /F "usebackq delims=" %%a IN (`%windir%\system32\inetsrv\appcmd.exe list vdir /physicalPath:"%siteRoot%" /text:APP.NAME 2^>nul`) DO set "detectedApp=%%a"
IF DEFINED detectedApp (
  FOR /F "usebackq delims=" %%p IN (`%windir%\system32\inetsrv\appcmd.exe list app "!detectedApp!" /text:applicationPool 2^>nul`) DO set "appPoolName=%%p"
)
IF NOT DEFINED appPoolName set "appPoolName=%fallbackAppPool%"

IF DEFINED detectedApp (
  echo   app pool : %ESC%[92m!appPoolName!%ESC%[0m   ^(detected from IIS: !detectedApp!^)
) ELSE (
  echo   app pool : %ESC%[93m!appPoolName!%ESC%[0m   ^(fallback - could not detect from IIS^)
)
echo.

REM ---------------------- questions ----------------------
SET /P AspNetAREYOUSURE=Publish the app ([%ESC%[92mY%ESC%[0m]/N), (%ESC%[92mdefault is Yes%ESC%[0m)?
SET /P BuildBundle=Build EF migrations bundle (Y/[%ESC%[92mN%ESC%[0m]), (%ESC%[92mdefault is No%ESC%[0m)?
SET /P DeployToSite=Deploy to the live IIS site ([%ESC%[92mY%ESC%[0m]/N), (%ESC%[92mdefault is Yes%ESC%[0m)?
SET /P MakeZip=Create ZIP for transfer (Y/[%ESC%[92mN%ESC%[0m]), (%ESC%[92mdefault is No%ESC%[0m)?
SET /P selectedAspNetEnvironment=Choose Environment *%ESC%[92mdefault is Production%ESC%[0m* ([%ESC%[93mP=Production%ESC%[0m], [%ESC%[94mD=Development%ESC%[0m])?

echo #################################
IF /I "%selectedAspNetEnvironment%" EQU "D" (
  ECHO Selected Environment [%ESC%[94mDevelopment%ESC%[0m] --------------------------
  set AspNetEnvironment=Development
) ELSE (
  ECHO Selected Environment [%ESC%[93mProduction%ESC%[0m] --------------------------
  set AspNetEnvironment=Production
)

IF /I NOT "%DeployToSite%" EQU "N" IF "%isAdmin%" EQU "0" (
  echo %ESC%[91mERROR: deploying to IIS needs an elevated prompt.%ESC%[0m
  echo   Close this window, right-click cmd.exe -^> Run as administrator, re-run the script.
  GOTO TheEnd
)

IF /I "%DeployToSite%" EQU "N" GOTO PoolCheckDone
%windir%\system32\inetsrv\appcmd.exe list apppool "!appPoolName!" >nul 2>&1
IF NOT ERRORLEVEL 1 GOTO PoolCheckDone
echo %ESC%[91mERROR: application pool [!appPoolName!] does not exist.%ESC%[0m
echo   These are the pools IIS actually has:
%windir%\system32\inetsrv\appcmd.exe list apppool
echo   Put the right name in  set fallbackAppPool=...  at the top of this script.
GOTO TheEnd
:PoolCheckDone

set "packagePath=%stagingRoot%\%projectName%-%DATETIME%-%AspNetEnvironment%"
set "publishPath=%packagePath%\app"
set "serverFilesPath=%packagePath%\server-files"

echo #################################
REM ---------------------- publish ----------------------
IF /I "%AspNetAREYOUSURE%" EQU "N" GOTO SkipApi

echo Project : [ %ApiProjectName% ]
echo Path    : [ %ApiProjectPath% ]
cd /d "%ApiProjectPath%"

echo %ESC%[96mStart publishing asp.net core ...........%ESC%[0m
dotnet publish "%csprojFile%" -c Release -o "%publishPath%" --no-self-contained /p:EnvironmentName=%AspNetEnvironment%
IF ERRORLEVEL 1 (
  echo %ESC%[91mdotnet publish FAILED.%ESC%[0m
  echo %ESC%[93m  If it failed on NuGet restore, this server has no package feed reachable.%ESC%[0m
  GOTO TheEnd
)
echo %ESC%[92mPublish done.%ESC%[0m

echo.
echo %ESC%[96m--- Verifying publish output ---%ESC%[0m

REM must exist
call :MustExist "%publishPath%\%projectName%.dll"  "NUH-PORTAL.dll"
call :MustExist "%publishPath%\web.config"         "web.config"
call :MustExist "%publishPath%\appsettings.json"   "appsettings.json - base"
call :MustExist "%publishPath%\wwwroot"            "wwwroot"
call :MustExist "%publishPath%\en"                 "english satellite resources"

REM  The base appsettings.json carries no secrets on purpose, so the
REM  environment file has to ship with the app. Without it the app throws
REM  at startup: "Database connection string 'DefaultConnection' is missing."
call :MustExist "%publishPath%\appsettings.%AspNetEnvironment%.json" "appsettings.%AspNetEnvironment%.json"

REM must NOT exist - hygiene checks
call :MustNotExist "%publishPath%\dotnet-tools.json"            "dotnet-tools.json"
IF /I "%AspNetEnvironment%" EQU "Production" call :MustNotExist "%publishPath%\appsettings.Development.json" "appsettings.Development.json"

:SkipApi

echo #################################
REM ---------------------- EF migrations bundle ----------------------
IF /I NOT "%BuildBundle%" EQU "Y" GOTO SkipBundle

IF NOT EXIST "%serverFilesPath%" mkdir "%serverFilesPath%"

echo %ESC%[96mBuilding EF migrations bundle ...........%ESC%[0m
cd /d "%MainProjectPath%"

REM EF tools are pinned in .config\dotnet-tools.json - restore them first
call dotnet tool restore
IF ERRORLEVEL 1 (
  echo %ESC%[91mdotnet tool restore FAILED - skipping bundle.%ESC%[0m
  GOTO SkipBundle
)

REM  The EF tools build the host to reach the DbContext, and Program.cs
REM  validates the JWT key and the connection string during that build.
REM  appsettings.Development.json is what supplies them, so the environment
REM  must be Development here regardless of the deployment environment above.
set "ASPNETCORE_ENVIRONMENT=Development"

dotnet ef migrations bundle --project "%csprojFile%" --configuration Release --self-contained -r win-x64 --force --output "%serverFilesPath%\efbundle.exe"
IF ERRORLEVEL 1 (
  echo %ESC%[91mEF bundle FAILED.%ESC%[0m
  echo %ESC%[93m  Run it manually to see the full error:%ESC%[0m
  echo   set ASPNETCORE_ENVIRONMENT=Development
  echo   dotnet ef migrations bundle --project "%csprojFile%"
) ELSE (
  echo %ESC%[92mefbundle.exe created.%ESC%[0m
)
set "ASPNETCORE_ENVIRONMENT="

:SkipBundle

echo #################################
REM ---------------------- server files ----------------------
IF NOT EXIST "%serverFilesSource%" GOTO SkipServerFiles
IF NOT EXIST "%serverFilesPath%" mkdir "%serverFilesPath%"

echo %ESC%[93mCopying server files (SQL scripts, deployment guide) ...%ESC%[0m
xcopy "%serverFilesSource%\*" "%serverFilesPath%\" /E /I /Y >nul
echo %ESC%[92mServer files copied.%ESC%[0m
:SkipServerFiles

echo #################################
REM ---------------------- deploy to IIS ----------------------
IF /I "%DeployToSite%" EQU "N" GOTO SkipDeploy
IF NOT EXIST "%publishPath%\%projectName%.dll" (
  echo %ESC%[91mNothing to deploy - publish output not found.%ESC%[0m
  GOTO SkipDeploy
)

set "backupPath=%stagingRoot%\_backup-%DATETIME%"
echo %ESC%[96mBacking up live site  -^>  %backupPath%%ESC%[0m
mkdir "%backupPath%"
xcopy "%siteRoot%\*" "%backupPath%\" /E /I /Y /Q >nul

REM  ---- تحويل الموقع لوضع الصيانة ----
REM  لو الصفحة مش موجودة لأي سبب، بنرجع للسلوك القديم (إيقاف الـ app pool)
REM  بدل ما ننسخ فوق موقع شغّال — النسخ ساعتها بيفشل على ملفات DLL مقفولة.
set "usedOfflinePage=0"
IF EXIST "%maintenancePage%" (
  echo %ESC%[96mTaking site offline  -^>  %siteRoot%\app_offline.htm%ESC%[0m
  copy /Y "%maintenancePage%" "%siteRoot%\app_offline.htm" >nul
  IF NOT ERRORLEVEL 1 set "usedOfflinePage=1"
)
IF "!usedOfflinePage!" EQU "1" (
  REM  مهلة عشان التطبيق يقفل ويسيب ملفاته قبل النسخ
  powershell -NoProfile -Command "Start-Sleep -Seconds 5"
) ELSE (
  echo %ESC%[93mMaintenance page not found at %maintenancePage%%ESC%[0m
  echo %ESC%[93mFalling back to stopping the app pool - users will see the bare IIS 503.%ESC%[0m
  echo %ESC%[96mStopping app pool [!appPoolName!] ...%ESC%[0m
  "%windir%\system32\inetsrv\appcmd.exe" stop apppool /apppool.name:"!appPoolName!"
  powershell -NoProfile -Command "Start-Sleep -Seconds 4"
)

set "poolStoppedForRetry=0"
echo %ESC%[96mCopying app  -^>  %siteRoot%%ESC%[0m
xcopy "%publishPath%\*" "%siteRoot%\" /E /I /Y >nul
IF NOT ERRORLEVEL 1 GOTO CopyDone

REM  ---- محاولة تانية بإيقاف الـ app pool ----
REM  ⚠️ صفحة الصيانة لوحدها بتخلّي ASP.NET Core يقفل التطبيق ويسيب ملفاته،
REM     وده بيكفي في الحالة العادية. لكن أحيانًا بيفضل هاندل مفتوح على ملف
REM     (ماسح فيروسات بيقراه، أو w3wp اتأخر في الإغلاق) فالنسخ بيفشل.
REM  ⚠️ وكان السكربت وقتها بيقف ويسيب الموقع مطفي بنصف نشر مستنّي تدخّل يدوي.
REM     دلوقتي بيوقف الـ pool - وده بيقفل العملية بالقوة ويفكّ أي قفل - ويعيد
REM     النسخ مرة واحدة. الزائر بيشوف صفحة IIS البيضا في الثواني دي بدل صفحة
REM     الصيانة، وده أرخص من موقع واقف لحد ما حد ياخد باله.
REM  ⚠️ والمنطق هنا مسطّح بـ GOTO عن قصد لا IF متداخلة. السبب إن قراءة نتيجة
REM     أمر جوّه قوس في batch فخّ معروف: %ERRORLEVEL% بتتبدّل وقت **قراءة**
REM     الكتلة كلها لا وقت تنفيذ السطر، فبترجع قيمة قديمة. (الشكل
REM     IF ERRORLEVEL n سليم جوّه القوس، لكن التفرقة بين الشكلين رفيعة
REM     والباج بيبقى صامت.) والمسار ده بيتنفّذ يوم ما النشر يفشل - يعني
REM     أسوأ يوم عشان نكتشف فيه إن الشرط كان بيتقري غلط.
echo %ESC%[93mCopy failed - a file is still locked. Stopping app pool and retrying once...%ESC%[0m
"%windir%\system32\inetsrv\appcmd.exe" stop apppool /apppool.name:"!appPoolName!"
set "poolStoppedForRetry=1"
powershell -NoProfile -Command "Start-Sleep -Seconds 4"
xcopy "%publishPath%\*" "%siteRoot%\" /E /I /Y >nul
IF NOT ERRORLEVEL 1 GOTO CopyDone

REM  متعمَّد: مابنشيلش صفحة الصيانة هنا. الموقع نصّه منسوخ، فإظهاره بنصف
REM  نشر أسوأ من إبقائه على صفحة صيانة مفهومة لحد ما تتصرّف.
echo %ESC%[91mCopy FAILED twice - the site is still offline.%ESC%[0m
echo %ESC%[91mRestore from %backupPath%%ESC%[0m
REM  الرسالة مشروطة: في المسار الاحتياطي مافيش app_offline.htm أصلًا، وتوجيه
REM  حد يمسح ملف مش موجود وسط عطل بيضيّع وقت في اللحظة الغلط.
IF "!usedOfflinePage!" EQU "1" echo %ESC%[91mthen delete %siteRoot%\app_offline.htm to bring the site back.%ESC%[0m
echo %ESC%[91mNOTE: the app pool [!appPoolName!] is STOPPED - start it after you restore.%ESC%[0m
GOTO SkipDeploy

:CopyDone
IF "!usedOfflinePage!" EQU "1" (
  echo %ESC%[96mBringing site back online  -^>  removing app_offline.htm%ESC%[0m
  del /F /Q "%siteRoot%\app_offline.htm" >nul 2>&1
)
REM  ⚠️ الـ pool بيترجع لو السكربت وقّفه - سواء في المسار الاحتياطي من الأول
REM     (صفحة الصيانة مش موجودة) أو في المحاولة التانية فوق. الشرطان منفصلان
REM     عن قصد: ممكن نكون استعملنا صفحة الصيانة **و** وقّفنا الـ pool في
REM     المحاولة التانية، وساعتها لازم نمسح الصفحة **و** نشغّل الـ pool.
IF "!usedOfflinePage!" NEQ "1" set "poolStoppedForRetry=1"
IF "!poolStoppedForRetry!" EQU "1" (
  echo %ESC%[96mStarting app pool [!appPoolName!] ...%ESC%[0m
  "%windir%\system32\inetsrv\appcmd.exe" start apppool /apppool.name:"!appPoolName!"
)

powershell -NoProfile -Command "Start-Sleep -Seconds 3"

echo %ESC%[96mHealth check: %healthUrl%%ESC%[0m
REM  ⚠️ كان بيستخدم Invoke-WebRequest، وبيفشل دايمًا على السيرفر ده برسالة
REM     "The underlying connection was closed" — مشكلة في مصافحة TLS من داخل
REM     .NET Framework، مش في الموقع. curl.exe (المدمج في ويندوز) بيعمل TLS
REM     بنفسه وبيرجّع الرد سليم. اتأكد بالتجربة على نفس السيرفر.
curl.exe -k -sS -m 25 "%healthUrl%"
echo.

REM ---------------------- prune old deploy folders ----------------------
REM  Every run leaves TWO full copies behind: the publish output
REM  (NUH-PORTAL-<stamp>-<env>) and a copy of the live site (_backup-<stamp>).
REM  Nothing ever deleted them, so D:\Deploy reached 60 folders / several GB.
REM  Keep the newest few of each and drop the rest.
REM
REM  Runs only after a SUCCESSFUL copy: a failed copy jumps to :SkipDeploy
REM  above, so the backups stay untouched exactly when they are needed.
REM  Sorting is by folder name, which is the yyyy-MM-dd_HH-mm-ss stamp,
REM  so newest-first is a plain descending sort.
set keepDeployFolders=3
echo %ESC%[96mPruning old deploy folders in %stagingRoot% ^(keeping newest %keepDeployFolders% of each^) ...%ESC%[0m
powershell -NoProfile -Command "$k=%keepDeployFolders%; $removed=0; foreach($pat in @('_backup-*','%projectName%-*-Production')){ $old = Get-ChildItem -Path '%stagingRoot%' -Directory -Filter $pat -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -Skip $k; foreach($d in $old){ try { Remove-Item $d.FullName -Recurse -Force -ErrorAction Stop; $removed++ } catch { Write-Host ('  could not remove ' + $d.Name) } } }; Write-Host ('  removed ' + $removed + ' old folder(s)')"

:SkipDeploy

echo #################################
REM ---------------------- ZIP ----------------------
IF /I NOT "%MakeZip%" EQU "Y" GOTO SkipZip
echo %ESC%[96mCreating ZIP ...........%ESC%[0m
powershell -NoProfile -Command "Compress-Archive -Path '%packagePath%\*' -DestinationPath '%packagePath%.zip' -Force"
IF ERRORLEVEL 1 (
  echo %ESC%[91mZIP creation FAILED.%ESC%[0m
) ELSE (
  echo %ESC%[92mZIP: %packagePath%.zip%ESC%[0m
)
:SkipZip

echo.
echo ############### Done ##################
echo Package: %ESC%[93m%packagePath%%ESC%[0m
echo.
echo   app\           -^> the deployable application
echo   server-files\  -^> SQL scripts, efbundle.exe, deployment guide
echo.
echo %ESC%[96mIf you answered N to the deploy question, do it manually:%ESC%[0m
echo   1^) copy /Y "%maintenancePage%" "%siteRoot%\app_offline.htm"      ^(site goes offline^)
echo   2^) wait ~5 seconds, then: xcopy "%publishPath%\*" "%siteRoot%\" /E /I /Y
echo   3^) del "%siteRoot%\app_offline.htm"                                ^(site comes back^)
echo   4^) verify: %healthUrl%
echo.
echo %ESC%[96mTo put the site under maintenance at any other time:%ESC%[0m
echo   copy /Y "%maintenancePage%" "%siteRoot%\app_offline.htm"     ^(offline^)
echo   del "%siteRoot%\app_offline.htm"                             ^(online^)
echo.
IF EXIST "%packagePath%" %SystemRoot%\explorer.exe "%packagePath%"

:TheEnd
endlocal
echo.
PAUSE
goto Begin


REM ==================== helpers ====================

:MustExist
REM  the label goes into a variable first - parentheses inside it would
REM  otherwise break the surrounding IF block
set "_lbl=%~2"
IF EXIST %1 (
  echo   %ESC%[92m[ OK ]%ESC%[0m   !_lbl!
) ELSE (
  echo   %ESC%[91m[MISS]%ESC%[0m   !_lbl!  -- expected but not found^!
)
exit /B 0

:MustNotExist
set "_lbl=%~2"
IF EXIST %1 (
  echo   %ESC%[93m[EXTRA]%ESC%[0m  !_lbl!  -- not needed on the server, safe to delete
) ELSE (
  echo   %ESC%[92m[ OK ]%ESC%[0m   !_lbl! excluded
)
exit /B 0

:setESC
for /F "tokens=1,2 delims=#" %%a in ('"prompt #$H#$E# & echo on & for %%b in (1) do rem"') do (
  set ESC=%%b
  exit /B 0
)
exit /B 0
