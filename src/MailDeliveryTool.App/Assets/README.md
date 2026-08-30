# Assets

アプリアイコン `app.ico` をここに配置する。

現時点では未配置。要件定義書 14章のとおり、社内ブランドロゴの
利用可否を確認したうえで差し替えること。未配置でもビルドは通る
（`MailDeliveryTool.App.csproj` で `ApplicationIcon` を条件付きにしている）。

必要なサイズ: 16 / 32 / 48 / 256 px を1つの .ico にまとめる。
MSIX 側のタイル画像は `packaging/MailDeliveryTool.Package/Images/` を参照。
