using Adamantium.UI.Core;

namespace Adamantium.UI.Controls;

/// <summary>What a cell MEANS, worked out from what it holds - "over the limit", "behind schedule", "settled". The other
/// half of <see cref="DataGridColumn.StateBinding"/>: that one reads a meaning the RECORD already carries, this one
/// derives it from the value, which is what conditional formatting actually is.
/// <para>It returns a MEANING, never a colour - the same rule the whole grid is built on. What "over the limit" looks
/// like belongs to the theme, and a rule that handed out brushes would be wrong the moment the theme changed. The
/// answer is matched by the theme's triggers, so it is whatever those compare against: a string in the sets shipped
/// here.</para>
/// <para>A <see cref="FundamentalUIComponent"/> so a rule written in markup can carry bound properties of its own - the
/// threshold read off the page rather than compiled in.</para></summary>
public abstract class DataGridStateRule : FundamentalUIComponent
{
    /// <summary>What <paramref name="value"/> means, or null when it means nothing in particular.
    /// <paramref name="item"/> is the whole record, so a rule can weigh one field against another.</summary>
    public abstract object State(object value, object item);
}
