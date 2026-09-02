using System.Globalization;
using System.Text;
using CsvHelper;
using MailDeliveryTool.Core.Models;

namespace MailDeliveryTool.Core.Csv;

/// <summary>
/// パートナーリストのCSV取込用テンプレートを書き出す（mock_prototype.htmlのdownloadCsvTemplate相当）。
/// バックアップ用の<see cref="Backup.ContactCsvExporter"/>とは別物。7列目の「配信停止」は
/// 空欄なら配信中、"TRUE"（大小文字を問わない）・"1"・"○" のいずれかなら停止中として取り込む。
/// </summary>
public static class ContactCsvTemplateWriter
{
    public static void Write(string filePath, IReadOnlyList<CategoryAxis> axes)
    {
        var typeAxis = axes.FirstOrDefault(a => a.Id == CategoryAxis.TypeAxisId);
        var techAxis = axes.FirstOrDefault(a => a.Id == CategoryAxis.TechFieldAxisId);

        // BOM付きUTF-8。Excelでの文字化けを防ぐため（mock_prototype.htmlと同じ方針）
        using var writer = new StreamWriter(filePath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        csv.WriteField("会社名");
        csv.WriteField("担当者名");
        csv.WriteField("メールアドレス");
        csv.WriteField(typeAxis?.Name ?? "種別");
        csv.WriteField(techAxis?.Name ?? "技術領域");
        csv.WriteField("メモ");
        csv.WriteField("配信停止");
        csv.NextRecord();

        csv.WriteField("株式会社サンプル");
        csv.WriteField("山田 太郎");
        csv.WriteField("sample@example.jp");
        csv.WriteField(typeAxis?.Values.FirstOrDefault()?.Name ?? string.Empty);
        csv.WriteField(techAxis?.Values.FirstOrDefault()?.Name ?? string.Empty);
        csv.WriteField("初回商談済み");
        csv.WriteField(string.Empty);
        csv.NextRecord();
    }
}
