using System.Windows;
using MailDeliveryTool.Core;
using MailDeliveryTool.Core.Data;

namespace MailDeliveryTool.App;

/// <summary>アプリケーションのエントリポイント。</summary>
public partial class App : Application
{
    /// <summary>
    /// アプリ全体で共有する接続ファクトリ。
    /// フェーズ5でDIコンテナを導入する際にここを差し替える。
    /// </summary>
    public static SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 初回起動時にDBを作成し、スキーマと初期データを投入する。
            // ログイン機能がないため、起動直後に無条件で実行してよい（要件定義書 2章）。
            ConnectionFactory = new SqliteConnectionFactory(AppPaths.DatabasePath);
            new DatabaseInitializer(ConnectionFactory).EnsureCreated();
        }
        catch (Exception ex)
        {
            // DBを作れない状態では何もできないため、原因を提示して終了する
            MessageBox.Show(
                $"データベースの初期化に失敗しました。\n\n{ex.Message}\n\n保存先: {AppPaths.DatabasePath}",
                "メール配信ツール",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
