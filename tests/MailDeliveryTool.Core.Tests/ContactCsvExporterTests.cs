using MailDeliveryTool.Core.Backup;
using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class ContactCsvExporterTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly string _csvPath;

    public ContactCsvExporterTests()
    {
        _csvPath = Path.Combine(Path.GetTempPath(), $"export-test-{Guid.NewGuid():N}.csv");
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
    public void Export_ヘッダーと停止中を含む全件が出力される()
    {
        var contactRepo = new ContactRepository(_db.Factory);
        var categoryRepo = new CategoryRepository(_db.Factory);

        var active = new Contact
        {
            CompanyName = "ガンマ製作所", ContactName = "高橋 健", Email = "t@x.jp",
            Memo = "決裁は本社経由", CategoryValueIds = { 1, 2, 3, 5 }, // 案件+人材, 開発+その他
        };
        var suspended = new Contact
        {
            CompanyName = "デルタ商会", ContactName = "", Email = "info@delta.jp",
            CategoryValueIds = { 1, 5 },
        };
        contactRepo.Add(active);
        contactRepo.Add(suspended);
        contactRepo.SetSuspended(suspended.Id, true);

        var contacts = contactRepo.GetAll();
        var axes = categoryRepo.GetAxes();
        ContactCsvExporter.Export(_csvPath, contacts, axes);

        var lines = File.ReadAllLines(_csvPath);
        // BOM付きUTF-8で書いているので、1行目冒頭のBOMを考慮しつつヘッダーを確認する
        Assert.Contains("会社名,担当者名,メールアドレス,種別,技術領域,メモ,状態", lines[0]);
        Assert.Equal(3, lines.Length); // ヘッダー + 2件

        Assert.Contains(lines, l => l.Contains("ガンマ製作所") && l.Contains("配信中")
            && l.Contains("\"案件,人材\"") && l.Contains("\"開発,その他\""));
        Assert.Contains(lines, l => l.Contains("デルタ商会") && l.Contains("停止中"));
    }

    [Fact]
    public void Export_BOM付きUTF8で書き出される()
    {
        var contactRepo = new ContactRepository(_db.Factory);
        var categoryRepo = new CategoryRepository(_db.Factory);
        contactRepo.Add(new Contact { CompanyName = "A", ContactName = "B", Email = "a@x.jp" });

        ContactCsvExporter.Export(_csvPath, contactRepo.GetAll(), categoryRepo.GetAxes());

        var bytes = File.ReadAllBytes(_csvPath);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }
}
