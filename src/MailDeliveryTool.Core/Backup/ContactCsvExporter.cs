using System.Globalization;
using System.Text;
using CsvHelper;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Backup;

/// <summary>
/// 宛先一覧をCSVへ書き出す。バックアップ（要件定義書5.5・10.2）と、
/// パートナーリストの手動エクスポート（フェーズ5・CSV機能）の両方から使う想定。
/// </summary>
/// <remarks>
/// 停止中の宛先も含める（D-006）。要件定義書5.5のCSV列（会社名・担当者名・
/// メールアドレス・種別・技術領域・メモ）に加えて「状態」列を末尾に追加している。
/// 取込テンプレート（フェーズ5のCSV取込機能）側は「状態」列がなくても
/// 全件配信中として扱う想定のため、列を追加しても取込側との互換性は崩れない。
/// </remarks>
public static class ContactCsvExporter
{
    /// <summary>
    /// CSVを書き出す。列: 会社名, 担当者名, メールアドレス, {種別軸名}, {技術領域軸名}, メモ, 状態
    /// </summary>
    public static void Export(string filePath, IReadOnlyList<Contact> contacts, IReadOnlyList<CategoryAxis> axes)
    {
        var typeAxis = axes.FirstOrDefault(a => a.Id == CategoryAxis.TypeAxisId);
        var techAxis = axes.FirstOrDefault(a => a.Id == CategoryAxis.TechFieldAxisId);
        var valueNameById = axes.SelectMany(a => a.Values).ToDictionary(v => v.Id, v => v.Name);

        // BOM付きUTF-8。Excelでの文字化けを防ぐため（フェーズ3モックのCSVテンプレートと同じ方針）
        using var writer = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField("会社名");
        csv.WriteField("担当者名");
        csv.WriteField("メールアドレス");
        csv.WriteField(typeAxis?.Name ?? "種別");
        csv.WriteField(techAxis?.Name ?? "技術領域");
        csv.WriteField("メモ");
        csv.WriteField("状態");
        csv.NextRecord();

        foreach (var contact in contacts)
        {
            csv.WriteField(contact.CompanyName);
            csv.WriteField(contact.ContactName);
            csv.WriteField(contact.Email);
            csv.WriteField(string.Join(",", ValueNamesForAxis(contact, typeAxis, valueNameById)));
            csv.WriteField(string.Join(",", ValueNamesForAxis(contact, techAxis, valueNameById)));
            csv.WriteField(contact.Memo ?? string.Empty);
            csv.WriteField(contact.IsSuspended ? "停止中" : "配信中");
            csv.NextRecord();
        }
    }

    private static IEnumerable<string> ValueNamesForAxis(
        Contact contact, CategoryAxis? axis, IReadOnlyDictionary<long, string> valueNameById)
    {
        if (axis is null)
        {
            yield break;
        }

        var axisValueIds = axis.Values.Select(v => v.Id).ToHashSet();
        foreach (var id in contact.CategoryValueIds)
        {
            if (axisValueIds.Contains(id) && valueNameById.TryGetValue(id, out var name))
            {
                yield return name;
            }
        }
    }
}
