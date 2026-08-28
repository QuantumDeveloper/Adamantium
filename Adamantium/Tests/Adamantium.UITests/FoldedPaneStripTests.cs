using Adamantium.UI.Controls.Docking;
using Adamantium.UI.Controls.Panels;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.UITests;

/// <summary>
/// Folding a docking panel against a side turns its tab labels ninety degrees, and a turned label needs a completely
/// different amount of room: it stops being as wide as its text and starts being as TALL as it.
/// <para>Measured on the stand, with a panel folded against the right edge: the strip asked for 179 and its group was
/// laid out at 78 - which is three tabs at the 26 of an UNTURNED row. The group had been measured before the labels
/// turned and never measured again, so the strip was clipped to a third of itself. It came right the moment anything
/// forced another layout pass, which is why clicking a tab appeared to fix it.</para>
/// </summary>
[TestFixture]
public class FoldedPaneStripTests
{
    /// <summary>How much room a pane needs is its PARENT's business. Turning the label is a change of exactly that, so
    /// it has to invalidate the parent as well - <c>AffectsMeasure</c> alone only re-measures the pane itself, and the
    /// strip around it goes on reporting the width it worked out for text lying flat.</summary>
    [Test]
    public void TurningAPanesLabel_InvalidatesTheStripAroundIt()
    {
        var pane = new Pane { Header = "Inspector" };
        var strip = new StackPanel { Orientation = Orientation.Vertical };
        strip.Children.Add(pane);

        strip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Assert.That(strip.IsMeasureValid, Is.True, "measured once, so the change below is the only thing under test");

        pane.LabelRotation = PaneLabelRotation.Left;

        Assert.That(strip.IsMeasureValid, Is.False,
            "a turned label changes what the pane asks for, and only the parent can act on that");
    }
}
