using Adamantium.Core.TypeParsing;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.Game.Sandbox.Behaviors;

/// <summary>
/// View-layer behavior (NOT a view-model): on the button's click it swaps a keyed brush in the nearest ancestor's inline
/// <c>ResourceContext.Resources</c> for a NEW brush, then tells the resource system. A live <c>{ObservableResource Key}</c>
/// re-resolves to the new brush; a <c>{ResourceReference Key}</c> keeps the one it resolved once. Attach in markup:
/// <code>&lt;Button&gt;&lt;Button.Behaviors&gt;&lt;local:CycleResourceBehavior Key="PanelAccent"/&gt;&lt;/Button.Behaviors&gt;&lt;/Button&gt;</code>
/// </summary>
public class CycleResourceBehavior : Behavior<Button>
{
    private static readonly string[] Colors = ["#22D3EE", "#F472B6", "#A3E635", "#FB923C", "#818CF8"];
    private Button _button;
    private int _index;

    /// <summary>The resource key (in an ancestor's inline ResourceContext.Resources) to cycle.</summary>
    public string Key { get; set; }

    protected override void OnAttached(Button button)
    {
        _button = button;
        button.Click += OnClick;
    }

    protected override void OnDetached(Button button)
    {
        button.Click -= OnClick;
    }

    private void OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Key)) return;

        // Walk up to the element that declared this key in its inline resources, swap the entry for a NEW brush instance
        // (so {ResourceReference} keeps its old one and only {ObservableResource} follows), then notify - the coalesced
        // ResourcesChanged fires next layout pass and re-resolves the live reference.
        for (IUIComponent node = _button; node != null; node = node.VisualParent)
        {
            if (node is AdamantiumComponent ac && ResourceContext.GetResources(ac) is { } dict && dict.ContainsKey(Key))
            {
                _index = (_index + 1) % Colors.Length;
                dict[Key] = TypeParser.Parse<Brush>(Colors[_index]);
                UIAppContext.Current?.ResourceManager?.NotifyResourcesChanged();
                return;
            }
        }
    }
}
