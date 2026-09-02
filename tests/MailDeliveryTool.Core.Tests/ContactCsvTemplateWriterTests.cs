using MailDeliveryTool.Core.Csv;
using MailDeliveryTool.Core.Data.Repositories;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class ContactCsvTemplateWriterTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly string _csvPath;

    public ContactCsvTemplateWriterTests()
    {
        _csvPath = Path.Combine(Path.GetTempPath(), $"template-test-{Guid.NewGuid():N}.csv");
    }

    public void Dispose()
    {
        _db.Dispose();
        if (File.Exists(_csvPath))
        {
            File.Delete(_csvPath);
        }
    }

    [Fact]
    public void Write_7列のヘッダーと例1行がBOM付きUTF8で書き出される()
    {
        var categoryRepo = new CategoryRepository(_db.Factory);

        ContactCsvTemplateWriter.Write(_csvPath, categoryRepo.GetAxes());

        var bytes = File.ReadAllBytes(_csvPath);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);

        var lines = File.ReadAllLines(_csvPath);
        Assert.Equal(2, lines.Length); // ヘッダー + 例1行
        Assert.Contains("会社名,担当者名,メールアドレス,種別,技術領域,メモ,配信停止", lines[0]);
        Assert.Contains("sample@example.jp", lines[1]);
    }
}
