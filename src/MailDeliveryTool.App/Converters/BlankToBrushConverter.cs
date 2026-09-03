using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MailDeliveryTool.App.Converters;

/// <summary>
/// 文字列が空（null・空文字・空白のみ）のときだけ、ConverterParameterで指定した
/// リソースキーのBrushを返す。値がある場合はDependencyProperty.UnsetValueを返し、
/// Setter適用前の既定のForeground（通常のテキスト色）のままにする。
///
/// 会社名が未入力の宛先（要件定義書7.1の想定）を一覧でDangerBrush、
/// 担当者名が未入力の宛先をWarnBrushで強調するのに使う
/// （mock_prototype.htmlの「（未入力・保存不可）」「（未入力）」相当）。
/// </summary>
public sealed class BlankToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = value as string ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(text) || parameter is not string resourceKey)
        {
            return DependencyProperty.UnsetValue;
        }

        return Application.Current.TryFindResource(resourceKey) ?? DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
