@echo off
taskkill /F /IM Lumo.Editor.exe 2>nul
timeout /t 1 /nobreak >nul
"C:\Program Files\dotnet\dotnet.exe" build "C:\Users\User\Pictures\lumo\LumoEngine.sln"
