using Adamantium.Core.Events;
using Adamantium.Multiverse;
using Adamantium.Multiverse.Input;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Universes;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class UniverseOutputSubscriptionTests
{
    [Test]
    public void AfterSwitchingPanels_TheOldPanelsKeys_NoLongerReachTheOutput()
    {
        var first = new RenderTargetPanel();
        var second = new RenderTargetPanel();
        var output = Output(first);

        ((UniverseOutput)output).SwitchContext(new OutputContext(second));
        Press(first);

        Assert.That(output.Input.KeyboardInputs, Is.Empty);

        Press(second);

        Assert.That(output.Input.KeyboardInputs, Has.Count.EqualTo(1));
    }

    [Test]
    public void ADisposedOutput_NoLongerListensToItsPanel()
    {
        var panel = new RenderTargetPanel();
        var output = Output(panel);

        output.Dispose();
        Press(panel);

        Assert.That(output.Input.KeyboardInputs, Is.Empty);
    }

    private static RenderTargetUniverseOutput Output(RenderTargetPanel panel)
    {
        var output = new RenderTargetUniverseOutput(new EventAggregator(), new OutputContext(panel));
        output.Input = new InputWormhole(output, new GamepadHub(new NoGamepadBackend(), null));
        return output;
    }

    private static void Press(RenderTargetPanel panel)
    {
        ((IObservableComponent)panel).RaiseEvent(
            new KeyEventArgs(KeyboardDevice.CurrentDevice, Key.W, InputModifiers.None, 0) { RoutedEvent = Keyboard.KeyDownEvent });
    }
}
