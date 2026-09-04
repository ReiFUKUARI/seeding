# MSIX パッケージング構成の検討（フェーズ4・成果物④）

要件定義書 12章で配布形式を MSIX と決定済みのため、本書では
「どう構成するか」を具体化し、決定事項と未決事項を切り分ける。

> **Windows実機で `.wapproj` のビルドに成功し、`artifacts/msix/` に
> パッケージが生成されることを確認済みです。** 実機でしか再現しない
> 環境依存のエラー（`APPX3217` / `NETSDK1112` 等）を複数踏んだため、
> 「5. ビルド手順」に対処法をまとめてあります。躓いたら先にそちらを
> 確認してください。

---

## 1. 方式の選定

WPF アプリを MSIX 化する方法は3つある。

| 方式 | 内容 | 採否 |
|---|---|---|
| **Windows Application Packaging Project（.wapproj）** | パッケージ専用プロジェクトをソリューションに追加し、WPF プロジェクトを参照する | **採用** |
| 単一プロジェクト MSIX | `.csproj` に `WindowsPackageType=MSIX` 等を指定する | 不採用 |
| MSIX Packaging Tool | 既存のインストーラーを GUI で変換する | 不採用 |

### 採用理由

- **.wapproj** はビルド成果物（MSIX）とアプリ本体を別プロジェクトに分離できるため、
  開発中は WPF プロジェクトを F5 で直接起動し、配布時のみパッケージをビルドする、
  という切り替えが素直にできる。
- 単一プロジェクト MSIX は本来 WinUI 3 / Windows App SDK 向けの構成で、
  WPF での採用例が少なく、情報も乏しい。
- MSIX Packaging Tool は「既存の MSI/EXE インストーラーを持っている」ことが前提。
  今回は新規開発でインストーラー自体が存在しないため、経由する意味がない。

構成ファイルは `packaging/MailDeliveryTool.Package/` に配置済み。

---

## 2. マニフェストの構成方針

`Package.appxmanifest` の要点。

### Capability は `runFullTrust` のみ

WPF は Win32 アプリのため `rescap:runFullTrust` が必須。
そして**この1つで本アプリに必要な操作はすべて賄える**。

| 必要な操作 | 追加 capability |
|---|---|
| SMTP 送信（587番ポートへの外向き通信） | 不要 |
| 添付ファイルの選択（ファイルダイアログ経由） | 不要 |
| ドキュメントフォルダへのCSVバックアップ書き込み | 不要 |
| DPAPI による認証情報の暗号化 | 不要 |

`broadFileSystemAccess` や `documentsLibrary` は宣言**しない**。
ユーザーがダイアログで明示的に選んだファイル・フォルダへのアクセスは
`runFullTrust` の範囲で可能であり、過剰な権限宣言は社内配布審査で不利になる。

### Identity / Publisher（確定済み）

`Publisher` は**署名証明書のサブジェクト（CN）と完全一致**していなければ
インストールが失敗する。[D-009](./decisions.md#d-009-msix署名方式publisher名確定)
のとおり `CN=ESC` で確定した。証明書のSubjectもこれと一字一句合わせること。

---

## 3. 署名と配布（確定済み）

MSIX は**署名が必須**で、かつ**その証明書がインストール先PCで信頼されている**必要がある。

### 方式：自己署名証明書（社内管理）

社内に署名方式の具体的なルールはなく「セキュリティが担保できていればよい」
という方針のみのため、**手間が最も少ない自己署名証明書**を採用する
（公的証明書は年額コスト・更新手続きが発生し、配布先が社内の営業担当者のみ
という要件定義書 2章の前提では見合わない）。

証明書ファイル（`.pfx`）は**リポジトリに含めない**（`.gitignore` で除外済み）。

### 証明書の発行（初回のみ・担当者が1回実行）

以下は署名鍵を管理する担当者が、自分のPCで1回だけ実行する。
**秘密鍵（`.pfx`）は絶対にリポジトリにコミットしない。**

```powershell
# 証明書を生成する（有効期限10年。更新の手間を減らすため長めに設定）
$cert = New-SelfSignedCertificate `
  -Type Custom `
  -Subject "CN=ESC" `
  -KeyUsage DigitalSignature `
  -FriendlyName "MailDeliveryTool code signing" `
  -CertStoreLocation "Cert:\CurrentUser\My" `
  -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
  -NotAfter (Get-Date).AddYears(10)

# 秘密鍵付き .pfx をエクスポートする（署名・ビルド時に使う。安全な場所に保管し、
# 誰がどこで保管するかを決めておくこと）
$pwd = Read-Host -AsSecureString "PFXのパスワードを入力"
Export-PfxCertificate -Cert $cert -FilePath .\ESC-codesigning.pfx -Password $pwd

# 公開鍵のみの .cer をエクスポートする（配布用。各PCへ配る）
Export-Certificate -Cert $cert -FilePath .\ESC.cer
```

### 配布とインストール（手間を最小化）

`.cer`（公開鍵）の信頼登録と`.msix`のインストールをまとめてできるよう、
セットアップスクリプトを用意した。**管理者権限は不要**
（現在のユーザーの証明書ストアのみを使うため）。

```powershell
# 各PCでの初回セットアップ（証明書の信頼登録＋インストールを1コマンドで）
.\packaging\MailDeliveryTool.Package\scripts\install-for-user.ps1 `
  -CerPath .\ESC.cer `
  -MsixPath .\MailDeliveryTool_0.1.0.0_x64.msix
```

（スクリプトの中身は
[`install-for-user.ps1`](../packaging/MailDeliveryTool.Package/scripts/install-for-user.ps1)）

Windows 11 は、信頼された証明書で署名済みのパッケージであれば既定で
サイドロードできる。うまくいかない場合は
「設定 → プライバシーとセキュリティ → 開発者向け」でサイドロードが
許可されているか確認すること。

### 更新（任意・ファイル共有の場所が決まってから有効化）

`.appinstaller` を使うと、アプリ起動時にファイル共有上の新しいバージョンを
検知して自動更新できる。「営業担当者に更新のたびに手作業を依頼しない」
という手間削減の方針に合うため**有効化を推奨**するが、
**パッケージを置くファイル共有の場所（社内で使っているものでよい）が
決まるまでは保留**にしてある（`.wapproj` には反映していない。無効な
プロパティを入れて未検証のままビルドを壊すリスクを避けるため）。

決まったら `MailDeliveryTool.Package.wapproj` に以下を追加する。

```xml
<GenerateAppInstallerFile>true</GenerateAppInstallerFile>
<AppInstallerUri>\\<ファイル共有のパス>\MailDeliveryTool\</AppInstallerUri>
<HoursBetweenUpdateChecks>24</HoursBetweenUpdateChecks>
```

有効化後は、更新のたびに `Package.appxmanifest` の `Version`
（4桁 `major.minor.build.revision`）を必ず**インクリメント**すること
（同一バージョンでは更新が適用されない）。

---

## 4. MSIX 化に伴う実装上の注意（重要）

### 4.1 アプリデータはアンインストールで消える

MSIX 環境では `%LOCALAPPDATA%` への書き込みがパッケージ配下
（`%LOCALAPPDATA%\Packages\<PackageFamilyName>\LocalCache\...`）へ
リダイレクトされる場合がある。いずれにせよ**アンインストール時に削除される**。

本アプリの SQLite DB（宛先リスト本体）はここに置かれるため、
**アンインストール＝宛先リストの消失**になる。

対策は要件定義書 5.5 のバックアップ機能がそのまま効く。
既定のバックアップ先をドキュメントフォルダ配下
（`ドキュメント\メール配信ツール\Backup`）にしているのは、
**パッケージのライフサイクルの外側にデータを逃がす**ためでもある。
`AppPaths.DefaultBackupDirectory` はこの意図で実装済み。

> 運用への申し送り: PC入れ替え・アプリ再インストール前に
> 手動バックアップを実行するよう、利用手順書に明記すること。

### 4.2 DPAPI は MSIX 下でも問題なく動作する

`DataProtectionScope.CurrentUser` は Windows ログインユーザーに紐づくため、
パッケージ化の有無に影響されない。ただし 4.1 のとおり暗号化済み
パスワードを格納した DB 自体が消えるため、**再インストール後は
パスワードの再入力が必要**になる（別PCへの移行時も同様）。

### 4.3 検証が必要な項目

実機で MSIX パッケージを作った後、以下を確認すること。

- [ ] `AppPaths.DatabasePath` が実際にどこへ解決されるか（リダイレクトの有無）
- [ ] ドキュメントフォルダへのバックアップ書き込みが成功するか
- [ ] 添付ファイル選択ダイアログが期待どおり動作するか
- [ ] 587番ポートへの送信がブロックされないか
- [ ] アンインストール→再インストールでDBがどうなるか（4.1 の裏取り）

---

## 5. ビルド手順

`.wapproj` は Windows + MSBuild でのみビルドできる（`dotnet build` では不可）。

### 必要なワークロード

Visual Studio 本体を使う場合は **.NET デスクトップ開発** と
**ユニバーサル Windows プラットフォーム開発** の2つ。

**Build Tools for Visual Studio**（IDEなしの軽量版）を使う場合は名称が異なる。
2026年時点のインストーラーでは「Universal Windows Platform build tools」という
名前は存在せず、同じワークロード（内部ID:
`Microsoft.VisualStudio.Workload.UniversalBuildTools`）が
**「WinUI アプリケーション開発ビルド ツール」** として表示される。
これを選択すること。

### Visual Studio の IDE を使わずに（Developer Command Prompt無しで）ビルドする場合

`.wapproj` は本来 Visual Studio の IDE から開いてビルドされることを前提にしており、
`$(WapProjPath)`（パッケージング用ターゲットの場所）が**IDE側で自動設定される**。
そのため、`msbuild` を素のPowerShell/コマンドプロンプトから直接叩くと、
このプロパティが空のまま Import 条件が false になり、**`MSB4040`
（プロジェクトにターゲットがない）で失敗する**。

`msbuild.exe` の場所と `WapProjPath` を自力で見つけて明示的に渡す必要がある。

```powershell
# msbuild.exe の場所を特定する（vswhere は既定では Build Tools を検索対象外にするため
# -products * が必須）
$msbuild = @(& "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" `
  -products * -latest -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe)

# WapProjPath（Microsoft.DesktopBridge.props/targets の場所）を特定する
$vsInstall = & "C:\Program Files (x86)\Microsoft Visual Studio\Installer\vswhere.exe" `
  -products * -latest -requires Microsoft.Component.MSBuild -property installationPath
$wapProjPath = Get-ChildItem -Path $vsInstall -Recurse -Filter "Microsoft.DesktopBridge.props" `
  -ErrorAction SilentlyContinue | Select-Object -First 1 -ExpandProperty DirectoryName

# 開発ビルド（署名なし。ローカル確認用）
& $msbuild[0] packaging\MailDeliveryTool.Package\MailDeliveryTool.Package.wapproj `
  /p:Configuration=Release /p:Platform=x64 /p:WapProjPath="$wapProjPath" /restore
```

（VS付属の「Developer Command Prompt for VS」から実行する場合は、
起動時にこれらの環境が自動設定されるため `WapProjPath` の指定は不要。
ただし起動時のカレントフォルダはBuildToolsのインストール先になっているため、
先にリポジトリのフォルダへ `cd` すること。）

### 署名ありの配布パッケージ

`.pfx`はパスワード保護されているため、`PackageCertificateKeyFile`（.pfxのパス）・
`PackageCertificateThumbprint`（拇印）に加えて**`PackageCertificatePassword`も必須**。
これが抜けていると「証明書を開くことができませんでした」「指定されたネットワーク
パスワードが間違っています」というエラーになる（実機で確認済み）。

パスワードをコマンド履歴に平文で残さないよう、`Read-Host -AsSecureString`で
安全に入力してから渡すこと。

```powershell
$securePwd = Read-Host -AsSecureString "PFXのパスワードを入力"
$plainPwd = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
  [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePwd))

& $msbuild[0] packaging\MailDeliveryTool.Package\MailDeliveryTool.Package.wapproj `
  /p:Configuration=Release /p:Platform=x64 /p:WapProjPath="$wapProjPath" /restore `
  /p:AppxPackageSigningEnabled=true `
  /p:PackageCertificateKeyFile=C:\path\to\signing.pfx `
  /p:PackageCertificateThumbprint=<拇印> `
  /p:PackageCertificatePassword=$plainPwd
```

出力先は `artifacts/msix/`（`.gitignore` 済み）。

Visual Studio からは「MailDeliveryTool.Package」を右クリック →
`公開` → `アプリ パッケージの作成` でも同じものが生成できる（この場合は
`WapProjPath` の問題は起きない）。

### `NETSDK1112`（ランタイムパックが見つからない）が出る場合

MSIXは win-x64 向けにアプリをビルドするため、`Microsoft.NETCore.App.Runtime.win-x64`
等のランタイムパックの復元が必要。`MailDeliveryTool.App.csproj` に
`<RuntimeIdentifiers>win-x64</RuntimeIdentifiers>` を設定済みなので、
`/restore` 付きで通常どおりビルドすれば自動的に解決される。

`App.csproj` だけでは不十分だった。`.wapproj` 経由のビルドでは
`ProjectReference` を辿った `MailDeliveryTool.Core.csproj` の restore が
正しく連鎖せず、`Core` 側にも同じ宣言が必要だった（設定済み）。
それでも再発する場合は NuGet のオフラインキャッシュ／社内フィード側に
win-x64 のランタイムパックが存在するか確認すること。

### `APPX3217`（`UAP.props` の入ったSDKフォルダーが見つからない）が出る場合

```
APPX3217: 'UAP <バージョン>' の 'UAP.props' が含まれる SDK フォルダーが見つかりません
```

`.wapproj` の `TargetPlatformVersion` が指定するバージョンの
**UWPプラットフォームSDK**（クラシックなWindows Kitsの一部。.NETのTFMに
付けるWindows SDKバージョンとは別物で、NuGetからは取得されない）が
そのマシンにインストールされていない。

まず、実際にインストールされているバージョンを確認する。

```powershell
dir "C:\Program Files (x86)\Windows Kits\10\Platforms\UAP\"
```

表示されたバージョンに `.wapproj` の `TargetPlatformVersion`（および
`Package.appxmanifest` の `TargetDeviceFamily` の `MaxVersionTested`）を
合わせる。実機（2026年8月時点）では `10.0.22621.0` が未導入で
`10.0.26100.0` のみが存在したため、両ファイルとも `10.0.26100.0` に
揃えてある。別のマシンで異なるバージョンしか入っていない場合は、
同じ要領でこの2ファイルの値を合わせること
（該当バージョンのWindows SDKを個別コンポーネントとして追加インストールする
方法でもよい）。

この値は本来 `Directory.Build.props` の `WindowsTargetFramework`
（.NETのWindows API参照アセンブリ用。NuGetから解決されるため、UAP
プラットフォームSDKの実インストール状況とは無関係）とは**独立**しており、
一致させる必要はない。ただし実機で確認できた構成に合わせて、
現状はどちらも `10.0.26100.0` で揃えてある。

---

## 6. 未決事項（社内確認が必要）

| # | 項目 | 状態 |
|---|---|---|
| A | Identity / Publisher | **確定済み**。`CN=ESC`（[D-009](./decisions.md#d-009-msix署名方式publisher名確定)） |
| B | 署名方式 | **確定済み**。自己署名証明書＋手間を最小化した配布手順を採用（[D-009](./decisions.md#d-009-msix署名方式publisher名確定)） |
| C | 自動更新 | **未決定・保留**。有効化を推奨するが、パッケージを置くファイル共有のパスが決まってから対応（「3. 署名と配布」参照） |
| D | アプリアイコン | **確定済み**。モックのロゴをそのまま採用（[D-008](./decisions.md#d-008-アプリアイコンはモックのロゴをそのまま採用)） |

残るのは **C（自動更新）のみ**。ファイル共有のパスが決まれば`.wapproj`に
3行追加するだけで有効化できる（手順は「3. 署名と配布」参照）。
