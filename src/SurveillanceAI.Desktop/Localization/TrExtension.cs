using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;
using SurveillanceAI.Application.Localization;

namespace SurveillanceAI.Desktop.Localization;

/// <summary>Live resource binding: labels change without rebuilding views or restarting monitoring.</summary>
public sealed class TrExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => new Binding($"[{key}]")
    {
        Source = Texts.Instance,
        Mode = BindingMode.OneWay
    }.ProvideValue(serviceProvider);
}

public sealed class LocalizedValueConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        => values.Length == 0 || values[0] == DependencyProperty.UnsetValue ? "" : Texts.Display(values[0]?.ToString() ?? "");
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
