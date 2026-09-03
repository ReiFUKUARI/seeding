# メール配信ツール

営業担当者が個人利用する、メール一斉配信デスクトップアプリケーション。

要件定義は別途「営業メール配信アプリ 要件定義・技術仕様書」を参照。
本リポジトリは**フェーズ5（機能実装）・フェーズ8（UI統一パス）完了**時点の内容です。
フェーズ4の作業内容は[フェーズ4 作業報告](docs/phase4-environment-setup.md)を参照。

## 構成

```
MailDeliveryTool.sln
├── src/MailDeliveryTool.Core/           業務ロジック（UI非依存）
│   ├── Data/                             SQLite 接続・スキーマ・初期化
│   ├── Mail/                             MailKit による SMTP 処理
│   ├── Models/                           ドメインモデル
│   └── Security/                         DPAPI による認証情報の暗号化
├── src/MailDeliveryTool.App/            WPF アプリ本体
├── src/MailDeliveryTool.SmtpProbe/      SMTP疎通検証ツール（配布対象外）
├── packaging/MailDeliveryTool.Package/  MSIX パッケージング
├── tools/validate_schema.py             DBスキーマ検証スクリプト
└── docs/                                設計・手順書
```

## 開発環境

- Windows 11（WPF のため Windows 必須）
- .NET SDK 10.0 以降（LTS）
- Visual Studio 2022（MSIX をビルドする場合。
  ワークロード「.NET デスクトップ開発」＋「ユニバーサル Windows プラットフォーム開発」）

## ビルドと実行

```powershell
dotnet restore
dotnet build MailDeliveryTool.sln -c Debug

# アプリを起動する（初回起動時にDBが自動作成される）
dotnet run --project src\MailDeliveryTool.App
```

> `.wapproj`（MSIX）は `dotnet build` では処理されません。
> MSBuild または Visual Studio でビルドしてください
> （[手順](docs/msix-packaging.md#5-ビルド手順)）。

### Windows以外（Linux/macOS）でのコンパイル確認について

`Directory.Build.props` が Windows 以外のビルドで自動的に
`EnableWindowsTargeting=true` を有効化するため、Linux/macOS でも
`MailDeliveryTool.Core` / `MailDeliveryTool.App` の**コンパイル確認**は可能です
（Windows実機でのビルドには影響しません）。

ただし WPF は Windows の UI ランタイムに依存するため、**実行・起動確認は
引き続きWindows実機が必要**です。コンパイルが通ることと、
アプリが実際に動くことは別です。

## 検証

```bash
# DBスキーマの検証（.NET SDK 不要・OS問わず実行可能）
python3 tools/validate_schema.py
```

```powershell
# SMTP疎通検証（実アカウントが必要）
dotnet run --project src\MailDeliveryTool.SmtpProbe -- --help
```

詳細は [SMTP疎通検証 手順書](docs/smtp-verification.md)。

## データの保存先

| 内容 | 場所 |
|---|---|
| SQLite データベース | `%LOCALAPPDATA%\メール配信ツール\maildelivery.db` |
| バックアップ（CSV） | `ドキュメント\メール配信ツール\Backup`（変更可） |

MSIX でインストールした場合、**アンインストールでデータベースが削除されます**。
再インストール前にバックアップを実行してください
（[詳細](docs/msix-packaging.md#41-アプリデータはアンインストールで消える)）。

## ドキュメント

| 文書 | 内容 |
|---|---|
| [決定事項ログ](docs/decisions.md) | 要件定義書に記載のない論点について確定させた判断 |
| [フェーズ4 作業報告](docs/phase4-environment-setup.md) | 環境構築の成果・未実施事項・次のアクション |
| [DBスキーマ 初版](docs/db-schema.md) | テーブル設計と設計判断の記録 |
| [SMTP疎通検証 手順書](docs/smtp-verification.md) | WebARENA への疎通検証手順と結果記録 |
| [MSIXパッケージング](docs/msix-packaging.md) | 配布形式の構成検討と未決事項 |
