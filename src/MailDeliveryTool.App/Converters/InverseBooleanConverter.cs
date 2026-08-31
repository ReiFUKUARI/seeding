using System.Globalization;
using System.Windows.Data;

namespace MailDeliveryTool.App.Converters;

/// <summary>bool を反転する。処理中フラグ（true）でボタンを無効化する用途などに使う。</summary>
public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is bool b ? !b : value!;
}
