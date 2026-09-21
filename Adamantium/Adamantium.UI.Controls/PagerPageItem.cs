using System;

namespace Adamantium.UI.Controls;

/// <summary>
/// One entry in a <see cref="DataPager"/>'s row of page numbers - either a page to jump to, or the ellipsis standing in
/// for the pages left out.
/// <para>An ellipsis is an entry rather than a hole in the template on purpose: with one shape for both, the row is a
/// plain list of buttons and needs no way to ask "is this one a number or an ellipsis" in markup. The engine ships no
/// value converters, so a template that had to branch would have nothing to branch WITH.</para>
/// <para>AND IT IS LIVE, not a label: it steps one page towards the side it is on, which is also the direction it is
/// pointing. A dead button in the middle of a row of live ones is a place the pointer goes to be refused.</para>
/// </summary>
public sealed class PagerPageItem
{
    internal PagerPageItem(string text, bool isCurrent, bool isEnabled, ICommand command)
    {
        Text = text;
        IsCurrent = isCurrent;
        IsEnabled = isEnabled;
        Command = command;
    }

    /// <summary>What the button shows: a page number, counted from one as a reader counts, or an ellipsis.</summary>
    public string Text { get; }

    /// <summary>True for the page being shown - what marks it in the row.</summary>
    public bool IsCurrent { get; }

    /// <summary>False only where there is nowhere to go.</summary>
    public bool IsEnabled { get; }

    /// <summary>Turns to this page - to the page itself for a number, one step towards it for an ellipsis.</summary>
    public ICommand Command { get; }
}
