using System.Threading.Tasks;
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

        // UIスレッドで捕まえられなかった例外は既定だとアプリが無言で落ちるだけになる。
        // 原因調査・ユーザーへの通知のため、エラー内容を表示してから続行できるようにする。
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        try
        {
            // 初回起動時にDBを作成し、スキーマと初期データを投入する。
            // ログイン機能がないため、起動直後に無条件で実行してよい（要件定義書 2章）。
            ConnectionFactory = new SqliteConnectionFactory(AppPaths.DatabasePath);
            new DatabaseInitializer(ConnectionFactory).EnsureCreated();
            Services = new CoreServices(ConnectionFactory);

            // D-004: 起動時に前回のバックアップから7日以上経過していれば自動的に実行する。
            // 起動処理をブロックしないよう背景で実行し、進捗モーダルは出さない（手動実行時のみ表示する）。
            // 保存先が書き込めない等で失敗した場合はLastBackupAtが更新されないため、
            // 次回起動時に自動的に再試行される。
            if (Services.BackupService.IsWeeklyBackupDue())
            {
                _ = Task.Run(() =>
                {
                    try
                    {
                        Services.BackupService.Run();
                    }
                    catch
                    {
                        // 背景実行のため、失敗してもユーザーへは通知しない（次回起動時に再試行する）
                    }
                });
            }
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

    private static void OnDispatcherUnhandledException(
        object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            $"予期しないエラーが発生しました。\n\n{e.Exception.Message}\n\n"
            + "この操作は中止されましたが、アプリは終了せず続行できます。",
            "メール配信ツール",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        // ここでハンドル済みにすることで、UIスレッド例外によるアプリ全体のクラッシュを防ぐ。
        e.Handled = true;
    }
}
