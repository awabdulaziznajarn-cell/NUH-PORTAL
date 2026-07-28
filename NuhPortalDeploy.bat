@echo off
REM ============================================================
REM  NUH-PORTAL deploy script  (ASP.NET Core 8 MVC + Web API)
REM  مبني على نسخة NuPermitApp — مع فروق مهمة:
REM    - مفيش Angular/SPA: الواجهة Razor Views + wwwroot ثابت
REM    - بيبني كمان efbundle.exe لتطبيق ترحيلات EF على السيرفر
REM    - بيتحقق إن ملفات الأسرار مطلعتش مع مخرجات النشر
REM    - بيجهّز حزمة نشر واحدة: app + server-files
REM  عدّل المسارات في قسم الإعدادات لو اتغيّرت.
REM ============================================================
:Begin
setlocal EnableDelayedExpansion
call :setESC
cls

FOR /F "delims=" %%i IN ('powershell -NoProfile -Command "Get-Date -Format yyyy-MM-dd_HH-mm-ss"') DO SET DATETIME=%%i

REM ===================== الإعدادات =====================
set projectName=NUH-PORTAL

set MainProjectPath=C:\Users\awabdulaziz\Desktop\NUH-PORTAL
set outputPath=C:\Users\awabdulaziz\Desktop\Publish

set ApiProjectName=%projectName%
set ApiProjectPath=%MainProjectPath%\%ApiProjectName%
set csprojFile=%ApiProjectPath%\%ApiProjectName%.csproj

REM ملفات السيرفر: appsettings.Production.json + سكربتات SQL + دليل النشر
set serverFilesSource=%outputPath%\_server-files
REM ====================================================

echo Deploy %ESC%[93m[%projectName%]%ESC%[0m  -  ASP.NET Core 8
echo.

REM ---------------------- فحوص أولية ----------------------
where dotnet >nul 2>&1
IF ERRORLEVEL 1 (
  echo %ESC%[91mERROR: dotnet SDK not found in PATH.%ESC%[0m
  GOTO TheEnd
)

IF NOT EXIST "%csprojFile%" (
  echo %ESC%[91mERROR: project file not found:%ESC%[0m
  echo   %csprojFile%
  GOTO TheEnd
)

REM ---------------------- الأسئلة ----------------------
SET /P AspNetAREYOUSURE=Publish the app ([%ESC%[92mY%ESC%[0m]/N), (%ESC%[92mdefault is Yes%ESC%[0m)?
SET /P BuildBundle=Build EF migrations bundle ([%ESC%[92mY%ESC%[0m]/N), (%ESC%[92mdefault is Yes%ESC%[0m)?
SET /P MakeZip=Create ZIP for transfer to server (Y/[%ESC%[92mN%ESC%[0m]), (%ESC%[92mdefault is No%ESC%[0m)?
SET /P selectedAspNetEnvironment=Choose Environment *%ESC%[92mdefault is Production%ESC%[0m* ([%ESC%[93mP=Production%ESC%[0m], [%ESC%[94mD=Development%ESC%[0m])?

echo #################################
IF /I "%selectedAspNetEnvironment%" EQU "D" (
  ECHO Selected Environment [%ESC%[94mDevelopment%ESC%[0m] --------------------------
  set AspNetEnvironment=Development
) ELSE (
  ECHO Selected Environment [%ESC%[93mProduction%ESC%[0m] --------------------------
  set AspNetEnvironment=Production
)

REM حزمة النشر: مجلد واحد فيه كل اللي هيتنقل للسيرفر
set "packagePath=%outputPath%\%projectName%-%DATETIME%-%AspNetEnvironment%"
set "publishPath=%packagePath%\app"
set "serverFilesPath=%packagePath%\server-files"

echo #################################
REM ---------------------- ASP.NET Core publish ----------------------
IF /I "%AspNetAREYOUSURE%" EQU "N" GOTO SkipApi

echo Project : [ %ApiProjectName% ]
echo Path    : [ %ApiProjectPath% ]
cd /d "%ApiProjectPath%"

echo %ESC%[96mStart publishing asp.net core ...........%ESC%[0m
echo %ESC%[95mdotnet publish -c Release -o "%publishPath%" /p:EnvironmentName=%AspNetEnvironment%%ESC%[0m
dotnet publish "%csprojFile%" -c Release -o "%publishPath%" --no-self-contained /p:EnvironmentName=%AspNetEnvironment%
IF ERRORLEVEL 1 (
  echo %ESC%[91mdotnet publish FAILED.%ESC%[0m
  GOTO TheEnd
)
echo %ESC%[92mPublish done.%ESC%[0m

echo.
echo %ESC%[96m--- Verifying publish output ---%ESC%[0m

REM لازم تكون موجودة
call :MustExist "%publishPath%\%projectName%.dll"  "NUH-PORTAL.dll"
call :MustExist "%publishPath%\web.config"         "web.config"
call :MustExist "%publishPath%\appsettings.json"   "appsettings.json - base"
call :MustExist "%publishPath%\wwwroot"            "wwwroot"
call :MustExist "%publishPath%\en"                 "english satellite resources"

REM  ملف الإنتاج لازم يخرج مع النشر — من غيره التطبيق بيرمي عند الإقلاع:
REM    "Database connection string 'DefaultConnection' is missing."
REM  لأن appsettings.json الأساسي فاضي عمدًا (بلا أسرار).
call :MustExist "%publishPath%\appsettings.Production.json" "appsettings.Production.json"

REM لازم تكون غير موجودة — فحوص نظافة
call :MustNotExist "%publishPath%\appsettings.Development.json" "appsettings.Development.json"
call :MustNotExist "%publishPath%\dotnet-tools.json"            "dotnet-tools.json"

:SkipApi

echo #################################
REM ---------------------- EF migrations bundle ----------------------
IF /I "%BuildBundle%" EQU "N" GOTO SkipBundle

IF NOT EXIST "%serverFilesPath%" mkdir "%serverFilesPath%"

echo %ESC%[96mBuilding EF migrations bundle ...........%ESC%[0m
cd /d "%MainProjectPath%"

REM أدوات EF محلية في dotnet-tools.json — لازم restore الأول
call dotnet tool restore
IF ERRORLEVEL 1 (
  echo %ESC%[91mdotnet tool restore FAILED - skipping bundle.%ESC%[0m
  GOTO SkipBundle
)

REM  أدوات EF بتبني الـ host عشان توصل للـ DbContext، و Program.cs بيتحقق
REM  من مفتاح JWT واتصال DB وقت البناء ده. appsettings.Development.json
REM  هو اللي بيوفّرهم، فلازم البيئة تكون Development هنا مهما كان
REM  اختيار بيئة النشر فوق.
set "ASPNETCORE_ENVIRONMENT=Development"

dotnet ef migrations bundle --project "%csprojFile%" --configuration Release --self-contained -r win-x64 --force --output "%serverFilesPath%\efbundle.exe"
IF ERRORLEVEL 1 (
  echo %ESC%[91mEF bundle FAILED.%ESC%[0m
  echo %ESC%[93m  جرّب يدويًا لتشوف الخطأ كامل:%ESC%[0m
  echo   set ASPNETCORE_ENVIRONMENT=Development
  echo   dotnet ef migrations bundle --project "%csprojFile%"
) ELSE (
  echo %ESC%[92mefbundle.exe created.%ESC%[0m
)
set "ASPNETCORE_ENVIRONMENT="

:SkipBundle

echo #################################
REM ---------------------- ملفات السيرفر ----------------------
IF NOT EXIST "%serverFilesSource%" GOTO SkipServerFiles
IF NOT EXIST "%serverFilesPath%" mkdir "%serverFilesPath%"

echo %ESC%[93mCopying server files (appsettings.Production.json, SQL scripts, guide) ...%ESC%[0m
xcopy "%serverFilesSource%\*" "%serverFilesPath%\" /E /I /Y >nul
echo %ESC%[92mServer files copied.%ESC%[0m
:SkipServerFiles

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
echo   app\           -^> copy to  C:\inetpub\NUH-PORTAL  on the server
echo                     ^(appsettings.Production.json is INSIDE - nothing to copy separately^)
echo   server-files\  -^> SQL setup script + efbundle.exe + deployment guide
echo.
echo %ESC%[96mOn the server, in order:%ESC%[0m
echo   1^) first deploy only: run SETUP-DATABASE-ALL-IN-ONE.sql in SSMS
echo   2^) Stop-WebAppPool -Name 'NUH-PORTAL'
echo   3^) copy app\* to C:\inetpub\NUH-PORTAL   ^(overwrites config - edit it in the project, not here^)
echo   4^) Start-WebAppPool -Name 'NUH-PORTAL'
echo   5^) verify: https://housing.nuh.edu.sa/api/Health
echo.
%SystemRoot%\explorer.exe "%packagePath%"

:TheEnd
endlocal
echo.
PAUSE
goto Begin


REM ==================== الدوال المساعدة ====================

:MustExist
REM  التسمية بتتحط في متغيّر الأول — أي قوس جواها بيكسر بلوك الـ IF لو اتكتبت مباشرة
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
