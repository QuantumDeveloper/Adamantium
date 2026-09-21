using Adamantium.Core.Commands;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Behaviors;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Input;

/// <summary>
/// One object that makes its host a drag SOURCE - a Behaviors wrapper over the <see cref="DragDrop"/> attached properties
/// (AllowDrag / DragData / DragStarted / DragCompleted). Cleaner than sprinkling the attached properties, and its own
/// <c>{Binding}</c>s resolve because a Behavior joins the host's logical tree (so it shares the host's DataContext). Attach:
/// <code>&lt;Border&gt;&lt;Border.Behaviors&gt;&lt;DragSourceBehavior Data="{Binding}"/&gt;&lt;/Border.Behaviors&gt;&lt;/Border&gt;</code>
/// </summary>
public class DragSourceBehavior : Behavior<AdamantiumComponent>
{
    public static readonly AdamantiumProperty AllowDragProperty = AdamantiumProperty.Register(nameof(AllowDrag),
        typeof(bool), typeof(DragSourceBehavior), new PropertyMetadata(true, OnChanged));

    public static readonly AdamantiumProperty DataProperty = AdamantiumProperty.Register(nameof(Data),
        typeof(object), typeof(DragSourceBehavior), new PropertyMetadata(null, OnChanged));

    public static readonly AdamantiumProperty StartedCommandProperty = AdamantiumProperty.Register(nameof(StartedCommand),
        typeof(ICommand), typeof(DragSourceBehavior), new PropertyMetadata(null, OnChanged));

    public static readonly AdamantiumProperty CompletedCommandProperty = AdamantiumProperty.Register(nameof(CompletedCommand),
        typeof(ICommand), typeof(DragSourceBehavior), new PropertyMetadata(null, OnChanged));

    public static readonly AdamantiumProperty DragTemplateProperty = AdamantiumProperty.Register(nameof(DragTemplate),
        typeof(Adamantium.UI.Core.Templates.DataTemplate), typeof(DragSourceBehavior), new PropertyMetadata(null, OnChanged));

    public static readonly AdamantiumProperty AllowExternalDragProperty = AdamantiumProperty.Register(nameof(AllowExternalDrag),
        typeof(bool), typeof(DragSourceBehavior), new PropertyMetadata(false, OnChanged));

    /// <summary>Let this source be dragged INTO OTHER APPLICATIONS: the gesture is handed to the OS drag loop, and the
    /// payload's <c>DataFormats.Text</c> / <c>DataFormats.Files</c> (or a plain string payload) cross the process
    /// boundary. Off by default - the in-app path keeps the live CLR object and costs no OS machinery.</summary>
    public bool AllowExternalDrag { get => GetValue<bool>(AllowExternalDragProperty); set => SetValue(AllowExternalDragProperty, value); }

    /// <summary>Whether the host can start a drag (default true).</summary>
    public bool AllowDrag { get => GetValue<bool>(AllowDragProperty); set => SetValue(AllowDragProperty, value); }

    /// <summary>The dragged payload - usually <c>{Binding}</c> (the item behind this element).</summary>
    public object Data { get => GetValue(DataProperty); set => SetValue(DataProperty, value); }

    /// <summary>Fires when the drag begins (the source records where the payload came from).</summary>
    public ICommand StartedCommand { get => GetValue(StartedCommandProperty) as ICommand; set => SetValue(StartedCommandProperty, value); }

    /// <summary>Fires when the drag ends with the final Effects (the source removes on Move).</summary>
    public ICommand CompletedCommand { get => GetValue(CompletedCommandProperty) as ICommand; set => SetValue(CompletedCommandProperty, value); }

    /// <summary>Optional custom ghost: a DataTemplate pictured (bound to <see cref="Data"/>) instead of a snapshot of the host.</summary>
    public Adamantium.UI.Core.Templates.DataTemplate DragTemplate
    {
        get => GetValue(DragTemplateProperty) as Adamantium.UI.Core.Templates.DataTemplate;
        set => SetValue(DragTemplateProperty, value);
    }

    protected override void OnAttached(AdamantiumComponent component) => Sync();

    protected override void OnDetached(AdamantiumComponent component)
    {
        DragDrop.SetAllowDrag(component, false);
        DragDrop.SetDragData(component, null);
        DragDrop.SetDragStartedCommand(component, null);
        DragDrop.SetDragCompletedCommand(component, null);
        DragDrop.SetDragTemplate(component, null);
        DragDrop.SetAllowExternalDrag(component, false);
    }

    private static void OnChanged(AdamantiumComponent b, AdamantiumPropertyChangedEventArgs e) => ((DragSourceBehavior)b).Sync();

    // Push the behavior's state onto the host's DragDrop.* attached properties (on attach + whenever a bound value arrives).
    private void Sync()
    {
        if (AssociatedComponent is not { } c) return;
        DragDrop.SetAllowDrag(c, AllowDrag);
        DragDrop.SetDragData(c, Data);
        DragDrop.SetDragStartedCommand(c, StartedCommand);
        DragDrop.SetDragCompletedCommand(c, CompletedCommand);
        DragDrop.SetDragTemplate(c, DragTemplate);
        DragDrop.SetAllowExternalDrag(c, AllowExternalDrag);
    }
}
