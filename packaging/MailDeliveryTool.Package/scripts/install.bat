@echo off
chcp 65001 >nul
setlocal enabledelayedexpansion

set "SCRIPT_DIR=%~dp0"
set "CER_FILE="
set "MSIX_FILE="

for %%f in ("%SCRIPT_DIR%*.cer") do set "CER_FILE=%%~ff"
for %%f in ("%SCRIPT_DIR%*.msix") do set "MSIX_FILE=%%~ff"

if not defined CER_FILE (
    echo [エラー] 証明書ファイル（.cer）が見つかりません。
    echo このファイルと同じフォルダに .cer ファイルを置いてください。
    echo.
    pause
    exit /b 1
)

if not defined MSIX_FILE (
    echo [エラー] インストール用ファイル（.msix）が見つかりません。
    echo このファイルと同じフォルダに .msix ファイルを置いてください。
    echo.
    pause
    exit /b 1
)

echo MailDeliveryTool をインストールします。しばらくお待ちください...
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%install-for-user.ps1" -CerPath "!CER_FILE!" -MsixPath "!MSIX_FILE!"

if errorlevel 1 (
    echo.
    echo [エラー] インストールに失敗しました。上に表示されたメッセージを
    echo 担当者に伝えてください。
) else (
    echo.
    echo インストールが完了しました。このウィンドウは閉じて構いません。
)

echo.
pause
