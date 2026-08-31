using System.Globalization;
using System.Text;
using CsvHelper;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Csv;

/// <summary>
/// パートナーリストのCSV取込を担う（mock_prototype.htmlのCSVから取り込むモーダル相当）。
/// 列順は<see cref="ContactCsvTemplateWriter"/>と同じ固定順（会社名,担当者名,メールアドレス,種別,技術領域,メモ）
/// を前提とし、列名ではなく位置で読む（軸の表示名はテンプレート生成時点のものと一致するとは限らないため）。
/// </summary>
public static class ContactCsvImporter
{
    /// <summary>
    /// CSVを読み込み、新規登録すべき宛先の一覧を返す。
    /// 種別・技術領域に既存のカテゴリ値にない値が含まれる行が1件でもあれば、取込全体を中止して
    /// <see cref="ContactCsvImportException"/> を投げる（部分的な取込は行わない）。
    /// 既にDBに存在するメールアドレスと一致する行は、新規登録の対象から除外する（重複登録の防止）。
    /// メールアドレスが空の行は取込対象としない。
    /// </summary>
    public static ContactCsvImportResult Import(
        string filePath, IReadOnlyList<CategoryAxis> axes, IReadOnlySet<string> existingEmails)
    {
        var typeAxis = axes.FirstOrDefault(a => a.Id == CategoryAxis.TypeAxisId);
        var techAxis = axes.FirstOrDefault(a => a.Id == CategoryAxis.TechFieldAxisId);
        var typeValueIdByName = (typeAxis?.Values ?? new List<CategoryValue>()).ToDictionary(v => v.Name, v => v.Id);
        var techValueIdByName = (techAxis?.Values ?? new List<CategoryValue>()).ToDictionary(v => v.Name, v => v.Id);
        var typeAxisName = typeAxis?.Name ?? "種別";
        var techAxisName = techAxis?.Name ?? "技術領域";

        using var reader = new StreamReader(filePath, Encoding.UTF8);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        csv.Read();
        csv.ReadHeader();

        var newContacts = new List<Contact>();
        var unknownValues = new List<string>();
        var skippedDuplicateCount = 0;

        while (csv.Read())
        {
            csv.TryGetField(0, out string? company);
            csv.TryGetField(1, out string? contactName);
            csv.TryGetField(2, out string? email);
            csv.TryGetField(3, out string? typeCell);
            csv.TryGetField(4, out string? techCell);
            csv.TryGetField(5, out string? memo);

            email = (email ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(email))
            {
                continue;
            }

            var rowHasUnknownValue = false;
            var categoryValueIds = new List<long>();

            foreach (var name in SplitValues(typeCell))
            {
                if (typeValueIdByName.TryGetValue(name, out var id))
                {
                    categoryValueIds.Add(id);
                }
                else
                {
                    unknownValues.Add($"{typeAxisName}: {name}");
                    rowHasUnknownValue = true;
                }
            }

            foreach (var name in SplitValues(techCell))
            {
                if (techValueIdByName.TryGetValue(name, out var id))
                {
                    categoryValueIds.Add(id);
                }
                else
                {
                    unknownValues.Add($"{techAxisName}: {name}");
                    rowHasUnknownValue = true;
                }
            }

            if (rowHasUnknownValue)
            {
                // このファイル全体が最終的にエラーになるため、この行の取込データ自体は作らない
                continue;
            }

            if (existingEmails.Contains(email))
            {
                skippedDuplicateCount++;
                continue;
            }

            var contact = new Contact
            {
                CompanyName = (company ?? string.Empty).Trim(),
                ContactName = (contactName ?? string.Empty).Trim(),
                Email = email,
                Memo = string.IsNullOrWhiteSpace(memo) ? null : memo!.Trim(),
            };
            contact.CategoryValueIds.AddRange(categoryValueIds);
            newContacts.Add(contact);
        }

        if (unknownValues.Count > 0)
        {
            throw new ContactCsvImportException(unknownValues.Distinct().ToList());
        }

        return new ContactCsvImportResult(newContacts, skippedDuplicateCount);
    }

    private static IEnumerable<string> SplitValues(string? cell) =>
        (cell ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>CSV取込の結果。</summary>
public sealed record ContactCsvImportResult(IReadOnlyList<Contact> NewContacts, int SkippedDuplicateCount);

/// <summary>既存のカテゴリ値にない値がCSVに含まれていたため、取込を中止したことを表す。</summary>
public sealed class ContactCsvImportException : Exception
{
    public IReadOnlyList<string> UnknownValues { get; }

    public ContactCsvImportException(IReadOnlyList<string> unknownValues)
        : base($"CSVに現在のカテゴリ値に存在しない値が含まれています: {string.Join("、", unknownValues)}")
    {
        UnknownValues = unknownValues;
    }
}
