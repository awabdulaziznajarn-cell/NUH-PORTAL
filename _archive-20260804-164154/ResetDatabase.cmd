@echo off
REM ============================================================
REM  NUH-PORTAL - rebuild NUH_DB from scratch
REM
REM  DESTRUCTIVE: drops the database and recreates it from the EF
REM  migration in the branch that is currently checked out.
REM
REM  Why this is needed: the current branch ships a single migration
REM  (20260723121858_InitialCreate) while the existing database was
REM  built from a different one (20260720120109_initalDb). Running
REM  "dotnet ef database update" against it fails on CREATE TABLE
REM  because the tables already exist.
REM
REM  Run from an ELEVATED command prompt.
REM ============================================================
setlocal EnableDelayedExpansion

set SQLINSTANCE=.\SQLEXPRESS
set DBNAME=NUH_DB
set SQLLOGIN=svc.nuh
set SQLPASS=aPCWLk1_RpLiNy2+11pji9W_
set REPO=D:\NUH-PORTAL
set PROJ=%REPO%\NUH-PORTAL\NUH-PORTAL.csproj

REM appsettings.Production.json is what supplies the connection string
set ASPNETCORE_ENVIRONMENT=Production

cls
echo ============================================================
echo   REBUILD DATABASE
echo ------------------------------------------------------------
echo   instance : %SQLINSTANCE%
echo   database : %DBNAME%          ^<-- WILL BE DROPPED
echo   project  : %PROJ%
echo   env      : %ASPNETCORE_ENVIRONMENT%
echo ------------------------------------------------------------
echo   Every row in %DBNAME% will be lost. There is no undo.
echo ============================================================
echo.
set /P CONFIRM=Type YES (capitals) to continue:
if /I not "%CONFIRM%"=="YES" goto :Abort

REM ---------------------- preflight ----------------------
where sqlcmd >nul 2>&1
IF ERRORLEVEL 1 (
  echo [ERROR] sqlcmd not found in PATH.
  echo         Install SSMS from D:\prog\SSMS-Setup-ENU.exe, or run the SQL
  echo         steps below by hand in SQL Server Management Studio.
  goto :TheEnd
)

where dotnet >nul 2>&1
IF ERRORLEVEL 1 (
  echo [ERROR] dotnet SDK not found in PATH. Open a new cmd window and retry.
  goto :TheEnd
)

IF NOT EXIST "%PROJ%" (
  echo [ERROR] project not found: %PROJ%
  goto :TheEnd
)

echo.
echo [1/6] Current migration history (before)
sqlcmd -S %SQLINSTANCE% -E -b -h-1 -Q "IF DB_ID('%DBNAME%') IS NULL PRINT '  (database does not exist)' ELSE SELECT '  ' + MigrationId FROM [%DBNAME%].dbo.__EFMigrationsHistory"

echo.
echo [2/6] Dropping %DBNAME%
sqlcmd -S %SQLINSTANCE% -E -b -Q "IF DB_ID('%DBNAME%') IS NOT NULL BEGIN ALTER DATABASE [%DBNAME%] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [%DBNAME%]; END"
IF ERRORLEVEL 1 (
  echo [ERROR] drop failed. Stop the IIS app pool first:
  echo         "%%windir%%\system32\inetsrv\appcmd.exe" stop apppool /apppool.name:"NUH-PORTAL"
  goto :TheEnd
)
echo   dropped.

echo.
echo [3/6] Making sure the login [%SQLLOGIN%] exists and can create databases
sqlcmd -S %SQLINSTANCE% -E -b -Q "IF SUSER_ID('%SQLLOGIN%') IS NULL CREATE LOGIN [%SQLLOGIN%] WITH PASSWORD = '%SQLPASS%', CHECK_POLICY = OFF; ALTER SERVER ROLE dbcreator ADD MEMBER [%SQLLOGIN%];"
IF ERRORLEVEL 1 (
  echo [ERROR] could not create or grant the login.
  goto :TheEnd
)
echo   ok.

echo.
echo [4/6] Testing SQL authentication as %SQLLOGIN%
sqlcmd -S %SQLINSTANCE% -U %SQLLOGIN% -P "%SQLPASS%" -b -Q "SELECT 1" >nul 2>&1
IF ERRORLEVEL 1 (
  echo [ERROR] cannot log in as %SQLLOGIN% with a password.
  echo         The instance is probably set to Windows Authentication only.
  echo         Fix: SSMS -^> right-click the server -^> Properties -^> Security
  echo              -^> "SQL Server and Windows Authentication mode" -^> OK
  echo              -^> then restart the SQL Server ^(SQLEXPRESS^) service.
  goto :TheEnd
)
echo   ok.

echo.
echo [5/6] Restoring EF tools and applying migrations
cd /d "%REPO%"
call dotnet tool restore
IF ERRORLEVEL 1 (
  echo [ERROR] dotnet tool restore failed - check .config\dotnet-tools.json
  goto :TheEnd
)

dotnet ef database update --project "%PROJ%"
IF ERRORLEVEL 1 (
  echo [ERROR] database update failed - read the message above.
  goto :TheEnd
)
echo   migrations applied.

echo.
echo [6/6] Migration history (after)
sqlcmd -S %SQLINSTANCE% -E -b -h-1 -Q "SELECT '  ' + MigrationId FROM [%DBNAME%].dbo.__EFMigrationsHistory"
echo.
echo   Table count:
sqlcmd -S %SQLINSTANCE% -E -b -h-1 -Q "SELECT '  ' + CAST(COUNT(*) AS varchar) + ' tables' FROM [%DBNAME%].sys.tables"

echo.
echo ============================================================
echo   DONE. Next: run NuhPortalDeploy.bat to publish the app.
echo   Reference data (lookups) is seeded by the app on first start.
echo ============================================================
goto :TheEnd

:Abort
echo.
echo Aborted - nothing was changed.

:TheEnd
echo.
endlocal
PAUSE
