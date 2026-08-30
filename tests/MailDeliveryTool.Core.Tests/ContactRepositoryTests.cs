using MailDeliveryTool.Core.Data.Repositories;
using MailDeliveryTool.Core.Models;
using Xunit;

namespace MailDeliveryTool.Core.Tests;

public sealed class ContactRepositoryTests : IDisposable
{
    private readonly TestDatabase _db = new();
    private readonly ContactRepository _repo;

    public ContactRepositoryTests() => _repo = new ContactRepository(_db.Factory);

    public void Dispose() => _db.Dispose();

    [Fact]
    public void Add_割り当てたIdが取得できる()
    {
        var contact = new Contact { CompanyName = "株式会社A", ContactName = "山田", Email = "a@x.jp" };

        var id = _repo.Add(contact);

        Assert.True(id > 0);
        Assert.Equal(id, contact.Id);
    }

    [Fact]
    public void Add_カテゴリ値の紐付けが保存される()
    {
        var contact = new Contact
        {
            CompanyName = "株式会社A",
            ContactName = "山田",
            Email = "a@x.jp",
            CategoryValueIds = { 1, 3 },
        };

        _repo.Add(contact);
        var loaded = _repo.GetById(contact.Id);

        Assert.NotNull(loaded);
        Assert.Equal(new[] { 1L, 3L }, loaded!.CategoryValueIds.OrderBy(x => x));
    }

    [Fact]
    public void Update_カテゴリ値が置き換わる()
    {
        var contact = new Contact
        {
            CompanyName = "株式会社A",
            ContactName = "山田",
            Email = "a@x.jp",
            CategoryValueIds = { 1, 3 },
        };
        _repo.Add(contact);

        contact.CategoryValueIds.Clear();
        contact.CategoryValueIds.AddRange(new[] { 2L, 4L });
        contact.CompanyName = "株式会社A改";
        _repo.Update(contact);

        var loaded = _repo.GetById(contact.Id);
        Assert.Equal("株式会社A改", loaded!.CompanyName);
        Assert.Equal(new[] { 2L, 4L }, loaded.CategoryValueIds.OrderBy(x => x));
    }

    [Fact]
    public void SetSuspended_停止と再開ができる()
    {
        var contact = new Contact { CompanyName = "A", ContactName = "B", Email = "a@x.jp" };
        _repo.Add(contact);

        _repo.SetSuspended(contact.Id, true);
        Assert.True(_repo.GetById(contact.Id)!.IsSuspended);

        _repo.SetSuspended(contact.Id, false);
        Assert.False(_repo.GetById(contact.Id)!.IsSuspended);
    }

    [Fact]
    public void Search_停止中の宛先は結果に含まれない()
    {
        var active = new Contact
        {
            CompanyName = "稼働中", ContactName = "A", Email = "a@x.jp", CategoryValueIds = { 1, 3 },
        };
        var suspended = new Contact
        {
            CompanyName = "停止中", ContactName = "B", Email = "b@x.jp", CategoryValueIds = { 1, 3 },
        };
        _repo.Add(active);
        _repo.Add(suspended);
        _repo.SetSuspended(suspended.Id, true);

        var result = _repo.Search(null, null);

        Assert.Contains(result, c => c.Id == active.Id);
        Assert.DoesNotContain(result, c => c.Id == suspended.Id);
    }

    [Fact]
    public void Search_会社名は部分一致()
    {
        _repo.Add(new Contact
        {
            CompanyName = "アルファ商事", ContactName = "A", Email = "a@x.jp", CategoryValueIds = { 1, 3 },
        });
        _repo.Add(new Contact
        {
            CompanyName = "ベータ物産", ContactName = "B", Email = "b@x.jp", CategoryValueIds = { 1, 3 },
        });

        var result = _repo.Search("ルファ", null);

        Assert.Single(result);
        Assert.Equal("アルファ商事", result[0].CompanyName);
    }

    [Fact]
    public void Search_特殊文字を含むキーワードでも安全に検索できる()
    {
        _repo.Add(new Contact
        {
            CompanyName = "100%割引商事", ContactName = "A", Email = "a@x.jp", CategoryValueIds = { 1, 3 },
        });
        _repo.Add(new Contact
        {
            CompanyName = "普通商事", ContactName = "B", Email = "b@x.jp", CategoryValueIds = { 1, 3 },
        });

        // "%" 自体がキーワードに含まれていても、LIKEのワイルドカードとして誤爆しないこと
        var result = _repo.Search("100%", null);

        Assert.Single(result);
        Assert.Equal("100%割引商事", result[0].CompanyName);
    }

    [Fact]
    public void Search_軸内はOR軸間はAND()
    {
        // 値ID: 1=案件 2=人材 3=開発 4=インフラ 5=その他（Seed_v1.sqlの初期値）
        _repo.Add(new Contact
        {
            CompanyName = "案件×開発", ContactName = "A", Email = "a@x.jp", CategoryValueIds = { 1, 3 },
        });
        _repo.Add(new Contact
        {
            CompanyName = "人材×開発", ContactName = "B", Email = "b@x.jp", CategoryValueIds = { 2, 3 },
        });
        _repo.Add(new Contact
        {
            CompanyName = "案件×インフラ", ContactName = "C", Email = "c@x.jp", CategoryValueIds = { 1, 4 },
        });

        var filters = new Dictionary<long, IReadOnlyList<long>>
        {
            [CategoryAxis.TypeAxisId] = new List<long> { 1, 2 },
            [CategoryAxis.TechFieldAxisId] = new List<long> { 3 },
        };
        var result = _repo.Search(null, filters);

        var names = result.Select(c => c.CompanyName).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(new[] { "人材×開発", "案件×開発" }, names);
    }

    [Fact]
    public void Add_重複メールアドレスを許容する()
    {
        _repo.Add(new Contact { CompanyName = "A社", ContactName = "A", Email = "dup@x.jp" });
        _repo.Add(new Contact { CompanyName = "B社", ContactName = "B", Email = "dup@x.jp" });

        var all = _repo.GetAll();
        Assert.Equal(2, all.Count(c => c.Email == "dup@x.jp"));
    }

    [Fact]
    public void GetAll_停止中も含めて全件返す()
    {
        var suspended = new Contact { CompanyName = "A", ContactName = "A", Email = "a@x.jp" };
        _repo.Add(suspended);
        _repo.SetSuspended(suspended.Id, true);
        _repo.Add(new Contact { CompanyName = "B", ContactName = "B", Email = "b@x.jp" });

        var all = _repo.GetAll();

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public void Update_存在しないIdはInvalidOperationExceptionになる()
    {
        var contact = new Contact { Id = 9999, CompanyName = "X", ContactName = "Y", Email = "x@x.jp" };

        Assert.Throws<InvalidOperationException>(() => _repo.Update(contact));
    }

    [Fact]
    public void SetSuspended_存在しないIdはInvalidOperationExceptionになる()
    {
        Assert.Throws<InvalidOperationException>(() => _repo.SetSuspended(9999, true));
    }

    [Fact]
    public void GetById_存在しなければnull()
    {
        Assert.Null(_repo.GetById(9999));
    }
}
