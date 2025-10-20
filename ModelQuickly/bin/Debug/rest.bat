@echo off
:loop
timeout /t 1 /nobreak >nul
tasklist | find /i "update.exe" >nul && goto loop
xcopy /y /e "%~dp0update_temp\*" "%~dp0" >nul
rd /s /q "%~dp0update_temp"
del /q "%~dp0update.zip" 2>nul
start "" /b "%~dp0TiorraBox.exe"
(del "%~f0")>nul 2>&1