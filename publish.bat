@echo off
echo ============================================
echo   Building VRS DCS Manager - Standalone EXE
echo ============================================
echo.

:: Kill any running instance first
echo Checking for running instances...
taskkill /F /IM DCSLogViewer.exe >nul 2>&1
if %ERRORLEVEL% EQU 0 (
    echo   Killed running DCSLogViewer process.
    timeout /t 2 /nobreak >nul
) else (
    echo   No running instance found.
)

:: Force delete the old EXE if it's locked
if exist "DCSLogViewer\bin\Publish\DCSLogViewer.exe" (
    echo Removing old build...
    del /F /Q "DCSLogViewer\bin\Publish\DCSLogViewer.exe" >nul 2>&1
    if exist "DCSLogViewer\bin\Publish\DCSLogViewer.exe" (
        echo   File still locked. Trying handle close...
        :: Use move-on-reboot trick: rename then delete
        move /Y "DCSLogViewer\bin\Publish\DCSLogViewer.exe" "DCSLogViewer\bin\Publish\DCSLogViewer.exe.old" >nul 2>&1
        del /F /Q "DCSLogViewer\bin\Publish\DCSLogViewer.exe.old" >nul 2>&1
        if exist "DCSLogViewer\bin\Publish\DCSLogViewer.exe" (
            echo.
            echo   ERROR: Cannot delete old EXE. Please close any program that
            echo   might have the file open (Explorer folder window, antivirus, etc.)
            echo   and try again.
            echo.
            pause
            exit /b 1
        )
    )
    echo   Old build removed.
)
echo.

dotnet publish DCSLogViewer\DCSLogViewer.csproj -c Release -p:PublishProfile=Properties\PublishProfiles\SingleFileExe.pubxml

echo.
if %ERRORLEVEL% EQU 0 (
    echo BUILD SUCCESSFUL!
    echo.
    echo Your EXE is at:
    echo   DCSLogViewer\bin\Publish\DCSLogViewer.exe
    echo.
    echo You can copy this single file anywhere and run it.
    echo No .NET installation required on the target machine.
    start "" "DCSLogViewer\bin\Publish"
) else (
    echo BUILD FAILED - check the errors above.
)

pause
