using Adamantium.Core.Commands;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Input;

/// <summary>
/// One object that makes its host a drop TARGET - a Behaviors wrapper over the <see cref="DragDrop"/> attached properties
/// (AllowDrop / DropCommand / DragOverCommand). Its <c>{Binding}</c>s resolve because a Behavior joins the host's logical
/// tree (shares its DataContext). Attach:
/// <code>&lt;Border&gt;&lt;Border.Behaviors&gt;&lt;DropTargetBehavior DropCommand="{Binding Drop}"/&gt;&lt;/Border.Behaviors&gt;&lt;/Border&gt;</code>
/// </summary>
public class DropTargetBehavior : Behavior<AdamantiumComponent>
{
    public static readonly AdamantiumProperty AllowDropProperty = AdamantiumProperty.Register(nameof(AllowDrop),
        typeof(bool), typeof(DropTargetBehavior), new PropertyMetadata(true, OnChanged));

    public static readonly AdamantiumProperty DropCommandProperty = AdamantiumProperty.Register(nameof(DropCommand),
        typeof(ICommand), typeof(DropTargetBehavior), new PropertyMetadata(null, OnChanged));

    public static readonly AdamantiumProperty DragOverCommandProperty = AdamantiumProperty.Register(nameof(DragOverCommand),
        typeof(ICommand), typeof(DropTargetBehavior), new PropertyMetadata(null, OnChanged));

    /// <summary>Whether the host accepts drops (default true).</summary>
    public bool AllowDrop { get => GetValue<bool>(AllowDropProperty); set => SetValue(AllowDropProperty, value); }

    /// <summary>Runs on drop with the payload (the target ADDS to its collection).</summary>
    public ICommand DropCommand { get => GetValue(DropCommandProperty) as ICommand; set => SetValue(DropCommandProperty, value); }

    /// <summary>Runs every move while a drag is over the host (decide whether THIS payload can land here right now).</summary>
    public ICommand DragOverCommand { get => GetValue(DragOverCommandProperty) as ICommand; set => SetValue(DragOverCommandProperty, value); }

    protected override void OnAttached(AdamantiumComponent component) => Sync();

    protected override void OnDetached(AdamantiumComponent component)
    {
        DragDrop.SetAllowDrop(component, false);
        DragDrop.SetDropCommand(component, null);
        DragDrop.SetDragOverCommand(component, null);
    }

    private static void OnChanged(AdamantiumComponent b, AdamantiumPropertyChangedEventArgs e) => ((DropTargetBehavior)b).Sync();

    private void Sync()
    {
        if (AssociatedComponent is not { } c) return;
        DragDrop.SetAllowDrop(c, AllowDrop);
        DragDrop.SetDropCommand(c, DropCommand);
        DragDrop.SetDragOverCommand(c, DragOverCommand);
    }
}
