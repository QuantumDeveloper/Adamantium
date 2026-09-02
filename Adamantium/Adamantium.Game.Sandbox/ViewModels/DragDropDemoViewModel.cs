using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.MVVM;
using Adamantium.Navigation;
using Adamantium.UI.Controls;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Core.Input;

namespace Adamantium.Game.Sandbox.ViewModels;

/// <summary>In-window drag-drop demo: drag items between two ListBoxes (Extended multi-select). The engine bakes the
/// pressed item into a cursor-following ghost (a real layered OS window) and delivers the payload to the drop target's
/// command; the whole selection travels. Move removes from the source, Copy (Ctrl) keeps it. No UI types in the VM.</summary>
[ViewModel]
public partial class DragDropDemoViewModel : TabPageViewModel
{
    // A long list so it scrolls/virtualizes - to verify the insertion index stays correct on a virtualized list.
    [Bindable] private ObservableCollection<string> _left = new(Enumerable.Range(1, 24).Select(i => $"Item {i:00}"));
    [Bindable] private ObservableCollection<string> _right = new(["Dog", "Cat"]);
    // A WrapPanel-hosted list: items flow left-to-right and wrap, so the insertion caret must run VERTICALLY (between
    // side-by-side chips) instead of horizontally - the orientation is derived from the host panel by the drag engine.
    [Bindable] private ObservableCollection<string> _wrap = new([
        "Red", "Green", "Blue", "Amber", "Violet", "Teal", "Coral", "Cyan", "Lime", "Pink",
        "Gold", "Indigo", "Olive", "Peach", "Mint", "Rose", "Sky", "Rust", "Plum", "Sand",
        "Jade", "Ruby", "Slate", "Ivory"]);

    // Two-way bound to each ListBox.SelectedItems, so a drag can pick up the whole selection.
    [Bindable] private ObservableCollection<string> _leftSelection = new();
    [Bindable] private ObservableCollection<string> _rightSelection = new();
    [Bindable] private ObservableCollection<string> _wrapSelection = new();

    // Flips the "Colors" list's WrapPanel flow at runtime (bound two-way to a DropDown + into WrapPanel.Orientation), so the
    // insertion caret can be checked live in both flows: Horizontal flow -> vertical caret, Vertical flow -> horizontal caret.
    public Orientation[] Orientations { get; } = [Orientation.Horizontal, Orientation.Vertical];
    [Bindable] private Orientation _wrapOrientation = Orientation.Horizontal;

    [Bindable] private ScrollBarVisibility _wrapHScroll = ScrollBarVisibility.Disabled;
    [Bindable] private ScrollBarVisibility _wrapVScroll = ScrollBarVisibility.Auto;

    // Toggle the "Animals" panel as a drop target: uncheck it and dragging over Animals shows the ⊘ "no-drop" cursor
    // (the engine maps Effects.None -> Cursors.No) - a live check of the cursor feedback.
    [Bindable] private bool _animalsAcceptDrop = true;

    // Bound to the DragHandle in every Fruits row: on, and only the grip starts a drag (the rest of the row just
    // selects); off, and the grip goes inactive so the whole row drags, as it does everywhere without a handle.
    [Bindable] private bool _handleOnlyDrag = true;

    // DragCursor: a target's DragOver may override the shape the engine picks from Effects (Copy/Move/⊘). Pick one here
    // and drag over Animals to see it - every standard shape the engine knows, so the whole platform catalog is visible.
    public CursorType[] CursorTypes { get; } = Enum.GetValues<CursorType>();
    [Bindable] private bool _overrideDragCursor;
    [Bindable] private CursorType _dragCursorType = CursorType.Hand;

    // Runs on every move while a drag is over Animals - the hook a target uses to say what would happen. Here it only
    // swaps the cursor; leaving DragCursor null keeps the standard Copy/Move/⊘ feedback. The view-model names a SHAPE
    // and nothing more - the enum converts to the shared cursor description on its own, so no UI object is built here.
    [Command]
    private void DragOverRight(object arg)
    {
        if (arg is not DragDropEventArgs e || !OverrideDragCursor) return;
        e.DragCursor = DragCursorType;
    }

    partial void OnWrapOrientationChanged(Orientation value)
    {
        var vertical = value == Orientation.Vertical;
        WrapHScroll = vertical ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
        WrapVScroll = vertical ? ScrollBarVisibility.Disabled : ScrollBarVisibility.Auto;
    }

    // A small in-memory tree to watch drag-drop on a TreeView: ghost, insertion caret between rows, and spring-load
    // (dwell over a folder -> it auto-expands). Nodes are draggable out; dropped items land as new root nodes (for now).
    [Bindable] private ObservableCollection<DragTreeNode> _tree = new([
        new DragTreeNode("Folder A", new DragTreeNode("A-1"), new DragTreeNode("A-2")),
        new DragTreeNode("Folder B",
            new DragTreeNode("B-1"),
            new DragTreeNode("Sub", new DragTreeNode("Deep-1"), new DragTreeNode("Deep-2"))),
        new DragTreeNode("Loose item"),
    ]);

    // OS interop (docs/DRAG_DROP_PLAN.md phases 5-6). Everything dropped in from ANOTHER application lands here, and the
    // "export" items below can be dragged OUT into one - both through the same commands an in-app drop uses.
    [Bindable] private ObservableCollection<string> _dropped = new();

    // Pictures dropped INTO the app, kept as the encoded bytes they arrived as - the view turns them into something
    // showable through a converter. A file drop counts too: Explorer hands over PATHS, never a bitmap, so a dragged
    // .png from a folder would otherwise be a picture the demo could not show.
    [Bindable] private ObservableCollection<Models.DroppedPicture> _droppedPictures = new();
    // One export per KIND of payload. The three pictures are stored as PNG, JPEG and GIF on disk and are handed over
    // exactly as they are: the engine turns whatever it was given into what each target asks for, so all three behave
    // identically on the way out. That is the point of them being here.
    [Bindable] private ObservableCollection<string> _exports = new([
        "Plain text note", "Another note", "A file (drag to Explorer)",
        "A picture, PNG", "A picture, JPEG", "A picture, GIF", "A picture, BMP", "A picture, TGA"]);

    private readonly INavigationService _navigation;

    public DragDropDemoViewModel(INavigationService navigation) : base("Drag & Drop")
    {
        _navigation = navigation;
    }

    // A drop from ANY source - Explorer, an editor, or one of our own lists. The view-model reads neutral formats and
    // never learns which application (or which platform's clipboard machinery) it came from.
    [Command]
    private void DropExternal(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        CollectPictures(e);
        // Land WHERE the drop happened: the engine reports the item the caret sat before (null = past the last one).
        // Appending regardless would throw away the insertion cue the user was aiming with.
        var at = e.InsertBefore is string before && Dropped.IndexOf(before) is var index and >= 0 ? index : Dropped.Count;
        foreach (var entry in Describe(e))
        {
            Dropped.Insert(at > Dropped.Count ? Dropped.Count : at, entry);
            at++;
        }
    }

    // One line per dropped thing, labelled by where it came from.
    private static IEnumerable<string> Describe(DragDropEventArgs e)
    {
        if (e.Data?.Get(DataFormats.Files) is string[] files && files.Length > 0)
        {
            return files.Select(file => $"file: {file}");
        }
        if (e.Data?.Get(DataFormats.Image) is byte[] image)
        {
            return [$"image: PNG, {image.Length} bytes"];
        }
        if (e.Data?.Get(DataFormats.Html) is string html)
        {
            return [$"html: {html}"];
        }
        if (e.Data?.Get(DataFormats.Rtf) is string rtf)
        {
            return [$"rtf: {rtf}"];
        }
        if (e.Data?.Get(DataFormats.Text) is string text)
        {
            return [$"text: {text}"];
        }
        return ItemsOf(e).Select(item => $"in-app: {item}");
    }

    // Anything picture-shaped in the drop, kept as bytes. Two sources, because the two are genuinely different drags:
    // a browser or another app hands over the picture itself; Explorer only ever hands over file PATHS.
    private void CollectPictures(DragDropEventArgs e)
    {
        // Land WHERE it was dropped, not at the end: the engine reports the item the insertion caret sat before
        // (null = past the last one), and ignoring it throws away the position the user was aiming at.
        var at = e.InsertBefore is Models.DroppedPicture before && DroppedPictures.IndexOf(before) is var index and >= 0
            ? index
            : DroppedPictures.Count;

        if (e.Data?.Get(DataFormats.Image) is byte[] { Length: > 0 } picture)
        {
            DroppedPictures.Insert(Math.Min(at, DroppedPictures.Count), new Models.DroppedPicture(picture));
            at++;
        }

        if (e.Data?.Get(DataFormats.Files) is not string[] files) return;
        foreach (var file in files)
        {
            if (!PictureExtensions.Contains(Path.GetExtension(file), StringComparer.OrdinalIgnoreCase)) continue;
            try
            {
                DroppedPictures.Insert(Math.Min(at, DroppedPictures.Count), new Models.DroppedPicture(File.ReadAllBytes(file)));
                at++;
            }
            catch (IOException)
            {
                // A file that vanished or is locked is not worth breaking the drop over.
            }
        }
    }

    private static readonly string[] PictureExtensions =
        [".png", ".jpg", ".jpeg", ".gif", ".bmp", ".tga", ".tif", ".tiff", ".ico", ".dds"];

    [Command]
    private void ClearDropped()
    {
        Dropped.Clear();
        DroppedPictures.Clear();
    }

    // The export list's drag start: what each item offers the outside world. A bare string payload already crosses out
    // as text on its own - everything here is the explicit, typed form, one item per kind of payload.
    [Command]
    private void ExportStarted(object arg)
    {
        if (arg is not DragDropEventArgs e || e.Data?.Get<string>() is not { } item) return;

        if (item.StartsWith("A picture"))
        {
            // The SAME neutral format for every encoding: hand over the bytes as they sit on disk and let the engine
            // render what each target wants (PNG for the modern ones, CF_DIB for the classic, a file for the many that
            // take nothing else). The file rendering comes from the engine because Program.cs opted in. Deferred, so a
            // drag that goes nowhere never touches the disk.
            var file = item switch
            {
                "A picture, JPEG" => "texture.jpg",
                "A picture, GIF" => "infinity.gif",
                "A picture, BMP" => "AplhaTestBitmap.bmp",
                // TGA is the interesting one: no target on the desktop accepts it, so every rendering the engine hands
                // over - the bitmap, the PNG, the file - is a conversion. If this drops, the conversion path works.
                "A picture, TGA" => "luxfon.tga",
                _ => "texture.png",
            };
            e.Data.SetDeferred(DataFormats.Image,
                () => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Textures", file)));
            return;
        }

        if (item.StartsWith("A file"))
        {
            // DEFERRED: the file is written only if the drop actually lands, and only when the target asks for the
            // format. Drag around, drop nowhere, and nothing was ever written to disk.
            e.Data.SetDeferred(DataFormats.Files, () =>
            {
                var path = Path.Combine(Path.GetTempPath(), "adamantium-drag-demo.txt");
                File.WriteAllText(path, $"Written on demand at {DateTime.Now:HH:mm:ss} - the drop asked for it.");
                return new[] { path };
            });
            return;
        }

        // Three renderings of the same note, so the target picks what it understands: Notepad takes the text, WordPad
        // and Word take the RTF or the HTML and keep the formatting. The engine wraps each in whatever the platform
        // demands (on Windows, CF_HTML's byte-offset header) - the view-model just names the format.
        e.Data.Set(DataFormats.Text, item);
        e.Data.Set(DataFormats.Html, $"<b>{item}</b> — <i>dragged out of Adamantium.UI</i>");
        e.Data.Set(DataFormats.Rtf, @"{\rtf1\ansi{\b " + item + @"}\i0 - dragged out of Adamantium.UI}");
    }

    // Opens the DEDICATED drag-drop window (its own ListBox) - drag items between it and these lists, both ways.
    [Command]
    private Task OpenDropWindow() =>
        _navigation.OpenWindowAsync<DragDropWindowViewModel>(windowShell: "dragdrop", singleInstance: true);

    // The list the current drag started from + the exact items being dragged - recorded on DragStarted, BEFORE any target
    // touches the lists, so DragCompleted removes the right items from the right place.
    private ObservableCollection<string> _dragOrigin;
    private List<string> _dragItems;

    // Each drop list paired with its SelectedItems, so origin/selection resolve uniformly across all of them.
    private (ObservableCollection<string> List, ObservableCollection<string> Selection)[] Lists =>
        [(Left, LeftSelection), (Right, RightSelection), (Wrap, WrapSelection)];

    [Command]
    private void DragStarted(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        var pressed = e.Data?.Get<string>();
        // Origin = the collection the drag ACTUALLY came from, matched by REFERENCE via SourceItemsSource - unambiguous even
        // after a Copy duplicated the value into another list (matching by value/selection would pick the wrong list, so a
        // Move deleted from the wrong place). Fall back to selection, then value, if the host didn't report the source.
        var match = Lists.FirstOrDefault(l => ReferenceEquals(l.List, e.SourceItemsSource));
        if (match.List == null) match = Lists.FirstOrDefault(l => l.Selection != null && l.Selection.Contains(pressed));
        if (match.List == null) match = Lists.FirstOrDefault(l => l.List.Contains(pressed));
        _dragOrigin = match.List;
        var selection = match.Selection;

        // Drag the whole selection when the pressed item is part of it; otherwise just the pressed item.
        _dragItems = selection != null && selection.Contains(pressed) && selection.Count > 1
            ? selection.ToList()
            : [pressed];
        e.Data?.Set(_dragItems);   // carried in the payload so a cross-window target can add them all
    }

    // A drop target only ADDS - it never removes from the source (which may live in another window / view-model).
    [Command]
    private void DropLeft(object arg) => Add(arg, Left);

    [Command]
    private void DropRight(object arg) => Add(arg, Right);

    [Command]
    private void DropWrap(object arg) => Add(arg, Wrap);

    // Hybrid tree drop: the engine hands us the hovered node (DropTarget) + where the drop lands (Placement). Into -> a
    // child (and auto-expand so it shows); Before/After -> a sibling in the target's parent collection; else a root node.
    // A node dragged from WITHIN the tree is MOVED (the same node reparented, not a copy); a string dragged in from another
    // panel becomes a fresh node.
    [Command]
    private void DropTree(object arg)
    {
        if (arg is not DragDropEventArgs e) return;
        var target = e.DropTarget as DragTreeNode;
        foreach (var node in DraggedNodes(e))
        {
            if (target != null && IsSelfOrAncestor(node, target)) continue;   // can't drop a node into its own subtree
            (node.Parent?.Children ?? Tree).Remove(node);   // detach from the old spot first -> MOVE (no-op for a fresh node)
            node.Parent = null;
            switch (e.Placement)
            {
                case DropPlacement.Into when target != null:
                    node.Parent = target;
                    target.Children.Add(node);   // add FIRST so the branch has the child when we expand...
                    target.IsExpanded = true;    // ...then open it - the flattener re-checks children live and splices
                    break;
                case DropPlacement.Before or DropPlacement.After when target != null:
                    var siblings = target.Parent?.Children ?? Tree;
                    node.Parent = target.Parent;
                    var at = siblings.IndexOf(target);
                    if (at < 0) at = siblings.Count;
                    siblings.Insert(e.Placement == DropPlacement.After ? at + 1 : at, node);
                    break;
                default:   // Into with no target (empty area = root), or a flat drop
                    Tree.Add(node);
                    break;
            }
        }
    }

    // The dragged tree nodes: the actual node for a tree-internal move (identity preserved -> reparent, not copy), or a
    // fresh node wrapping each string dropped in from another panel.
    private static IEnumerable<DragTreeNode> DraggedNodes(DragDropEventArgs e) =>
        e.Data?.Get<DragTreeNode>() is { } node ? [node] : ItemsOf(e).Select(s => new DragTreeNode(s));

    // True if target is node itself or one of node's descendants - dropping there would splice the subtree into itself.
    private static bool IsSelfOrAncestor(DragTreeNode node, DragTreeNode target)
    {
        for (var n = target; n != null; n = n.Parent)
            if (ReferenceEquals(n, node)) return true;
        return false;
    }

    private static void Add(object arg, ObservableCollection<string> target)
    {
        if (arg is not DragDropEventArgs e) return;

        // Insert BEFORE the reference item (survives the source removal shifting indices in the same list); append if none.
        var at = e.InsertBefore is string before && target.IndexOf(before) is var idx and >= 0 ? idx : target.Count;
        foreach (var item in ItemsOf(e))
        {
            if (target.Contains(item)) continue;
            target.Insert(at > target.Count ? target.Count : at, item);
            at++;
        }
    }

    // The SOURCE completes the gesture: on a Move it removes the dragged items from their origin (Copy leaves them). This
    // is who deletes from the old collection - and it works even when the drop landed in another window's view-model.
    [Command]
    private void DragCompleted(object arg)
    {
        if (arg is DragDropEventArgs { Effects: DragDropEffects.Move } && _dragOrigin != null && _dragItems != null)
        {
            foreach (var item in _dragItems) _dragOrigin.Remove(item);
        }
        _dragOrigin = null;
        _dragItems = null;
    }

    // The dragged items as strings: the packaged selection if present, else the single pressed string, else a tree node's
    // Title (so a tree node dragged into a flat panel still drops its label).
    internal static IReadOnlyList<string> ItemsOf(DragDropEventArgs e) =>
        e.Data?.Get<List<string>>() ?? (e.Data?.Get<string>() is { } s ? [s]
            : e.Data?.Get<DragTreeNode>() is { } n ? [n.Title] : []);
}
