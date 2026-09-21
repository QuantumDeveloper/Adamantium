using System.Linq;
using Adamantium.Mathematics;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>A group has to be able to say what it would COST at each of its variants before anything picks one. The
/// answers are measured, so they are also kept - the search asks the same question many times per layout pass.</summary>
[TestFixture]
public class RibbonGroupWidthTests
{
    // Stand-ins for commands: the width they report depends on the size the group gave them, which is exactly what a
    // real command's template does through its Ribbon.Size triggers.
    private sealed class SizedCommand : Border
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            Width = Ribbon.GetSize(this) switch
            {
                RibbonSize.Large => 60,
                RibbonSize.Medium => 40,
                _ => 20
            };
            Height = 60;
            return base.MeasureOverride(availableSize);
        }
    }

    private static RibbonGroupPanel Packed(int commands, RibbonSize max = RibbonSize.Large)
    {
        var panel = new RibbonGroupPanel();
        for (var i = 0; i < commands; i++)
        {
            var command = new SizedCommand();
            Ribbon.SetMaxSize(command, max);
            panel.Children.Add(command);
        }

        panel.Measure(Size.Infinity);
        return panel;
    }

    // A group that has to tighten must actually get narrower for it - a variant that costs the same as the one before
    // it is a step the search would take for nothing.
    [Test]
    public void EachVariant_IsNoWiderThanTheOneBeforeIt()
    {
        var panel = Packed(3);

        var widths = Enumerable.Range(0, panel.Variants.Count)
            .Select(panel.WidthAt)
            .Where(w => !double.IsNaN(w))
            .ToArray();

        Assert.Multiple(() =>
        {
            Assert.That(widths, Is.Ordered.Descending.Or.Ordered, "widths: " + string.Join(", ", widths));
            Assert.That(widths.First(), Is.GreaterThan(widths.Last()), "and the tightest is genuinely tighter");
        });
    }

    // The roomiest variant is what a MEASURE answers with: the group asks for what its author wanted, and the choice is
    // made later from the arrange slot. A group that answered a probing measure with a shrunken variant would talk
    // itself into being small.
    [Test]
    public void AMeasure_AnswersWithTheRoomiestVariant()
    {
        var panel = Packed(3);

        Assert.That(panel.DesiredSize.Width, Is.EqualTo(panel.WidthAt(0)));
    }

    // Measured once, then remembered. The search asks each width repeatedly while it narrows things down, and paying a
    // full re-measure per question is O(groups x variants) of real layout work every pass.
    [Test]
    public void AskingTheSameWidthTwice_MeasuresOnce()
    {
        var panel = Packed(3);
        for (var i = 0; i < panel.Variants.Count; i++) panel.WidthAt(i);

        var before = MeasurableUIComponent.TotalMeasureCores;
        for (var round = 0; round < 5; round++)
            for (var i = 0; i < panel.Variants.Count; i++)
                panel.WidthAt(i);

        Assert.That(MeasurableUIComponent.TotalMeasureCores, Is.EqualTo(before), "no measure ran for a known width");
    }

    // ...and forgotten as soon as the answers stop being about the current content. Probed at the ROOMIEST variant,
    // where every command owns a column: at the tightest they all stack into one and a third command costs nothing,
    // which would let a stale cache pass for a fresh one.
    [Test]
    public void AddingACommand_ForgetsTheMeasuredWidths()
    {
        var panel = Packed(2);
        var before = panel.WidthAt(0);

        panel.Children.Add(new SizedCommand());
        panel.Measure(Size.Infinity);

        Assert.That(panel.WidthAt(0), Is.EqualTo(before + 60), "the third command is paid for at the roomiest variant");
    }

    // Applying a variant is what actually draws it: the commands carry the sizes it names.
    [Test]
    public void ApplyingAVariant_SizesTheCommands()
    {
        var panel = Packed(2);
        var tightest = panel.Variants.Count - 2;   // the last one before collapsed

        panel.Apply(tightest);

        Assert.That(panel.Children.Select(Ribbon.GetSize), Is.All.EqualTo(RibbonSize.Small));
    }

    // A probe must leave no trace in the LAYOUT either, not just in the sizes: MeasurePacked also derives the columns
    // and their widths.
    //
    // WEAK: it passes with the restoring re-measure removed as well, because arranging a measure-invalid panel
    // re-measures it and repairs the columns before anything can see them. Kept as a guard against the ordering
    // changing, NOT as evidence - the real symptom was found in a log from the running window.
    [Test]
    public void ProbingAWidth_LeavesTheAppliedVariantLaidOutCorrectly()
    {
        var panel = Packed(3);
        var slot = new Rect(0, 0, panel.DesiredSize.Width, panel.DesiredSize.Height);

        panel.Arrange(slot);
        var before = panel.Children.Select(c => c.Bounds).ToArray();

        // What the search does: ask about every variant, then arrange - with no measure in between to tidy up after it.
        // A LARGER slot, because arranging into the same rect twice short-circuits and would never reach the columns.
        for (var i = 0; i < panel.Variants.Count; i++) panel.WidthAt(i);
        panel.Arrange(new Rect(0, 0, slot.Width + 1, slot.Height));

        var after = panel.Children.Select(c => c.Bounds).ToArray();

        Assert.That(after, Is.EqualTo(before),
            "asking what the other variants cost changed where this one puts its commands");
    }

    // The collapsed variant has no width here - it is a button the theme draws, and answering with a number nobody
    // measured would be a guess the search then trusted.
    [Test]
    public void TheCollapsedVariant_HasNoWidthYet()
    {
        var panel = Packed(2);

        Assert.That(panel.WidthAt(panel.Variants.Count - 1), Is.NaN);
    }
}
