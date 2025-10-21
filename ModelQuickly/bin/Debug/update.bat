@echo off
setlocal

rem 设置当前目录
set "current_dir=%~dp0"

rem 设置压缩包路径
set "zip_file=%current_dir%ModelQuickly.zip"

rem 检查压缩包是否存在
if not exist "%zip_file%" (
    echo 文件 "%zip_file%" 不存在。
    pause
    goto :eof
)

rem 使用 PowerShell 解压
powershell -NoProfile -Command "Expand-Archive -Path '%zip_file%' -DestinationPath '%current_dir%' -Force"

rem 检查解压是否成功
if %errorlevel% neq 0 (
    echo 解压失败。
    pause
    goto :eof
)

rem 删除压缩包
del "%zip_file%"
start "" "%~dp0ModelQuickly.exe"
echo 解压并删除完成。
pause