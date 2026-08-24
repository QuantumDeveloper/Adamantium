using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Adamantium.Core.DependencyInjection;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Dispatcher;
using Adamantium.UI.Core.Graphics;
using Adamantium.UI.Core.Media.Animation;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.XamlTests;

/// <summary>
/// Runtime side of element-level (markup) triggers: a control declares &lt;Button.Triggers&gt; in AUML, the codegen
/// fills the control's <c>Triggers</c> collection, and the framework applies them via
/// FundamentalUIComponent.OnAttachedToLogicalTree -> ApplyTriggers when the control joins a live logical tree.
/// This drives the REAL attach path (not SelfTriggerContext) with the exact string-typed values the generator emits.
/// </summary>
[TestFixture]
public class ElementTriggerTests
{
    [OneTimeSetUp]
    public void EnsureAppContext()
    {
        // ApplyTriggers reaches through UIAppContext.Current; a minimal context with a no-op theme is enough.
        UIAppContext.Initialize(new FakeApp(new AdamantiumDependencyContainer()), null);
    }

    /// <summary>A visual root, so the element under test is in a LIVE tree - not merely a logical one. An animation
    /// deliberately waits for the VISUAL attach (AnimationManager.DeferIfOutOfTree: a spinner inside a view still being
    /// built off the loop thread must not start against a tree it is not in yet), so a test that only parents logically
    /// asserts something the engine is right not to do.</summary>
    private sealed class TestVisualRoot : Adamantium.UI.Controls.Panels.Grid, IRootVisualComponent
    {
        public Vector2 PointToClient(PixelPoint point) => new((float)point.X, (float)point.Y);
        public PixelPoint PointToScreen(Vector2 point) => new(point.X, point.Y);
        public PixelPoint Position { get; set; }
        public void AttachContextAndInitialize(IUIContext context) { }
        public double Left { get; set; }
        public double Top { get; set; }
        public string Title { get; set; }
        public double ClientWidth { get; set; }
        public double ClientHeight { get; set; }
        public IUIContext UIContext => null;
    }

    [Test]
    public void ElementLevelTrigger_AppliedOnAttach_FiresAnimationOnHover()
    {
        var parent = new TestVisualRoot();
        var button = new Button { Width = 120 };

        // Mirror exactly what the codegen emits: string Value / string keyframe setters.
        var trigger = new PropertyTrigger { Property = "IsMouseOver", Value = "true" };
        var anim = new Animation { Duration = TimeSpan.FromSeconds(1), Easing = new LinearEasing() };
        anim.KeyFrames.Add(KeyFrameWith("Width", 0.0, "120"));
        anim.KeyFrames.Add(KeyFrameWith("Width", 1.0, "150"));
        trigger.EnterActions.Add(new RunAnimationAction { Animation = anim });
        button.Triggers.Add(trigger);

        AnimationManager.Reset();

        // The real path: joining the tree -> SetParent -> OnAttachedToLogicalTree -> ApplyTriggers. Through Panel.Children,
        // so the button is parented in BOTH trees - which is what a control on screen actually is, and what the animation
        // waits for.
        parent.Children.Add(button);
        Assert.That(AnimationManager.HasActiveAnimations, Is.False, "nothing animates before the condition is met");

        // Hover: IsMouseOver crosses to true -> the EnterAction must start the keyframe animation.
        button.SetValue(InputUIComponent.IsMouseOverProperty, true);
        Assert.That(AnimationManager.HasActiveAnimations, Is.True,
            "element-level trigger's EnterAction did not fire on hover");

        AnimationManager.Tick(1.0);
        Assert.That(button.Width, Is.EqualTo(150).Within(0.001), "the animation did not drive Width to its end value");
    }

    private static KeyFrame KeyFrameWith(string property, double cue, object value)
    {
        var keyFrame = new KeyFrame { Cue = cue };
        keyFrame.Setters.Add(new Setter(property, value));
        return keyFrame;
    }
}
