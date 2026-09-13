using Adamantium.UI.Controls.DataGrid;
using Adamantium.UI.Core;

namespace Adamantium.Game.Sandbox.Validation;

/// <summary>Conditional formatting in its plainest form: past a threshold the number MEANS something. The threshold is
/// BOUND from the view-model rather than written into the markup - what counts as a lot is a page's business and moves
/// while the page is open.
/// <para>It answers "Warning", not yellow. Which colour that is belongs to the theme, and the same page under another
/// one gets that theme's caution colour without a line changing here.</para></summary>
public class AboveThresholdRule : DataGridStateRule
{
    public static readonly AdamantiumProperty ThresholdProperty = AdamantiumProperty.Register(nameof(Threshold),
        typeof(int), typeof(AboveThresholdRule), new PropertyMetadata(int.MaxValue));

    public int Threshold
    {
        get => GetValue<int>(ThresholdProperty);
        set => SetValue(ThresholdProperty, value);
    }

    public override object State(object value, object item) =>
        int.TryParse(value?.ToString(), out var number) && number > Threshold ? "Warning" : null;
}
