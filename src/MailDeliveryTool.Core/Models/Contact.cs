namespace MailDeliveryTool.Core.Models;

/// <summary>宛先（パートナーリストの1行）。要件定義書 5.4。</summary>
public sealed class Contact
{
    public long Id { get; set; }

    /// <summary>会社名。フォーム上は必須だが、CSV等の経緯で空になりうる（7.1の検証対象）。</summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>担当者名。同上。</summary>
    public string ContactName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>メモ（任意・100文字まで）。</summary>
    public string? Memo { get; set; }

    /// <summary>停止中フラグ。true の宛先は「新しい配信」に一切表示されない（5.2）。</summary>
    public bool IsSuspended { get; set; }

    /// <summary>この宛先に紐づくカテゴリ値のID一覧（軸をまたいだ全件）。</summary>
    public List<long> CategoryValueIds { get; set; } = new();

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
