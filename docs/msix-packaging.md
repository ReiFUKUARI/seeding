# MSIX パッケージング構成の検討（フェーズ4・成果物④）

要件定義書 12章で配布形式を MSIX と決定済みのため、本書では
「どう構成するか」を具体化し、決定事項と未決事項を切り分ける。

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

### Identity / Publisher

`Publisher` は**署名証明書のサブジェクト（CN）と完全一致**していなければ
インストールが失敗する。現在はプレースホルダ（`CN=ExampleCorp`）のため、
署名方針の確定後に必ず差し替える。→ 未決事項 A

---

## 3. 署名と配布

MSIX は**署名が必須**で、かつ**その証明書がインストール先PCで信頼されている**必要がある。

### 選択肢

| 方式 | 初期コスト | 配布手順 | 向き |
|---|---|---|---|
| **社内CA / 自己署名証明書** | 低（無償） | `.cer` を各PCの「信頼されたルート証明機関」または「信頼された発行元」に導入する手順が別途必要 | 社内数名規模 |
| **公的コード署名証明書（OV/EV）** | 年額数万円〜 | 証明書導入が不要。ダブルクリックでインストールできる | 配布先が広い場合 |

**推奨は社内CA／自己署名**。要件定義書 2章のとおり利用者は営業担当者ごとの
個別インストールで、社内に閉じた配布のため。
ただし証明書導入の一手間が各PCで発生する点は運用と合わせて要合意。→ 未決事項 B

証明書ファイル（`.pfx`）は**リポジトリに含めない**（`.gitignore` で除外済み）。

### 配布とインストール

MSIX はユーザー単位インストールが既定で、要件定義書 2章の
「1人1台・営業担当者ごとに個別インストール」と素直に合致する。管理者権限は不要。

```powershell
# 各PCでの初回インストール
Add-AppxPackage -Path "\\fileserver\share\MailDeliveryTool_0.1.0.0_x64.msix"
```

Windows 11 はサイドロードが既定で有効なため、追加のポリシー設定は不要。

### 更新

`.appinstaller` ファイルを併用すると、ファイル共有上のパッケージを見て
アプリ起動時に自動更新できる。利用者が営業担当者で、更新のたびに
手作業を依頼しにくいことを踏まえると導入価値は高い。→ 未決事項 C

更新時は `Package.appxmanifest` の `Version`（4桁 `major.minor.build.revision`）を
必ず**インクリメント**する。同一バージョンでは更新が適用されない。

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

```powershell
& $msbuild[0] packaging\MailDeliveryTool.Package\MailDeliveryTool.Package.wapproj `
  /p:Configuration=Release /p:Platform=x64 /p:WapProjPath="$wapProjPath" /restore `
  /p:AppxPackageSigningEnabled=true `
  /p:PackageCertificateKeyFile=C:\path\to\signing.pfx `
  /p:PackageCertificateThumbprint=<拇印>
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
このエラーが再発する場合は NuGet のオフラインキャッシュ／社内フィード側に
win-x64 のランタイムパックが存在するか確認すること。

---

## 6. 未決事項（社内確認が必要）

| # | 項目 | 内容 |
|---|---|---|
| A | Identity / Publisher | 正式なパッケージ名と発行者名。署名証明書のサブジェクトと一致させる必要がある |
| B | 署名方式 | 社内CA・自己署名・公的証明書のいずれにするか。自己署名の場合、各PCへの `.cer` 導入手順を誰が実施するか |
| C | 自動更新 | `.appinstaller` による自動更新を採用するか。採用する場合、パッケージを置くファイル共有のパス |
| D | アプリアイコン | 要件定義書 14章の社内ブランドロゴの利用可否（`packaging/.../Images/` に未配置） |
