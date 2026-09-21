using System;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Extensions;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// Template that produces the <see cref="Panel"/> an <see cref="ItemsControl"/> lays its items out in. Like the other
/// templates it takes a <see cref="Func{TemplateResult}"/> builder (so the AUML generator emits it the same way as
/// ControlTemplate/DataTemplate); the result's <see cref="TemplateResult.RootComponent"/> is the panel.
/// </summary>
public class ItemsPanelTemplate : UiTemplate
{
    private readonly Func<TemplateResult> _builder;

    public ItemsPanelTemplate()
    {
    }

    public ItemsPanelTemplate(Func<TemplateResult> builder)
    {
        _builder = builder ?? throw new ArgumentNullException(nameof(builder));
    }

    /// <summary>Default items host: a vertical stack (the common list layout).</summary>
    public static ItemsPanelTemplate Default { get; } =
        new(() => new TemplateResult { RootComponent = new StackPanel { Orientation = Orientation.Vertical } });

    /// <summary>The panel is a templated part like any other, so it may state its settings as {TemplateBinding} against
    /// the control whose items it hosts - which is how a gallery tells its grid how many columns this size is drawn in.</summary>
    public override TemplateResult Build(IUIComponent templatedParent)
    {
        if (_builder == null)
            throw new InvalidOperationException("ItemsPanelTemplate has no builder.");

        var result = _builder();
        result.HostComponent = templatedParent;

        if (templatedParent == null) return result;

        result.RootComponent.TraverseVisualTree(component => ((FundamentalUIComponent)component).TemplatedParent = templatedParent);

        foreach (var binding in result.TemplateBindings)
        {
            binding.Source = templatedParent;
            binding.EstablishConnection();
        }

        foreach (var binding in result.Bindings)
        {
            binding.EstablishConnection();
        }

        return result;
    }
}
