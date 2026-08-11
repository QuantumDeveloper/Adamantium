using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using NUnit.Framework;

namespace Adamantium.UITests;

// The level machine: Alt shows the top level, typing a tab's keys descends into it, Escape steps back out one level
// rather than leaving, and a key nothing answers to ends the mode instead of leaving it stuck.
[TestFixture]
public class KeyTipSessionTests
{
    private class Tab : Border, IKeyTipTarget, ISelectable
    {
        public int Pressed;
        public bool IsSelected { get; set; }
        public void PressKeyTip()
        {
            Pressed++;
            IsSelected = true;
        }
    }

    private static Tab Scope(string keys, params IMeasurableComponent[] commands)
    {
        var tab = new Tab();
        KeyTipService.SetKeyTip(tab, keys);
        KeyTipService.SetIsScope(tab, true);

        var host = new StackPanel();
        foreach (var command in commands) host.Children.Add(command);
        tab.Child = host;

        return tab;
    }

    private static Button Command(string keys)
    {
        var button = new Button();
        KeyTipService.SetKeyTip(button, keys);
        return button;
    }

    [Test]
    public void BeginningShowsTheTopLevel()
    {
        var root = new StackPanel();
        root.Children.Add(Scope("H", Command("V")));
        root.Children.Add(Scope("N"));

        var session = new KeyTipSession(root);
        session.Begin();

        Assert.Multiple(() =>
        {
            Assert.That(session.IsActive, Is.True);
            Assert.That(session.Candidates, Has.Count.EqualTo(2), "the tabs, not their commands");
        });
    }

    [Test]
    public void TypingAScopeKeyDescendsIntoIt()
    {
        var home = Scope("H", Command("V"), Command("C"));
        var root = new StackPanel();
        root.Children.Add(home);

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('h');

        Assert.Multiple(() =>
        {
            Assert.That(home.Pressed, Is.EqualTo(1), "the tab is told, so it can select itself");
            Assert.That(session.Scope, Is.SameAs(home));
            Assert.That(session.Candidates, Is.Empty,
                "nothing is shown yet: the band still holds the tab that is leaving, and badging ITS commands for a "
                + "frame is the flicker this deferral exists to remove");
        });

        // The owner re-reads once layout has settled and the level is really there.
        session.Refresh();

        Assert.That(session.Candidates, Has.Count.EqualTo(2), "now its own commands");
    }

    [Test]
    public void TheLevelOneIsAlreadyOnShowsAtOnce()
    {
        var home = Scope("H", Command("V"), Command("C"));
        home.IsSelected = true;   // it is the tab already open - entering it re-lays out nothing
        var root = new StackPanel();
        root.Children.Add(home);

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('H');

        Assert.That(session.Candidates, Has.Count.EqualTo(2),
            "nothing is going to change, so nothing waits for a layout pass that will never come");
    }

    [Test]
    public void TypingACommandKeyRunsItAndLeaves()
    {
        var run = Command("V");
        var ran = 0;
        run.Click += (_, _) => ran++;

        var root = new StackPanel();
        root.Children.Add(Scope("H", run));

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('H');
        session.Press('V');

        Assert.Multiple(() =>
        {
            Assert.That(ran, Is.EqualTo(1));
            Assert.That(session.IsActive, Is.False, "running a command finishes the mode");
        });
    }

    [Test]
    public void EscapeStepsBackOneLevel()
    {
        var home = Scope("H", Command("V"));
        var root = new StackPanel();
        root.Children.Add(home);

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('H');
        session.Escape();

        Assert.Multiple(() =>
        {
            Assert.That(session.IsActive, Is.True, "one level back, not out");
            Assert.That(session.Scope, Is.SameAs(root));
        });
    }

    [Test]
    public void EscapeAtTheTopLeaves()
    {
        var root = new StackPanel();
        root.Children.Add(Scope("H"));

        var session = new KeyTipSession(root);
        session.Begin();
        session.Escape();

        Assert.That(session.IsActive, Is.False);
    }

    [Test]
    public void AHalfTypedKeyTipIsWhatEscapeDropsFirst()
    {
        var root = new StackPanel();
        root.Children.Add(Scope("FN"));
        root.Children.Add(Scope("FS"));

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('F');
        session.Escape();

        Assert.Multiple(() =>
        {
            Assert.That(session.IsActive, Is.True, "Escape after half a key tip means 'not that one'");
            Assert.That(session.Candidates, Has.Count.EqualTo(2), "and both are offered again");
        });
    }

    [Test]
    public void AKeyNothingAnswersToEndsTheMode()
    {
        var root = new StackPanel();
        root.Children.Add(Scope("H"));

        var session = new KeyTipSession(root);
        session.Begin();

        Assert.Multiple(() =>
        {
            Assert.That(session.Press('Z'), Is.True, "the key is still consumed - it was aimed at the mode");
            Assert.That(session.IsActive, Is.False);
        });
    }

    [Test]
    public void AKeystrokeIsTriedBothWays()
    {
        var home = Scope("H");
        var root = new StackPanel();
        root.Children.Add(home);

        var session = new KeyTipSession(root);
        session.Begin();

        // What a Russian layout produces on the key that carries H. The typed character means nothing here; the letter
        // the key carries does - and without the second reading the band would be unreachable from that keyboard.
        session.Press('р', 'H');

        Assert.That(session.Scope, Is.SameAs(home));
    }

    [Test]
    public void TheTypedCharacterIsTriedFirst()
    {
        var typed = Scope("Р");
        var latin = Scope("H");
        var root = new StackPanel();
        root.Children.Add(typed);
        root.Children.Add(latin);

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('р', 'H');

        Assert.That(session.Scope, Is.SameAs(typed), "a band labelled in the user's own language wins");
    }

    [Test]
    public void TwoLetterKeyTipsNarrowBeforeTheyAct()
    {
        var fn = Scope("FN");
        var root = new StackPanel();
        root.Children.Add(fn);
        root.Children.Add(Scope("FS"));

        var session = new KeyTipSession(root);
        session.Begin();
        session.Press('F');

        Assert.That(session.Candidates, Has.Count.EqualTo(2), "still ambiguous - nothing acts yet");

        session.Press('N');

        Assert.That(session.Scope, Is.SameAs(fn));
    }
}
