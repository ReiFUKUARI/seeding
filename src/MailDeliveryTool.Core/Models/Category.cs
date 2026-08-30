namespace MailDeliveryTool.Core.Models;

/// <summary>
/// カテゴリ軸。種別／技術領域の2件に固定される（要件定義書 5.3）。
/// </summary>
public sealed class CategoryAxis
{
    /// <summary>種別軸の固定ID。</summary>
    public const long TypeAxisId = 1;

    /// <summary>技術領域軸の固定ID。</summary>
    public const long TechFieldAxisId = 2;

    public long Id { get; set; }

    /// <summary>表示名が変わってもコードから安定参照するためのキー（Type / TechField）。</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public List<CategoryValue> Values { get; set; } = new();
}

/// <summary>軸配下のカテゴリ値（案件・人材・開発 等）。設定画面から追加・削除可能。</summary>
public sealed class CategoryValue
{
    public long Id { get; set; }
    public long AxisId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
}
