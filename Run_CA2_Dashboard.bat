@echo off
cd /d "%~dp0"
echo =========================================
echo CA2 Dam Valve Monitor
 echo Open http://localhost:5188 in your browser.
echo Your MQTT connection results appear here.
echo =========================================
dotnet run --project "CA2\CA2.csproj" --no-launch-profile
pause
