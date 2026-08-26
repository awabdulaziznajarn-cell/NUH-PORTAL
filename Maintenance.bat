@echo off
REM ============================================================================
REM  Maintenance.bat - تشغيل/إيقاف وضع الصيانة يدويًّا
REM ============================================================================
REM   الاستعمال:
REM       Maintenance.bat on       الموقع يدخل وضع الصيانة
REM       Maintenance.bat off      الموقع يرجع يشتغل
REM       Maintenance.bat status   يقول الوضع الحالي
REM
REM   ⚠️ ليه السكربت ده موجود:
REM      وضع الصيانة كان بيتعمل بـ Stop-WebAppPool، وده **بيلغي** صفحة الصيانة
REM      بدل ما يعرضها. السبب معماري لا عطل:
REM
REM        • الـ app pool شغّال + app_offline.htm موجود
REM              -> الطلب بيوصل لـ IIS، وASP.NET Core Module بيرد بمحتوى
REM                 الملف بحالة 503. الزائر بيشوف صفحة الصيانة بتاعتنا.
REM
REM        • الـ app pool واقف
REM              -> الطلب بيترفض في http.sys على مستوى ويندوز، قبل IIS وقبل
REM                 التطبيق. مفيش أي إعداد في web.config ولا في الموقع يقدر
REM                 يتدخّل. الزائر بيشوف صفحة ويندوز البيضا "Service
REM                 Unavailable" مهما كان شكل صفحتنا.
REM
REM      يعني: عشان الزائر يشوف صفحة الصيانة، الـ pool لازم يفضل **شغّال**.
REM      وde مش تنازل عن الأمان - وجود app_offline.htm لوحده بيخلّي
REM      ASP.NET Core يقفل التطبيق ويسيب أقفال ملفات الـ DLL، وهو الغرض
REM      الأصلي من الملف.
REM
REM   ⚠️ السكربت ده للتشغيل اليدوي بس (صيانة قاعدة بيانات، تدخّل طارئ).
REM      النشر العادي مالوش دعوة بيه - NuhPortalDeploy.bat بيعمل نفس الحركة
REM      دي جوّاه.
REM ============================================================================

setlocal EnableDelayedExpansion

set "siteRoot=D:\Publish"
set "maintenancePage=D:\NUH-PORTAL\_maintenance\app_offline.htm"
set "appPoolName=Housing"
set "liveFile=%siteRoot%\app_offline.htm"
set "appcmd=%windir%\system32\inetsrv\appcmd.exe"

REM ---- لازم صلاحيات مدير: appcmd مابيشتغلش من غيرها ----
net session >nul 2>&1
IF ERRORLEVEL 1 (
  echo [ERROR] Run this from an Administrator command prompt.
  goto :End
)

set "action=%~1"
IF "%action%"=="" set "action=status"

IF /I "%action%"=="on"     goto :On
IF /I "%action%"=="off"    goto :Off
IF /I "%action%"=="status" goto :Status
echo [ERROR] Unknown argument "%action%". Use: on ^| off ^| status
goto :End

REM ---------------------------------------------------------------- ON ----
:On
IF NOT EXIST "%maintenancePage%" (
  echo [ERROR] Maintenance page not found: %maintenancePage%
  echo         Without it the site cannot show a styled maintenance page.
  goto :End
)
REM  ⚠️ مزامنة سطر الحقوق من ملف الترجمة قبل النسخ.
REM     صفحة الصيانة يخدمها IIS والتطبيق متوقّف، فنصّها مكتوب داخلها بالضرورة
REM     ولا يصله تعديل SharedResource.resx. السكربت ده بيولّده منه، فيفضل
REM     المصدر واحدًا. فشلُه لا يوقف الصيانة - الصفحة القديمة أفضل من لا صفحة.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0_maintenance\Sync-MaintenanceCopyright.ps1"
IF ERRORLEVEL 1 echo [WARN] Copyright sync failed - the page will show its previous text.

copy /Y "%maintenancePage%" "%liveFile%" >nul
IF ERRORLEVEL 1 (
  echo [ERROR] Could not copy the maintenance page to %liveFile%
  goto :End
)
echo [OK] Maintenance page is in place.

REM  ⚠️ الـ pool لازم يبقى شغّال وإلا الصفحة اللي لسه نسخناها مش هتتعرض أصلًا.
"%appcmd%" start apppool /apppool.name:"%appPoolName%" >nul 2>&1
echo [OK] App pool [%appPoolName%] is running - required for the page to show.
echo.
echo      The site now answers every request with the maintenance page (HTTP 503).
echo      Bring it back with:  Maintenance.bat off
goto :Status

REM --------------------------------------------------------------- OFF ----
:Off
IF EXIST "%liveFile%" (
  del /F /Q "%liveFile%" >nul 2>&1
  IF EXIST "%liveFile%" (
    echo [ERROR] Could not delete %liveFile% - the site is still offline.
    goto :End
  )
  echo [OK] Maintenance page removed.
) ELSE (
  echo [..] No maintenance page was in place.
)
"%appcmd%" start apppool /apppool.name:"%appPoolName%" >nul 2>&1
echo [OK] App pool [%appPoolName%] started.
goto :Status

REM ------------------------------------------------------------ STATUS ----
:Status
echo.
echo ---------------------------------------------
set "poolState=unknown"
REM  ⚠️ /text:state بترجّع الحالة لوحدها (Started / Stopped). من غيرها
REM     appcmd بترجّع السطر كامل ولازم نقصّه بأقواس - وde بيرجّع
REM     "MgdVersion:v4.0,MgdMode:Integrated,state:Started" مش الحالة.
FOR /F "usebackq delims=" %%s IN (`"%appcmd%" list apppool "%appPoolName%" /text:state 2^>nul`) DO set "poolState=%%s"
echo   app pool [%appPoolName%] : !poolState!
IF EXIST "%liveFile%" (
  echo   app_offline.htm         : present
  echo.
  echo   =^> SITE IS IN MAINTENANCE - visitors see the styled page.
) ELSE (
  echo   app_offline.htm         : absent
  echo.
  echo   =^> SITE IS LIVE.
)
echo ---------------------------------------------

:End
endlocal
