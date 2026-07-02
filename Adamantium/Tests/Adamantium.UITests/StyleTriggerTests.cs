using Adamantium.Mathematics;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Decorators;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Controls;
using Adamantium.UI.Core.Data;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.Resources.Triggers;
using Adamantium.UI.Core.Templates;
using Adamantium.UI.Core.TypeParsers;
using NUnit.Framework;

namespace Adamantium.UITests;

[TestFixture]
public class StyleTriggerTests
{
    // Regression: a trigger setter value can be a {ResourceReference} marker. The activator must resolve it as a marker,
    // not blindly CastFromString it - the latter stringified the marker into the brush parser and crashed
    // (KeyNotFoundException "...ResourceReference") the moment a hover/press/disabled trigger first fired.
    [Test]
    public void PropertyTrigger_ResourceReferenceSetter_DoesNotReachBrushParser()
    {
        var host = new Border();
        var trigger = new PropertyTrigger { Property = "IsEnabled", Value = false };
        trigger.Add(new Setter { Property = "Background", Value = new ResourceReference("ControlFillColorDefault") });

        trigger.Apply(new PartContext(host, theme: null));

        Assert.DoesNotThrow(() => host.IsEnabled = false,
            "ResourceReference marker is routed through the theme, never parsed as a colour string");
    }

    // The unification: ONE template, the variants only differ in the control's state brushes. A trigger reads a brush
    // off the templated control via {TemplateBinding} and projects it onto a named part - live, and undone on exit.
    [Test]
    public void PropertyTrigger_TemplateBindingSetter_ProjectsHostBrushOntoPart()
    {
        var disabled = new SolidColorBrush(Colors.Red);
        var host = new Button { BackgroundDisabled = disabled };
        host.Template = NamedPartTemplate("InnerBorder");   // set == applied synchronously

        var trigger = new PropertyTrigger { Property = "IsEnabled", Value = false };
        trigger.Add(new Setter
        {
            TargetName = "InnerBorder",
            Property = "Background",
            Value = new TemplateBinding { Path = "BackgroundDisabled" }
        });
        trigger.Apply(new PartContext(host, theme: null));

        var part = (Border)host.GetTemplateChild("InnerBorder");
        var rest = part.Background;
        Assert.That(rest, Is.Not.SameAs(disabled), "inactive before the condition holds");

        host.IsEnabled = false;
        Assert.That(part.Background, Is.SameAs(disabled), "{TemplateBinding} read the host's state brush onto the part");

        var swapped = new SolidColorBrush(Colors.RoyalBlue);
        host.BackgroundDisabled = swapped;
        Assert.That(part.Background, Is.SameAs(swapped), "live: a host brush change flows to the part while the trigger holds");

        host.IsEnabled = true;
        Assert.That(part.Background, Is.SameAs(rest), "reverts to the rest value once the condition no longer holds");
    }

    // Clockwork lifecycle: swapping a control's template at runtime must re-point an active part-targeting trigger onto
    // the NEW part and fully clean the OLD one - no stale trigger value left behind, no leaked subscription. Exercises
    // the real path: Style.Attach registers the activator on the control, and TemplatedUIComponent re-wires it on the
    // Template change (Style.ReevaluateActivators).
    [Test]
    public void StyleTrigger_TemplateSwap_RetargetsToNewPartAndCleansOld()
    {
        var disabled = new SolidColorBrush(Colors.Purple);
        var button = new Button { BackgroundDisabled = disabled };

        var style = new Style();
        style.Selector.Types.Add(typeof(Button));
        var trigger = new PropertyTrigger { Property = "IsEnabled", Value = false };
        trigger.Add(new Setter
        {
            TargetName = "InnerBorder",
            Property = "Background",
            Value = new TemplateBinding { Path = "BackgroundDisabled" }
        });
        style.Triggers.Add(trigger);
        style.Attach(button);

        button.Template = NamedPartTemplate("InnerBorder");          // template A
        var partA = (Border)button.GetTemplateChild("InnerBorder");

        button.IsEnabled = false;
        Assert.That(partA.Background, Is.SameAs(disabled), "trigger applied to the first template's part");

        button.Template = NamedPartTemplate("InnerBorder");          // swap -> template B, condition still holds
        var partB = (Border)button.GetTemplateChild("InnerBorder");

        Assert.That(partB, Is.Not.SameAs(partA), "a genuinely new part instance");
        Assert.That(partB.Background, Is.SameAs(disabled), "re-pointed onto the new template's part after the swap");
        Assert.That(partA.Background, Is.Not.SameAs(disabled), "old part cleaned - no stale trigger value left behind");
    }

    // A property-condition selector ("Button[IsEnabled=false]", Avalonia-style) parses into the structural facet (type)
    // PLUS a runtime Condition; the condition does NOT gate attachment (see StyleSelector.Match / Style.Attach).
    [Test]
    public void SelectorParser_ExtractsPropertyConditions_KeepingTheStructuralPart()
    {
        var selector = new SelectorParser().Parse("Button[IsEnabled=false]");
        Assert.Multiple(() =>
        {
            Assert.That(selector.Types.Contains(typeof(Button)), Is.True, "the structural type is still parsed");
            Assert.That(selector.HasConditions, Is.True);
            Assert.That(selector.Conditions, Has.Count.EqualTo(1));
            Assert.That(selector.Conditions[0].Property, Is.EqualTo("IsEnabled"));
            Assert.That(selector.Conditions[0].Value, Is.EqualTo("false"));
        });
    }

    // DataTrigger: active while a {Binding} on the host resolves to a value. The binding runs in producer mode against
    // the host's DataContext; a source-property change re-evaluates and applies/undoes the trigger's setter live. Setter
    // values are strings, as from markup (the setter machinery parses them to the target type).
    [Test]
    public void DataTrigger_AppliesSetter_WhileBoundValueMatches()
    {
        var vm = new ActiveVm { IsActive = false };
        var host = new Border { DataContext = vm };

        var trigger = new DataTrigger { Binding = new Binding("IsActive"), Value = "true" };
        trigger.Add(new Setter { Property = "Background", Value = "Red" });
        trigger.Apply(new PartContext(host, theme: null));

        Assert.That((host.Background as SolidColorBrush)?.Color, Is.Not.EqualTo(Colors.Red), "inactive while the bound value doesn't match");

        vm.IsActive = true;
        Assert.That((host.Background as SolidColorBrush)?.Color, Is.EqualTo(Colors.Red), "applied once the bound value matched");

        vm.IsActive = false;
        Assert.That((host.Background as SolidColorBrush)?.Color, Is.Not.EqualTo(Colors.Red), "undone when the bound value no longer matches");
    }

    // Regression: a trigger/style setter on Width (or Height) that is later CLEARED must not crash. The size callback
    // cast e.NewValue to double, which threw on the UnsetValue a cleared setter produces (guarded now).
    [Test]
    public void PropertyTrigger_ClearingAWidthSetter_DoesNotThrow()
    {
        var host = new Border { IsEnabled = true };
        var trigger = new PropertyTrigger { Property = "IsEnabled", Value = false };
        trigger.Add(new Setter { Property = "Width", Value = "120" });
        trigger.Apply(new PartContext(host, theme: null));

        host.IsEnabled = false;
        Assert.That(host.Width, Is.EqualTo(120.0), "the trigger applied its Width");

        Assert.DoesNotThrow(() => host.IsEnabled = true, "clearing the Width setter must not (double)UnsetValue-throw");
        Assert.That(host.Width, Is.Not.EqualTo(120.0), "Width reverted to its base once the trigger let go");
    }

    // ZIndex is a real AdamantiumProperty, so styles/triggers/AUML can set it by name (what makes the render + hit-test
    // paint order themeable). A raised child draws over its siblings (composited by ZIndex in RenderCache).
    [Test]
    public void ZIndex_IsARegisteredAdamantiumProperty_SettableByName()
    {
        var host = new Border();
        Assert.That(host.ZIndex, Is.EqualTo(0), "default document order");
        Assert.That(host.GetProperty("ZIndex"), Is.Not.Null, "registered DP");

        host.SetValue("ZIndex", 3, ValuePriority.Local);
        Assert.That(host.ZIndex, Is.EqualTo(3), "settable by name (the style/trigger/AUML path)");
    }

    // Re-applying a style without a preceding detach (the theme-swap path) must not pile up activators, and Detach must
    // fully remove the style's own activators (no leaked, still-subscribed trigger). Exercises Style's per-component
    // activator tracking (RemoveMyActivators / RecordActivator).
    [Test]
    public void Style_ReAttachThenDetach_AppliesOnceAndFullyDetaches()
    {
        var host = new Border();
        var style = new Style();
        style.Selector.Types.Add(typeof(Border));
        var trigger = new PropertyTrigger { Property = "IsEnabled", Value = false };
        trigger.Add(new Setter { Property = "Width", Value = "50" });
        style.Triggers.Add(trigger);

        style.Attach(host);
        style.Attach(host);   // re-apply without detach
        host.IsEnabled = false;
        Assert.That(host.Width, Is.EqualTo(50.0), "re-attached style still applies its trigger");

        style.Detach(host);
        host.IsEnabled = true;
        host.IsEnabled = false;
        Assert.That(host.Width, Is.Not.EqualTo(50.0), "fully detached - the activator is gone, not left subscribed");
    }

    private sealed class ActiveVm : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set
            {
                _isActive = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsActive)));
            }
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
    }

    private static ControlTemplate NamedPartTemplate(string partName)
    {
        var inner = new Border();
        return new ControlTemplate(() =>
        {
            var result = new TemplateResult();
            result.RegisterName(partName, inner);
            result.RootComponent = inner;
            return result;
        });
    }

    // Mirrors StyleTriggerExecutionContext (internal): the host for an empty TargetName, otherwise a named part of the
    // host's template - the path that lets a standalone "triggers" style reach the parts a ControlTemplate defines.
    private sealed class PartContext : ITriggerExecutionContext
    {
        private readonly IFundamentalUIComponent _host;
        public PartContext(IFundamentalUIComponent host, ITheme theme) { _host = host; Theme = theme; }
        public IFundamentalUIComponent HostComponent => _host;
        public ITheme Theme { get; }
        public IAdamantiumComponent FindTarget(string targetName) =>
            string.IsNullOrEmpty(targetName) ? _host : (_host as ITemplatedUIComponent)?.GetTemplateChild(targetName);
    }
}
