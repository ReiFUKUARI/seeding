using System.Windows;
using MailDeliveryTool.Core;
using MailDeliveryTool.Core.Data;

namespace MailDeliveryTool.App;

/// <summary>アプリケーションのエントリポイント。</summary>
public partial class App : Application
{
    /// <summary>
    /// アプリ全体で共有する接続ファクトリ。
    /// 本格的なDIコンテナを導入するほどの規模ではないため、静的プロパティで共有する。
    /// </summary>
    public static SqliteConnectionFactory ConnectionFactory { get; private set; } = null!;

    /// <summary>アプリ全体で共有するリポジトリ・サービス（<see cref="CoreServices"/> 参照）。</summary>
    public static CoreServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            // 初回起動時にDBを作成し、スキーマと初期データを投入する。
            // ログイン機能がないため、起動直後に無条件で実行してよい（要件定義書 2章）。
            ConnectionFactory = new SqliteConnectionFactory(AppPaths.DatabasePath);
            new DatabaseInitializer(ConnectionFactory).EnsureCreated();
            Services = new CoreServices(ConnectionFactory);
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
