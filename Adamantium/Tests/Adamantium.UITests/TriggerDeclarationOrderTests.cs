using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Extensions;
using NUnit.Framework;

namespace Adamantium.UITests;

// Two triggers writing the SAME property resolve by where they stand in the markup - the one written LOWER wins.
// Resolving by which fired last made the look depend on the history of events: a drop-down row that was both selected
// and keyboard-highlighted came out accent on its first showing and grey on the next, because closing dropped the
// highlight and reopening pushed it back on top of the selection.
[TestFixture]
public class TriggerDeclarationOrderTests
{
    private static readonly Brush Upper = Brushes.Red;    // declared first  - loses
    private static readonly Brush Lower = Brushes.Lime;   // declared second - wins

    private static Border Triggered()
    {
        var border = new Border { Width = 40, Height = 20 };

        border.Triggers.Add(Trigger(nameof(Border.IsEnabled), true, Upper));
        border.Triggers.Add(Trigger(nameof(Border.Focusable), true, Lower));

        var window = new Window { Width = 200, Height = 100, Content = border };
        for (var i = 0; i < 3; i++) WindowExtension.UpdateTree(window);

        return border;
    }

    private static PropertyTrigger Trigger(string property, object value, Brush background)
    {
        var trigger = new PropertyTrigger { Property = property, Value = value };
        trigger.Add(new Setter(nameof(Border.Background), background));
        return trigger;
    }

    [Test]
    public void TheOneWrittenLowerWins()
    {
        var border = Triggered();
        border.Focusable = true;
        border.IsEnabled = true;

        Assert.That(border.Background, Is.SameAs(Lower));
    }

    [Test]
    public void AndKeepsWinningWhenTheOtherIsRePUSHED()
    {
        var border = Triggered();
        border.Focusable = true;
        border.IsEnabled = true;

        // What reopening a drop-down does to the highlight: off, then on again - and it must not climb over the winner.
        border.IsEnabled = false;
        border.IsEnabled = true;

        Assert.That(border.Background, Is.SameAs(Lower), "a re-applied contribution must not outrank one written below it");
    }

    [Test]
    public void RemovingTheWinnerFallsBackToTheOther()
    {
        var border = Triggered();
        border.Focusable = true;
        border.IsEnabled = true;

        border.Focusable = false;

        Assert.That(border.Background, Is.SameAs(Upper), "leaving the top trigger restores the one beneath, not the default");
    }
}
