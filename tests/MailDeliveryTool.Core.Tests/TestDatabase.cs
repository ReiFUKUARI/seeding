using MailDeliveryTool.Core.Data;

namespace MailDeliveryTool.Core.Tests;

/// <summary>
/// テストごとに独立したSQLiteファイルを作成し、スキーマを適用するヘルパー。
/// リポジトリ各クラスは呼び出しのたびに新しい接続を開く設計のため、
/// :memory: を使うと呼び出しごとに別々の空DBになってしまう。そのため
/// 一時ファイルを使う。
/// </summary>
public sealed class TestDatabase : IDisposable
{
    public SqliteConnectionFactory Factory { get; }

    private readonly string _path;

    public TestDatabase()
    {
        _path = Path.Combine(Path.GetTempPath(), $"maildelivery-test-{Guid.NewGuid():N}.db");
        Factory = new SqliteConnectionFactory(_path);
        new DatabaseInitializer(Factory).EnsureCreated();
    }

    public void Dispose()
    {
        // WALモード（SqliteConnectionFactory参照）のため -wal / -shm の副ファイルも一緒に消す
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var file = _path + suffix;
            if (File.Exists(file))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException)
                {
                    // ベストエフォート。テスト一時ファイルの後始末に失敗しても本体には影響しない
                }
            }
        }
    }
}
