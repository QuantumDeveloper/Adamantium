using System;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Templates;

namespace Adamantium.UI.Controls;

/// <summary>
/// Template that produces the <see cref="Panel"/> an <see cref="ItemsControl"/> lays its items out in. Lives in
/// Controls (not Core) because it deals with <see cref="Panel"/>, a Controls type. The code path (a <see cref="Func{Panel}"/>
/// factory) is used now; AUML authoring of <c>&lt;ItemsPanelTemplate&gt;</c> reuses the markup template path later.
/// </summary>
public class ItemsPanelTemplate : UiTemplate
{
    private readonly Func<Panel> _factory;

    public ItemsPanelTemplate()
    {
    }

    public ItemsPanelTemplate(Func<Panel> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Default items host: a vertical stack (the common list layout).</summary>
    public static ItemsPanelTemplate Default { get; } = new(() => new StackPanel { Orientation = Orientation.Vertical });

    public override TemplateResult Build(IUIComponent templatedParent)
    {
        if (_factory == null)
            throw new InvalidOperationException("ItemsPanelTemplate has no factory; AUML-authored items panels land in a later phase.");

        return new TemplateResult { RootComponent = _factory() };
    }
}
