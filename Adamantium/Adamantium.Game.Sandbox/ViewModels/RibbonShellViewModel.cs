using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.Core.Commands;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Core;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>Content view model of the RIBBON window: a command band over a document surface, with the quick-access bar
/// in the caption. Names its window shell through <see cref="IWindowAware"/>, so it never touches a Window type.</summary>
[ViewModel]
public partial class RibbonShellViewModel : IWindowAware
{
    public string WindowShellKey => "ribbon";
    public string Title => "Ribbon — Adamantium editor shell";
    public double Width => 1180;
    public double Height => 720;

    /// <summary>The demo's stand-in for a document.</summary>
    [Bindable] private string _status = "Ready.";

    // This window has its OWN swapchain, so its presentation is its own setting - which is the point of comparing the
    // two shells side by side: one can run unthrottled while the other stays tear-free, in one process.
    public Adamantium.Graphics.Core.Presentation.PresentPolicy[] PresentPolicies { get; } =
        Enum.GetValues<Adamantium.Graphics.Core.Presentation.PresentPolicy>();

    [Bindable] private Adamantium.Graphics.Core.Presentation.PresentPolicy _presentPolicy =
        Adamantium.Graphics.Core.Presentation.PresentPolicy.Inherit;

    /// <summary>Gates Cut/Copy/Delete/Duplicate - toggle it in the View tab and half the Home tab goes dim.</summary>
    [Bindable, Affects(nameof(CutCommand), nameof(CopyCommand), nameof(DeleteCommand), nameof(DuplicateCommand))]
    private bool _hasSelection = true;

    /// <summary>Set by Cut/Copy; gates Paste.</summary>
    [Bindable, Affects(nameof(PasteCommand))] private bool _hasClipboard;

    // Two-way bound to RibbonToggleButtons, read back by the document surface.
    [Bindable] private bool _showGrid = true;
    [Bindable] private bool _showGizmos = true;
    [Bindable] private bool _wireframe;
    [Bindable] private bool _snapToGrid;

    [Bindable] private double _gridSize = 1.0;

    /// <summary>Read by BOTH quick-access bars - the one in the caption and the one in the ribbon's footer row. Each
    /// shows itself only while this names its own slot; the collection they list is this one view model's.</summary>
    [Bindable] private RibbonQuickAccessPlacement _quickAccessPlacement = RibbonQuickAccessPlacement.Caption;

    [Bindable] private string _shadingMode = "Lit";

    /// <summary>An icon held as DATA - plain path text, converted to a Geometry by the binding.</summary>
    public string MaterialIcon => "M3,2 L11,2 L13,4 L13,14 L3,14 Z M8,6 L8,11 M5.5,8.5 L10.5,8.5";

    public IEnumerable<string> ShadingModes { get; } = ["Lit", "Unlit", "Normals", "UV checker"];

    // The gates name the generated [Bindable] properties directly - both halves come out of one generator pass.
    [Command(CanExecute = nameof(HasClipboard))] private void Paste() => Status = "Pasted from the clipboard.";

    [Command] private void PasteKeepFormatting() => Status = "Pasted, keeping the formatting.";

    [Command] private void PasteValuesOnly() => Status = "Pasted the values only.";

    [Command] private void PasteSpecial() => Status = "Paste special...";

    /// <summary>The rows of Paste's drop-down, as DATA - which is what lets the command be put in the quick-access bar
    /// and keep its arrow there. Built on first read: the commands are generated, so they exist by then.</summary>
    public IReadOnlyList<MenuCommand> PasteOptions => _pasteOptions ??=
    [
        new MenuCommand { Header = "Keep formatting", Command = PasteKeepFormattingCommand },
        new MenuCommand { Header = "Values only", Command = PasteValuesOnlyCommand },
        new MenuCommand { Header = "Paste special...", Command = PasteSpecialCommand }
    ];

    private IReadOnlyList<MenuCommand> _pasteOptions;

    /// <summary>The rows of Add's drop-down - data for the same reason.</summary>
    public IReadOnlyList<MenuCommand> PrimitiveOptions => _primitiveOptions ??=
    [
        new MenuCommand { Header = "Cube", Command = AddCubeCommand },
        new MenuCommand { Header = "Sphere", Command = AddSphereCommand },
        new MenuCommand { Header = "Plane", Command = AddPlaneCommand },
        new MenuCommand { Header = "Empty entity", Command = AddEntityCommand }
    ];

    private IReadOnlyList<MenuCommand> _primitiveOptions;

    /// <summary>The rows of the right-click menu THIS shell wants on its snapping commands, instead of the one the
    /// ribbon offers. Nothing about them is the ribbon's business - they are the view model's own list, drawn by the
    /// template the view points <c>Ribbon.CommandContextMenuTemplate</c> at.</summary>
    public IReadOnlyList<MenuCommand> SnapMenuRows => _snapMenuRows ??=
    [
        new MenuCommand { Header = "Snap settings...", Command = SnapSettingsCommand },
        new MenuCommand { Header = "Clear all snaps", Command = ClearSnapsCommand }
    ];

    private IReadOnlyList<MenuCommand> _snapMenuRows;

    [Command] private void SnapSettings() => Status = "Snap settings...";

    [Command] private void ClearSnaps() => Status = "Snaps cleared.";

    [Command(CanExecute = nameof(HasSelection))] private void Cut()
    {
        HasClipboard = true;
        Status = "Cut the selection.";
    }

    [Command(CanExecute = nameof(HasSelection))] private void Copy()
    {
        HasClipboard = true;
        Status = "Copied the selection.";
    }

    [Command(CanExecute = nameof(HasSelection))] private void Delete()
    {
        HasSelection = false;
        Status = "Deleted the selection.";
    }

    [Command(CanExecute = nameof(HasSelection))] private void Duplicate() => Status = "Duplicated the selection.";

    [Command] private void SelectAll()
    {
        HasSelection = true;
        Status = "Selected everything.";
    }

    [Command] private void SelectNone()
    {
        HasSelection = false;
        Status = "Selection cleared.";
    }

    [Command] private void AddEntity() => Status = "Added an empty entity.";

    [Command] private void AddCube() => Add("cube");

    [Command] private void AddSphere() => Add("sphere");

    [Command] private void AddPlane() => Add("plane");

    [Command] private void Extrude() => Status = "Extruded the selected faces.";

    [Command] private void Bevel() => Status = "Bevelled the selected edges.";

    [Command] private void Subdivide() => Status = "Subdivided the mesh.";

    [Command] private void NewMaterial() => Status = "Created a new material.";

    [Command] private void EditAlbedo() => Status = "Editing the albedo channel.";

    [Command] private void EditNormal() => Status = "Editing the normal channel.";

    [Command] private void EditRoughness() => Status = "Editing the roughness channel.";

    /// <summary>The gallery's choices. DATA, so the dropped-down gallery can build its own cells from the same template.</summary>
    private static readonly MaterialSwatch[] MaterialChoices =
    [
        new MaterialSwatch { Name = "Steel", Fill = "#8A8F98" },
        new MaterialSwatch { Name = "Copper", Fill = "#B87333" },
        new MaterialSwatch { Name = "Gold", Fill = "#D4AF37" },
        new MaterialSwatch { Name = "Jade", Fill = "#4FA37A" },
        new MaterialSwatch { Name = "Cobalt", Fill = "#3B6FD4" },
        new MaterialSwatch { Name = "Ruby", Fill = "#C0334A" },
        new MaterialSwatch { Name = "Slate", Fill = "#4A5058" },
        new MaterialSwatch { Name = "Sand", Fill = "#C9B48A" },
        new MaterialSwatch { Name = "Ivory", Fill = "#E8E2D4" },
        new MaterialSwatch { Name = "Basalt", Fill = "#2E3238" },
        new MaterialSwatch { Name = "Moss", Fill = "#6B7A45" },
        new MaterialSwatch { Name = "Plum", Fill = "#7A4A85" }
    ];

    public IReadOnlyList<MaterialSwatch> Materials => MaterialChoices;

    [Bindable] private MaterialSwatch _selectedMaterial = MaterialChoices[0];

    /// <summary>What the contextual tabs hang on: switch it and "Mesh tools" appears in the strip with its own tabs.
    /// The ribbon only reads it - appearing is an offer, so the open tab is not pulled out from under anyone.</summary>
    [Bindable] private bool _hasMeshSelection;

    /// <summary>A second context, so the strip has to order two of them and draw two ledges.</summary>
    [Bindable] private bool _hasLightSelection;

    /// <summary>Whether the contexts draw their ledge. Off, the colour of the tabs is the only thing saying which
    /// belong together - and the strip stops paying the ledge row's height.</summary>
    [Bindable] private bool _showContextHeader = true;

    // Home carries a real editor's worth of groups, so the band's LAST resort - scrolling, once every group has been
    // collapsed and it still does not fit - is reachable by dragging the window narrow.
    [Command] private void AlignLeft() => Status = "Aligned to the left.";

    [Command] private void AlignCenter() => Status = "Centred.";

    [Command] private void Distribute() => Status = "Distributed evenly.";

    [Command] private void GroupSelection() => Status = "Grouped the selection.";

    [Command] private void BringForward() => Status = "Brought forward.";

    [Command] private void SendBackward() => Status = "Sent backward.";

    [Command] private void AddLight() => Status = "Added a light.";

    [Command] private void BakeLighting() => Status = "Baking the lighting.";

    [Command] private void AddCollider() => Status = "Added a collider.";

    [Command] private void Simulate() => Status = "Simulating.";

    [Command] private void Measure() => Status = "Measuring.";

    [Command] private void Annotate() => Status = "Annotating.";

    // The ribbon hands over a DESCRIPTION and never touches this collection - the shell decides what its own items are
    // made of. Here they are WindowCommands, the type the caption bar already lists.
    [Command] private void AddToQuickAccess(object request)
    {
        if (request is not RibbonQuickAccessEventArgs asked) return;


        var item = new QuickAccessCommand
        {
            IconData = asked.Icon as string,
            Label = asked.Label,
            ToolTip = asked.ToolTip as string,
            Key = asked.Key,
            Command = asked.Action,
            CommandParameter = asked.ActionParameter,
            // What is not a button (a slider) hands over its own compact form; a button leaves this null and is drawn
            // by the bar's default.
            QuickAccessTemplate = asked.Template,
            DropDownItems = asked.DropDownItems,
            DropDownItemTemplate = asked.DropDownItemTemplate
        };

        // A command WITH a state is one this view model already keeps a property for, so the item shows THAT property -
        // the button in the caption and the button in the ribbon end up two views of one value. Which command is which is
        // said in the markup by key; nothing here holds a control.
        Mirror(item, asked.Key as string);

        QuickAccess.Add(item);

        // Nothing is written back to the ribbon: it is pointed at this collection (Ribbon.QuickAccessItems in the view)
        // and recognises its own commands in it by key. A view model that kept the ribbon's control to mark it would be
        // holding a control.
        Status = "Added to the quick-access bar.";
    }

    // Which of this view model's own states each named command shows. A command that names none stays a plain button.
    private void Mirror(QuickAccessCommand item, string key)
    {
        switch (key)
        {
            case "ShowGrid":
                Mirror(item, nameof(ShowGrid), () => ShowGrid, value => ShowGrid = value);
                break;
            case "ShowGizmos":
                Mirror(item, nameof(ShowGizmos), () => ShowGizmos, value => ShowGizmos = value);
                break;
            case "Wireframe":
                Mirror(item, nameof(Wireframe), () => Wireframe, value => Wireframe = value);
                break;
            case "SnapToGrid":
                Mirror(item, nameof(SnapToGrid), () => SnapToGrid, value => SnapToGrid = value);
                break;
        }
    }

    // ONE value, two views of it: writing either side lands on the property, and the other side is told. No guard is
    // needed - a write that changes nothing raises nothing.
    private void Mirror(QuickAccessCommand item, string property, Func<bool> read, Action<bool> write)
    {
        item.IsChecked = read();
        item.PropertyChanged += (_, _) => write(item.IsChecked == true);

        void Follow(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName != property) return;

            item.IsChecked = read();
        }

        PropertyChanged += Follow;
        _mirrors[item] = Follow;
    }

    private readonly Dictionary<QuickAccessCommand, System.ComponentModel.PropertyChangedEventHandler> _mirrors = [];

    [Command] private void RemoveFromQuickAccess(object request)
    {
        if (request is not RibbonQuickAccessEventArgs asked) return;

        for (var i = QuickAccess.Count - 1; i >= 0; i--)
        {
            var item = QuickAccess[i] as QuickAccessCommand;
            var same = asked.Key != null
                ? Equals(item?.Key, asked.Key)
                : item?.Command != null && ReferenceEquals(item.Command, asked.Action);
            if (!same) continue;

            if (_mirrors.Remove(item, out var follow))
            {
                PropertyChanged -= follow;
            }

            QuickAccess.RemoveAt(i);
        }

        Status = "Removed from the quick-access bar.";
    }

    [Command] private void MoveQuickAccess()
    {
        var below = QuickAccessPlacement == RibbonQuickAccessPlacement.Caption;
        QuickAccessPlacement = below ? RibbonQuickAccessPlacement.BelowRibbon : RibbonQuickAccessPlacement.Caption;
        Status = below ? "Quick access moved below the ribbon." : "Quick access moved back to the caption.";
    }

    /// <summary>Which shape the File menu takes - the window-wide backstage, or the panel dropped under the button.</summary>
    [Bindable] private bool _isBackstage = true;

    [Command] private void Import(object format)
    {
        Status = $"Imported a {format} file.";
    }

    [Command] private void Export(object format)
    {
        Status = $"Exported the scene as {format}.";
    }

    [Command] private void NewScene() => Status = "New scene.";

    [Command] private void OpenScene() => Status = "Opened a scene.";

    [Command] private void Exit() => Status = "Exit requested.";

    [Command] private void Save() => Status = "Scene saved.";

    [Command] private void Undo() => Status = "Undone.";

    [Command] private void Redo() => Status = "Redone.";

    private void Add(string what)
    {
        HasSelection = true;
        Status = $"Added a {what}.";
    }

    // On the SHELL, not on either control: the user reorders it and it outlives a session. Lazy, so the generated
    // commands exist by first bind.
    private ObservableCollection<WindowCommand> _quickAccess;

    public ObservableCollection<WindowCommand> QuickAccess => _quickAccess ??=
    [
        new WindowCommand { IconData = "M3,2 L11,2 L13,4 L13,13 L3,13 Z M5,2 L5,6 L11,6 L11,2", Label = "Save", ToolTip = "Save the scene", Command = SaveCommand },
        new WindowCommand { IconData = "M6,4 L2,7 L6,10 M2,7 L10,7 A3,3 0 0 1 10,13 L8,13", Label = "Undo", ToolTip = "Undo", Command = UndoCommand },
        new WindowCommand { IconData = "M8,4 L12,7 L8,10 M12,7 L4,7 A3,3 0 0 0 4,13 L6,13", Label = "Redo", ToolTip = "Redo", Command = RedoCommand },
    ];
}
