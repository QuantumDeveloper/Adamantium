using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Theming happens ONE CONTROL AT A TIME, over and over, as the tree is built and walked - so a failure while theming
/// one of them decides how far the walk gets. Applying a theme runs markup: it resolves resources, writes setters,
/// attaches triggers and builds the control's template, and any of that can throw on a single bad attribute (an
/// unresolvable <c>{TemplateBinding}</c> resolves to a null property and throws while the template is BUILT).
/// <para>That throw used to travel up through SetParent and the logical-children walk and abandon the rest of the pass,
/// leaving the application HALF THEMED - some controls in the new theme, the rest still wearing the old one, and
/// nothing on screen saying why. It reads as "the new theme was never written", which is how it was actually
/// misdiagnosed for a while.</para>
/// <para>The boundary therefore sits at <see cref="FundamentalUIComponent.ApplyCurrentTheme"/> - the seam every control
/// goes through - rather than around any one thing that might throw inside it.</para>
/// </summary>
[TestFixture]
public class ThemingBoundaryTests
{
    [OneTimeSetUp]
    public void EnsureAppContext() => UIAppContext.Initialize(new FakeApp(new AdamantiumDependencyContainer()), null);

    // A control whose theming fails the way a bad attribute makes it fail.
    private sealed class Unthemeable : ContentControl
    {
        public int Attempts { get; private set; }

        protected override void ApplyCurrentThemeCore()
        {
            Attempts++;
            throw new InvalidOperationException("bad markup");
        }
    }

    private sealed class Themeable : ContentControl
    {
        public int Attempts { get; private set; }

        // Calls base: the point of this double is only to COUNT the attempts, so everything real theming does - and in
        // particular marking the control applied - has to keep happening.
        protected override void ApplyCurrentThemeCore()
        {
            Attempts++;
            base.ApplyCurrentThemeCore();
        }
    }

    /// <summary>The one that matters: theming the broken control does not throw at whoever is walking the tree, so the
    /// controls after it are still reached.</summary>
    [Test]
    public void ABrokenControlDoesNotStopTheWalk()
    {
        var walked = new List<ContentControl> { new Unthemeable(), new Themeable() };

        Assert.DoesNotThrow(() =>
        {
            foreach (var control in walked) control.ApplyCurrentTheme();
        });

        Assert.That(((Themeable)walked[1]).Attempts, Is.EqualTo(1),
            "the control after the broken one was never themed - the walk died on the first failure");
    }

    /// <summary>A failure is not retried. The cause is a fixed defect in markup, so the next attempt fails identically -
    /// and LayoutManager re-themes anything still unapplied on EVERY pass, which would turn one bad attribute into an
    /// exception per frame and a log nobody can read.</summary>
    [Test]
    public void AFailureIsNotRetriedForever()
    {
        var control = new Unthemeable();

        control.ApplyCurrentTheme();

        Assert.That(control.IsStyleApplied, Is.True,
            "left unapplied, the layout pass would re-theme it - and fail - every single frame");
    }

    /// <summary>The boundary must not swallow the ordinary path: a control that themes cleanly is themed exactly
    /// once.</summary>
    [Test]
    public void AControlThatThemesCleanlyIsUntouched()
    {
        var control = new Themeable();

        control.ApplyCurrentTheme();

        Assert.Multiple(() =>
        {
            Assert.That(control.Attempts, Is.EqualTo(1));
            Assert.That(control.IsStyleApplied, Is.True);
        });
    }
}
