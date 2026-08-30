using System.Text;
using System.Windows;
using System.Windows.Controls;
using MailDeliveryTool.Core;
using MailDeliveryTool.Core.Data;

namespace MailDeliveryTool.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            ShowDiagnostics();
            ShowPlaceholder("Compose");
        };
    }

    /// <summary>
    /// サイドバーの選択に応じてタイトルと説明を切り替える。
    /// 実際の画面遷移はフェーズ5で実装する。
    /// </summary>
    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton { Tag: string key })
        {
            return;
        }

        ShowPlaceholder(key);
    }

    private void ShowPlaceholder(string key)
    {
        var (title, body) = key switch
        {
            "Partners" => ("パートナーリスト",
                "宛先マスタの検索・一覧・登録・停止管理（要件定義書 5章）。"
                + "CSV取込とテンプレートダウンロードもこの画面のモーダルから行います。"),
            "Settings" => ("設定",
                "メールアカウント／バックアップ／カテゴリ管理の3セクション（要件定義書 10章）。"
                + "パスワードはDPAPIで暗号化して保存します。"),
            _ => ("新しい配信",
                "宛先選択（すべて／メールリストの2タブ）→ メール作成 → 内容の確認 → 送信中 → 送信結果"
                + "というウィザードフロー（要件定義書 6〜9章）。"),
        };

        TitleText.Text = title;
        PlaceholderTitle.Text = title;
        PlaceholderBody.Text = body + "\n\n（フェーズ5で実装予定）";
    }

    /// <summary>
    /// DBが正しく初期化されたかを画面上で確認できるようにする。
    /// フェーズ4の成果物②が動作していることの目視確認用。
    /// </summary>
    private void ShowDiagnostics()
    {
        var report = new StringBuilder();
        report.AppendLine($".NET             : {Environment.Version}");
        report.AppendLine($"OS               : {Environment.OSVersion.VersionString}");
        report.AppendLine($"データベース     : {AppPaths.DatabasePath}");

        try
        {
            using var connection = App.ConnectionFactory.Create();

            long Scalar(string sql)
            {
                using var command = connection.CreateCommand();
                command.CommandText = sql;
                return Convert.ToInt64(command.ExecuteScalar());
            }

            report.AppendLine($"スキーマ版数     : v{Scalar("SELECT IFNULL(MAX(Version),0) FROM SchemaVersion")}"
                              + $"（想定 v{DatabaseInitializer.CurrentSchemaVersion}）");
            report.AppendLine($"カテゴリ軸       : {Scalar("SELECT COUNT(*) FROM CategoryAxis")} 件");
            report.AppendLine($"カテゴリ値       : {Scalar("SELECT COUNT(*) FROM CategoryValue")} 件");
            report.AppendLine($"宛先             : {Scalar("SELECT COUNT(*) FROM Contact")} 件");
            report.AppendLine($"外部キー制約     : {(Scalar("PRAGMA foreign_keys") == 1 ? "有効" : "無効（要確認）")}");

            using var accountCommand = connection.CreateCommand();
            accountCommand.CommandText = "SELECT Host, Port, UserName FROM MailAccountSetting WHERE Id = 1";
            using var reader = accountCommand.ExecuteReader();
            if (reader.Read() && !string.IsNullOrWhiteSpace(reader.GetString(0)))
            {
                AccountText.Text = reader.GetString(2);
                report.AppendLine($"送信元サーバー   : {reader.GetString(0)}:{reader.GetInt32(1)}");
            }
            else
            {
                AccountText.Text = "未設定";
                report.AppendLine("送信元サーバー   : 未設定（設定画面から登録してください）");
            }
        }
        catch (Exception ex)
        {
            report.AppendLine($"データベース確認に失敗しました: {ex.Message}");
        }

        DiagnosticsText.Text = report.ToString().TrimEnd();
    }
}
