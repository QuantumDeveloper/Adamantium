using System;

namespace Adamantium.UI;

/// <summary>
/// Provides helper methods needed for layout.
/// </summary>
public static class LayoutHelper
{
    /// <summary>
    /// Calculates a control's size based on its <see cref="MeMeasurableUiUiUiComponentidth"/>,
    /// <see cref="MeMeasurableUiUiUiComponenteight"/>, <see cref="MeMeasurableUiUiUiComponentinWidth"/>,
    /// <see cref="MeMeasurableUiUiUiComponentaxWidth"/>, <see cref="MeMeasurableUiUiUiComponentinHeight"/> and
    /// <see cref="MeMeasurableUiUiUiComponentaxHeight"/>.
    /// </summary>
    /// <param name="element">The control.</param>
    /// <param name="constraints">The space available for the control.</param>
    /// <returns>The control's size.</returns>
    public static Size ApplyLayoutConstraints(this MeasurableUIComponent element, Size constraints)
    {
        double width = (element.Width > 0) ? element.Width : constraints.Width;
        double height = (element.Height > 0) ? element.Height : constraints.Height;
        width = Math.Min(width, element.MaxWidth);
        width = Math.Max(width, element.MinWidth);
        height = Math.Min(height, element.MaxHeight);
        height = Math.Max(height, element.MinHeight);
        return new Size(width, height);
    }
}