using System.Globalization;
using System.Windows.Data;

namespace MailDeliveryTool.App.Converters;

/// <summary>
/// 文字列を指定文字数（ConverterParameter、既定10）まで省略して末尾に「…」を付ける。
/// 一覧のメモ列で、全文はツールチップに出しつつ表示は短く保つ用途に使う。
///
/// ConverterParameterは"maxLength"または"maxLength|未入力時の代替表示"の形式。
/// 代替表示を指定した場合、値が空（null・空文字・空白のみ）のときは省略せずそのまま返す
/// （mock_prototype.htmlの「（未入力・保存不可）」「（未入力）」「-」相当）。
/// </summary>
public sealed class TruncateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var maxLength = 10;
        string? blankFallback = null;
        if (parameter is string s)
        {
            var parts = s.Split('|', 2);
            if (int.TryParse(parts[0], out var parsed))
            {
                maxLength = parsed;
            }

            if (parts.Length > 1)
            {
                blankFallback = parts[1];
            }
        }

        if (blankFallback is not null && string.IsNullOrWhiteSpace(text))
        {
            return blankFallback;
        }

        return text.Length > maxLength ? text[..maxLength] + "…" : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
