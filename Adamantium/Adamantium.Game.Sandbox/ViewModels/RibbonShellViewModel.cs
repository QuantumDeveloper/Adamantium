using System.Collections.Generic;
using System.Collections.ObjectModel;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls;

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

    [Bindable] private string _shadingMode = "Lit";

    /// <summary>An icon held as DATA - plain path text, converted to a Geometry by the binding.</summary>
    public string MaterialIcon => "M3,2 L11,2 L13,4 L13,14 L3,14 Z M8,6 L8,11 M5.5,8.5 L10.5,8.5";

    public IEnumerable<string> ShadingModes { get; } = ["Lit", "Unlit", "Normals", "UV checker"];

    // The gates name the generated [Bindable] properties directly - both halves come out of one generator pass.
    [Command(CanExecute = nameof(HasClipboard))] private void Paste() => Status = "Pasted from the clipboard.";

    [Command] private void PasteKeepFormatting() => Status = "Pasted, keeping the formatting.";

    [Command] private void PasteValuesOnly() => Status = "Pasted the values only.";

    [Command] private void PasteSpecial() => Status = "Paste special...";

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
