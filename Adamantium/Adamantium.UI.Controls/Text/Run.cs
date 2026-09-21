using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.Text;

/// <summary>
/// A run of text with its own optional colour and size inside a <see cref="TextBlock"/>. Every property is a bindable
/// <see cref="AdamantiumProperty"/> and the run inherits the TextBlock's DataContext, so <c>Text</c> / <c>Foreground</c>
/// bind straight to the view-model (<c>&lt;Run Text="{Binding Name}" Foreground="{Binding Colour}"/&gt;</c>). An unset
/// <see cref="Foreground"/> / <see cref="FontSize"/> falls back to the owning TextBlock's.
/// </summary>
public class Run : Inline
{
    public static readonly AdamantiumProperty TextProperty = AdamantiumProperty.Register(nameof(Text),
        typeof(string), typeof(Run), new PropertyMetadata(string.Empty, OnRunPropertyChanged));

    public static readonly AdamantiumProperty ForegroundProperty = AdamantiumProperty.Register(nameof(Foreground),
        typeof(Brush), typeof(Run), new PropertyMetadata(null, OnRunPropertyChanged));

    // NaN = "inherit the TextBlock's FontSize".
    public static readonly AdamantiumProperty FontSizeProperty = AdamantiumProperty.Register(nameof(FontSize),
        typeof(double), typeof(Run), new PropertyMetadata(double.NaN, OnRunPropertyChanged));

    public string Text
    {
        get => GetValue<string>(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>This run's text colour; null inherits the TextBlock's <c>Foreground</c>.</summary>
    public Brush Foreground
    {
        get => GetValue<Brush>(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>This run's font size; NaN (default) inherits the TextBlock's <c>FontSize</c>.</summary>
    public double FontSize
    {
        get => GetValue<double>(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    private static void OnRunPropertyChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
        => (a as Run)?.RaiseChanged();
}
