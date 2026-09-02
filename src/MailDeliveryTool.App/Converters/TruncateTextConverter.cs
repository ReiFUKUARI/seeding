using System.Globalization;
using System.Windows.Data;

namespace MailDeliveryTool.App.Converters;

/// <summary>
/// 文字列を指定文字数（ConverterParameter、既定10）まで省略して末尾に「…」を付ける。
/// 一覧のメモ列で、全文はツールチップに出しつつ表示は短く保つ用途に使う。
/// </summary>
public sealed class TruncateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        var maxLength = 10;
        if (parameter is string s && int.TryParse(s, out var parsed))
        {
            maxLength = parsed;
        }

        return text.Length > maxLength ? text[..maxLength] + "…" : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
