<#
.SYNOPSIS
    メール配信ツールの初回セットアップ（証明書の信頼登録＋インストール）を1コマンドで行う。

.DESCRIPTION
    自己署名証明書（.cer、公開鍵のみ）をカレントユーザーの「信頼できる発行元」
    ストアに登録し、続けてMSIXパッケージをインストールする。
    CurrentUserストアを使うため管理者権限は不要（要件定義書2章のとおり
    1人1台・個別インストールという運用に合わせている）。

.EXAMPLE
    .\install-for-user.ps1 -CerPath .\ESC.cer -MsixPath .\MailDeliveryTool_0.1.0.0_x64.msix
#>
param(
    [Parameter(Mandatory = $true)]
    [string]$CerPath,

    [Parameter(Mandatory = $true)]
    [string]$MsixPath
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $CerPath)) {
    throw "証明書ファイルが見つかりません: $CerPath"
}
if (-not (Test-Path $MsixPath)) {
    throw "MSIXパッケージが見つかりません: $MsixPath"
}

# ZIPをダウンロード・展開したファイルには「インターネットからのファイル」の
# マークが付いており、これが原因でImport-Certificate/Add-AppxPackageが
# 不可解な警告やブロックを起こすことがある。事前に解除しておく（無害な操作）。
Unblock-File -Path $CerPath -ErrorAction SilentlyContinue
Unblock-File -Path $MsixPath -ErrorAction SilentlyContinue

Write-Host "1/2 証明書を信頼済み発行元として登録します（現在のユーザーのみ、管理者権限不要）..."
Import-Certificate -FilePath $CerPath -CertStoreLocation Cert:\CurrentUser\TrustedPeople | Out-Null

Write-Host "2/2 メール配信ツールをインストールします..."
Add-AppxPackage -Path $MsixPath

Write-Host "完了しました。スタートメニューから「MailDeliveryTool」を起動できます。"
