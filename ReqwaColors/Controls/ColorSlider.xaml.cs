using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Foundation;

namespace ReqwaColors.Controls;

/// <summary>
/// A labeled slider with live value readout. Wraps the WinUI Slider
/// (themed purple via resources) and adds Reqwa typography.
/// Supports keyboard control natively through the underlying Slider.
/// </summary>
public sealed partial class ColorSlider : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(ColorSlider), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
        nameof(Value), typeof(double), typeof(ColorSlider),
        new PropertyMetadata(0.0, OnValuePropertyChanged));

    public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
        nameof(Minimum), typeof(double), typeof(ColorSlider), new PropertyMetadata(0.0));

    public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
        nameof(Maximum), typeof(double), typeof(ColorSlider), new PropertyMetadata(100.0));

    public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
        nameof(Step), typeof(double), typeof(ColorSlider), new PropertyMetadata(1.0));

    public static readonly DependencyProperty FormatProperty = DependencyProperty.Register(
        nameof(Format), typeof(string), typeof(ColorSlider),
        new PropertyMetadata("0", OnValuePropertyChanged));

    public ColorSlider()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    /// <summary>.NET format string for the value readout, e.g. "0 '%'" or "0.00".</summary>
    public string Format
    {
        get => (string)GetValue(FormatProperty);
        set => SetValue(FormatProperty, value);
    }

    /// <summary>Formatted value shown at the right of the title row.</summary>
    public string ValueLabel
    {
        get
        {
            string fmt = Format?.Length > 0 ? Format : "0";
            return Value.ToString(fmt).Replace("%", "％");
        }
    }

    private static void OnValuePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ColorSlider)d;
        self.ValueText.Text = self.ValueLabel;
    }

    private static void OnFormatPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var self = (ColorSlider)d;
        self.ValueText.Text = self.ValueLabel;
    }
}