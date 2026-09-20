using Adamantium.Graphics.Fonts;
using Adamantium.UI.Controls.Base;
using Adamantium.UI.Controls.Buttons;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls.DrawingBoard;

// WHAT A PERSON DOES TO THE PLANE: what is selected, the steps that can be taken back, groups, frames, lining up,
// deleting, the clipboard and the grips - and the gestures that drive them.
public partial class InfiniteCanvas
{
    /// <summary>What is selected. The canvas holds it rather than the tool, because the frame that shows it is drawn
    /// here and because a tool being swapped must not take the selection with it. A PROPERTY, so an inspector can
    /// follow it: each change publishes a fresh snapshot, since a list edited in place tells a binding nothing.
    /// </summary>
    public static readonly AdamantiumProperty SelectionProperty = AdamantiumProperty.Register(nameof(Selection),
        typeof(IReadOnlyList<ICanvasItem>), typeof(InfiniteCanvas),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnSelectionSet));

    /// <summary>Whether a small plate follows the pointer showing where it is on the plane, while the grid is pulling.
    /// An option and not a rule: a readout is what you want while placing something to a coordinate and clutter the
    /// rest of the time.</summary>
    public static readonly AdamantiumProperty ShowsPointerReadoutProperty = AdamantiumProperty.Register(
        nameof(ShowsPointerReadout), typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(true, PropertyMetadataOptions.AffectsRender));

    /// <summary>How big the readout's digits are, in screen pixels.</summary>
    public static readonly AdamantiumProperty ReadoutSizeProperty = AdamantiumProperty.Register(nameof(ReadoutSize),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(12.0, PropertyMetadataOptions.AffectsRender));

    public Boolean ShowsPointerReadout
    {
        get => GetValue<Boolean>(ShowsPointerReadoutProperty);
        set => SetValue(ShowsPointerReadoutProperty, value);
    }

    public Double ReadoutSize
    {
        get => GetValue<Double>(ReadoutSizeProperty);
        set => SetValue(ReadoutSizeProperty, value);
    }

    /// <summary>Whether anything is selected - the one question an inspector asks to know which of its two faces to
    /// show, and a property so it can be asked by a binding.</summary>
    public static readonly AdamantiumProperty HasSelectionProperty = AdamantiumProperty.Register(nameof(HasSelection),
        typeof(Boolean), typeof(InfiniteCanvas),
        new PropertyMetadata(false, PropertyMetadataOptions.BindsTwoWayByDefault));

    public IReadOnlyList<ICanvasItem> Selection => _selection;

    public Boolean HasSelection
    {
        get => GetValue<Boolean>(HasSelectionProperty);
        private set => SetValue(HasSelectionProperty, value);
    }

    /// <summary>Raised when what is selected changes.</summary>
    public event EventHandler SelectionChanged;

    /// <summary>The box round everything selected, in WORLD units - what the manipulation frame is drawn on and what a
    /// resize scales. Nothing when nothing is selected.</summary>
    public Rect? SelectionBounds
    {
        get
        {
            if (_selection.Count == 0) return null;

            // ROUND WHAT HAS A PLACE. A band takes the wires along with the nodes, and a wire's box is its whole bend
            // from one node to the other - a frame including those stood far outside everything a person can see they
            // picked. Nothing is lost: a wire is always between two of the things in the box.
            if (Placed() is { Count: > 0 } placed) return Box(placed);

            return Box(new List<ICanvasItem>(_selection));
        }
    }

    public bool IsSelected(ICanvasItem item) => item != null && _selection.Contains(item);

    /// <summary>Selects one thing, replacing what was selected unless <paramref name="extend"/> says to add to it.
    /// </summary>
    public void Select(ICanvasItem item, bool extend)
    {
        if (item == null) return;

        if (!extend) _selection.Clear();
        if (!_selection.Contains(item)) _selection.Add(item);

        Selected();
    }

    public void SelectMany(IEnumerable<ICanvasItem> items, bool extend)
    {
        if (!extend) _selection.Clear();

        foreach (var item in items)
        {
            if (item != null && !_selection.Contains(item)) _selection.Add(item);
        }

        Selected();
    }

    public void Deselect(ICanvasItem item)
    {
        if (item == null || !_selection.Remove(item)) return;

        Selected();
    }

    public void ClearSelection()
    {
        if (_selection.Count == 0) return;

        _selection.Clear();
        Selected();
    }

    /// <summary>Opens an EDIT: everything that happens until it is closed is one step of undo. The canvas opens one
    /// round every gesture on its own - one drag of ten things is one step - and this is here so an application can put
    /// its own changes in a step too. Opened inside an open one, it joins it. Costs nothing with no
    /// <see cref="History"/>.</summary>
    public void BeginEdit(String reason)
    {
        if (History == null || Scene == null) return;

        if (_editDepth++ > 0) return;

        _editReason = reason;
        CanvasStep.Snapshot(Scene, out _editBefore, out _editWasAt);
    }

    /// <summary>Closes the edit and records it, if anything actually changed.</summary>
    public void EndEdit()
    {
        if (History == null || Scene == null || _editDepth == 0) return;
        if (--_editDepth > 0) return;

        CanvasStep.Snapshot(Scene, out var after, out var isAt);
        History.Push(new CanvasStep(_editReason, _editBefore, after, _editWasAt, isAt));

        _editBefore = null;
        _editWasAt = null;
    }

    /// <summary>Puts the last step back. What Ctrl+Z is wired to, and what a button calls.</summary>
    public bool Undo()
    {
        if (History?.Undo(Scene) != true) return false;

        // What was selected may have left the drawing - a frame round something the scene no longer holds is a frame
        // round nothing.
        Reselect();
        Retie();
        return true;
    }

    public bool Redo()
    {
        if (History?.Redo(Scene) != true) return false;

        Reselect();
        Retie();
        return true;
    }

    // A step puts WIRES back and takes them away, and whether a socket is drawn as taken is a fact about the wires - so
    // after a step every socket is asked again, rather than that being a second thing to remember.
    private void Retie()
    {
        if (Scene is not { } scene) return;

        foreach (var item in scene.ItemsIn(Everything))
        {
            if (item is not ElementItem { Element: CanvasNode node }) continue;

            foreach (var pin in node.InputPins) ConnectionItem.Retie(scene, pin);
            foreach (var pin in node.OutputPins) ConnectionItem.Retie(scene, pin);
        }
    }

    // Everything selected that the scene still holds: undo and redo both add and remove things, and the selection must
    // not point at what is gone.
    private void Reselect()
    {
        var alive = new HashSet<ICanvasItem>(Scene.ItemsIn(Everything));
        var kept = new List<ICanvasItem>();

        foreach (var item in _selection)
        {
            if (alive.Contains(item)) kept.Add(item);
        }

        if (kept.Count != _selection.Count) SelectMany(kept, false);

        Repaint();
    }

    /// <summary>Makes ONE thing out of what is selected, and selects it. Null when there is nothing to group.
    /// <para>The children LEAVE the scene: what is drawn stays one flat list, and the group takes the place of the
    /// topmost of them in paint order, so a group does not jump to the front merely by being made.</para></summary>
    public GroupItem GroupSelection()
    {
        if (_selection.Count < 2 || Scene == null) return null;

        BeginEdit("Group");
        try
        {
            return Gather();
        }
        finally
        {
            EndEdit();
        }
    }

    private GroupItem Gather()
    {
        // In PAINT order, taken from the scene rather than from the selection: what was selected first is not what is
        // drawn first, and a group that reordered its own contents would change the drawing by being made.
        var ordered = new List<ICanvasItem>();
        foreach (var item in Scene.ItemsIn(Everything))
        {
            if (_selection.Contains(item)) ordered.Add(item);
        }

        if (ordered.Count < 2) return null;

        var group = new GroupItem(ordered);

        // ONE STEP: the canvas redraws on every edit, so a replace plus a removal apiece let it walk a plane holding
        // the group and its contents at once - a picture met twice became a child of the same layer twice and belonged
        // to neither.
        if (!Scene.Fold(ordered, group)) return null;

        Select(group, false);
        return group;
    }

    /// <summary>Breaks the selected groups open, putting their children back exactly where the group was, and selects
    /// what came out. False when nothing selected was a group.</summary>
    public bool UngroupSelection()
    {
        if (_selection.Count == 0 || Scene == null) return false;

        BeginEdit("Ungroup");
        try
        {
            return Scatter();
        }
        finally
        {
            EndEdit();
        }
    }

    private bool Scatter()
    {
        var freed = new List<ICanvasItem>();
        var kept = new List<ICanvasItem>();

        foreach (var item in _selection.ToArray())
        {
            if (item is GroupItem group && Scene.Replace(group, group.Children)) freed.AddRange(group.Children);
            else kept.Add(item);
        }

        if (freed.Count == 0) return false;

        kept.AddRange(freed);
        SelectMany(kept, false);
        return true;
    }

    // Everything there is. The scene answers by VISIBLE region, and grouping is about what is selected wherever it
    // happens to be - including the part of it that is off screen.
    private static Rect Everything =>
        new(Double.MinValue / 4, Double.MinValue / 4, Double.MaxValue / 2, Double.MaxValue / 2);

    /// <summary>Raised before anything is taken out, so the application can ask first. See
    /// <see cref="CanvasDeleteRequestedEventArgs"/> for what answering means.</summary>
    public event EventHandler<CanvasDeleteRequestedEventArgs> DeleteRequested;

    /// <summary>WHETHER A DELETION IS WORTH A QUESTION. Off by default - with undo in place, being asked about every
    /// object is noise - and the asking is the CANVAS's: an application says whether it wants the question, not how to
    /// ask it. Handling <see cref="DeleteRequested"/> takes the job over entirely.</summary>
    public static readonly AdamantiumProperty ConfirmsDeleteProperty = AdamantiumProperty.Register(
        nameof(ConfirmsDelete), typeof(Boolean), typeof(InfiniteCanvas), new PropertyMetadata(false));

    public Boolean ConfirmsDelete
    {
        get => GetValue<Boolean>(ConfirmsDeleteProperty);
        set => SetValue(ConfirmsDeleteProperty, value);
    }

    /// <summary>What the question says, or null for the canvas's own words. <c>{0}</c> is how many things are
    /// going.</summary>
    public static readonly AdamantiumProperty DeleteQuestionProperty = AdamantiumProperty.Register(
        nameof(DeleteQuestion), typeof(String), typeof(InfiniteCanvas), new PropertyMetadata(null));

    public String DeleteQuestion
    {
        get => GetValue<String>(DeleteQuestionProperty);
        set => SetValue(DeleteQuestionProperty, value);
    }

    /// <summary>...and what the question about emptying the whole plane says. <c>{0}</c> is how many things are on
    /// it.</summary>
    public static readonly AdamantiumProperty ClearQuestionProperty = AdamantiumProperty.Register(
        nameof(ClearQuestion), typeof(String), typeof(InfiniteCanvas), new PropertyMetadata(null));

    public String ClearQuestion
    {
        get => GetValue<String>(ClearQuestionProperty);
        set => SetValue(ClearQuestionProperty, value);
    }

    // The question while it stands. One at a time: a second Delete while it is up is the same question.
    private OverlayWindow _asking;

    /// <summary>Asks to take everything selected out - what Delete and a delete button both go through. A handler of
    /// <see cref="DeleteRequested"/> may take the job over; otherwise the canvas asks when
    /// <see cref="ConfirmsDelete"/> says to, and deletes straight away when it does not.</summary>
    public bool RequestDeleteSelection()
    {
        if (_selection.Count == 0 || Scene == null) return false;

        var asked = new CanvasDeleteRequestedEventArgs(_selection.ToArray());
        DeleteRequested?.Invoke(this, asked);
        if (asked.Handled) return false;

        if (ConfirmsDelete)
        {
            Ask("Delete",
                String.Format(System.Globalization.CultureInfo.CurrentCulture,
                    DeleteQuestion ?? (asked.Items.Count > 1 ? "Remove {0} objects?" : "Remove it?"),
                    asked.Items.Count),
                "Delete", DeleteSelection);

            return false;
        }

        DeleteSelection();
        return true;
    }

    /// <summary>Asks to empty the plane - everything on it, of both modes, in one step of the history. Asks on the same
    /// switch deleting does: emptying a plane is the same action about everything, and a canvas that asked about three
    /// objects and not about three hundred would be lying about which is the dangerous one.</summary>
    public bool RequestClear()
    {
        if (Scene is not { } scene) return false;

        var many = 0;
        foreach (var _ in scene.ItemsIn(Everything)) many++;

        if (many == 0) return false;

        if (ConfirmsDelete)
        {
            Ask("Clear the canvas",
                String.Format(System.Globalization.CultureInfo.CurrentCulture,
                    ClearQuestion ?? "Remove all {0} objects?", many),
                "Clear", Clear);

            return false;
        }

        Clear();
        return true;
    }

    /// <summary>Empties the plane, asking nobody - one step of the history, so it can be taken back.</summary>
    public void Clear()
    {
        if (Scene is not { } scene) return;

        BeginEdit("Clear");
        try
        {
            scene.Reset([]);
            _selection.Clear();
            Selected();
            scene.Touch();
        }
        finally
        {
            EndEdit();
        }
    }

    // THE QUESTION, IN THE ENGINE'S OWN OVERLAY WINDOW - modal, over the very thing it is about, and the same dialog
    // everything else in the application asks with.
    private void Ask(String title, String question, String does, Action done)
    {
        if (_asking != null || WindowAround() is not { } host) return;

        _answer = done;

        var words = new TextBlock
        {
            Text = question,
            TextWrapping = TextWrapping.WrapByWords,
            MarginBottom = 20
        };

        var yes = new Button { Content = does, MinWidth = 96, MarginRight = 8 };
        var no = new Button { Content = "Cancel", MinWidth = 96 };

        yes.Click += (_, _) => Answered(true);
        no.Click += (_, _) => Answered(false);

        var row = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        row.Children.Add(yes);
        row.Children.Add(no);

        var body = new StackPanel { Orientation = Orientation.Vertical, Width = 360, Margin = new Thickness(16) };
        body.Children.Add(words);
        body.Children.Add(row);

        _asking = new OverlayWindow
        {
            Title = title,
            IsModal = true,
            AllowMove = true,
            // A question is answered by ANSWERING it: a click on the dim behind would be a third answer nobody gave.
            CloseOnOverlay = false,
            Content = body
        };

        // Closed by the cross or by Escape is a NO: the destructive thing is what needs saying out loud.
        _asking.Closed += (_, _) => Answered(false);

        host.ShowOverlayWindow(_asking);
    }

    // The window this canvas stands in. Walked up the tree rather than taken from the application: a canvas in a second
    // window must not put its question on the first one.
    private IPopupHost WindowAround()
    {
        for (IUIComponent node = this; node != null; node = node.VisualParent)
        {
            if (node is IPopupHost host) return host;
        }

        return Popup.HostOf(this);
    }

    /// <summary>Whether the canvas is waiting for an answer about something. Nothing goes until it is answered.
    /// </summary>
    public bool IsAsking => _asking != null;

    // What the question is about, so one dialog serves every question the canvas asks.
    private Action _answer;

    private void Answered(bool yes)
    {
        if (_asking is not { } window) return;

        var done = _answer;

        // CLEARED FIRST, because closing the window raises Closed - which comes back here - and the answer must not be
        // given twice, nor a "yes" turned into the "no" that a close means.
        _asking = null;
        _answer = null;

        window.Close();

        if (yes) done?.Invoke();
    }

    /// <summary>How far a pasted or duplicated copy is put from the original, in WORLD units. Far enough to see that
    /// there are two, near enough that the copy is where you were looking.</summary>
    public static readonly AdamantiumProperty CopyOffsetProperty = AdamantiumProperty.Register(nameof(CopyOffset),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(24.0));

    public Double CopyOffset
    {
        get => GetValue<Double>(CopyOffsetProperty);
        set => SetValue(CopyOffsetProperty, value);
    }

    // The canvas's OWN clipboard and not the system's: what is copied here is a piece of a drawing or of a graph, and
    // there is no agreed way to put one of those where another application could read it. Copies and not the items
    // themselves, so editing the original afterwards does not edit what will be pasted.
    private readonly List<ICanvasItem> _clipboard = new();
    private readonly List<(int From, int FromPin, int To, int ToPin)> _clipboardWires = new();

    /// <summary>Whether anything can be pasted.</summary>
    public Boolean CanPaste => _clipboard.Count > 0;

    /// <summary>Takes a copy of what is selected, with the wires that run BETWEEN the selected nodes. What cannot be
    /// copied is left behind rather than refusing the whole gesture - see <see cref="ICanvasItem.Copy"/>.</summary>
    public void Copy()
    {
        _clipboard.Clear();
        _clipboardWires.Clear();

        if (_selection.Count == 0) return;

        var taken = new List<ICanvasItem>();
        foreach (var item in _selection)
        {
            if (item.Copy() is { } made)
            {
                _clipboard.Add(made);
                taken.Add(item);
            }
        }

        GatherWires(taken);
    }

    /// <summary>Puts what was copied on the plane, a little to one side, and selects it.</summary>
    public void Paste() => Put(_clipboard, _clipboardWires);

    /// <summary>Copies what is selected and puts it down at once, leaving the clipboard alone: duplicating is its own
    /// gesture and must not spend what was copied an hour ago.</summary>
    public void Duplicate()
    {
        if (_selection.Count == 0) return;

        var copies = new List<ICanvasItem>();
        var taken = new List<ICanvasItem>();

        foreach (var item in _selection)
        {
            if (item.Copy() is not { } made) continue;

            copies.Add(made);
            taken.Add(item);
        }

        var wires = new List<(int, int, int, int)>();
        GatherWires(taken, wires);

        Put(copies, wires);
    }

    // The wires that run BETWEEN the things being copied, said by POSITION in that list: a wire whose far end is not
    // being copied is not part of what is being copied, and one recorded by object would point at the originals.
    private void GatherWires(List<ICanvasItem> taken, List<(int, int, int, int)> into = null)
    {
        into ??= _clipboardWires;
        if (Scene == null) return;

        foreach (var item in Scene.ItemsIn(Everything))
        {
            if (item is not ConnectionItem wire) continue;

            var from = taken.IndexOf(wire.FromItem);
            var to = taken.IndexOf(wire.ToItem);
            if (from < 0 || to < 0) continue;

            var fromPin = Index(wire.FromItem, wire.FromPin);
            var toPin = Index(wire.ToItem, wire.ToPin);
            if (fromPin < 0 || toPin < 0) continue;

            into.Add((from, fromPin, to, toPin));
        }
    }

    // Puts a set down, offset, wired the way it was, and selected.
    private void Put(List<ICanvasItem> copies, List<(int From, int FromPin, int To, int ToPin)> wires)
    {
        if (copies.Count == 0 || Scene == null) return;

        BeginEdit("Paste");
        try
        {
            var step = new Vector2(CopyOffset, CopyOffset);
            var made = new List<ICanvasItem>(copies.Count);

            foreach (var item in copies)
            {
                // A copy OF THE COPY: the clipboard holds one set and may be pasted many times, and pasting the same
                // objects twice would put one thing on the plane in two places.
                if (item.Copy() is not { } fresh) continue;

                fresh.Move(step);
                Scene.Add(fresh);
                made.Add(fresh);
            }

            foreach (var (from, fromPin, to, toPin) in wires)
            {
                if (from >= made.Count || to >= made.Count) continue;
                if (made[from] is not ElementItem source || made[to] is not ElementItem target) continue;
                if (source.Element is not CanvasNode left || target.Element is not CanvasNode right) continue;
                if (fromPin >= left.OutputPins.Count || toPin >= right.InputPins.Count) continue;

                var wire = new ConnectionItem(source, left.OutputPins[fromPin], target, right.InputPins[toPin]);
                Scene.Add(wire);

                left.OutputPins[fromPin].IsConnected = true;
                right.InputPins[toPin].IsConnected = true;
            }

            SelectMany(made, false);
            Scene.Touch();
        }
        finally
        {
            EndEdit();
        }
    }

    private static int Index(ElementItem item, CanvasNodePin pin)
    {
        if (item?.Element is not CanvasNode node || pin == null) return -1;

        var list = pin.IsInput ? node.InputPins : node.OutputPins;
        for (var i = 0; i < list.Count; i++)
        {
            if (ReferenceEquals(list[i], pin)) return i;
        }

        return -1;
    }

    /// <summary>How much room a new frame leaves round what it is drawn about, in WORLD units - and the strip at the
    /// top is that much again, so the title has somewhere to sit that is not over a node.</summary>
    public static readonly AdamantiumProperty FrameMarginProperty = AdamantiumProperty.Register(nameof(FrameMargin),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(20.0));

    public Double FrameMargin
    {
        get => GetValue<Double>(FrameMarginProperty);
        set => SetValue(FrameMarginProperty, value);
    }

    /// <summary>Draws a titled frame round what is selected and selects IT - the comment every graph editor has. Null
    /// when nothing is selected. The frame holds nothing: what is inside it goes on being moved, wired and deleted
    /// without ever consulting it - see <see cref="CanvasFrameItem"/>.</summary>
    public CanvasFrameItem FrameSelection(string title = null)
    {
        if (Scene is not { } scene || SelectionBounds is not { } bounds) return null;

        var room = Math.Max(0, FrameMargin);
        var strip = room * 1.4;

        // A COPY of the accent, not the accent itself: the frame's colour is a thing a person changes in the panel, and
        // written into the theme's own brush that change would repaint every accent in the application.
        var frame = new CanvasFrameItem(
            new Rect(bounds.X - room, bounds.Y - room - strip, bounds.Width + room * 2, bounds.Height + room * 2 + strip),
            title ?? "Comment",
            SelectionBrush?.Copy()) { TitleHeight = strip };

        BeginEdit("Frame");
        try
        {
            // UNDER what it is about, and the scene draws in order - so it goes to the BACK. This is the whole of what
            // keeps it there: a frame left where it was made would be a sheet of colour over the very nodes it is
            // drawn round.
            scene.Add(frame);
            scene.SendToBack(frame);

            Select(frame, false);
            scene.Touch();
        }
        finally
        {
            EndEdit();
        }

        return frame;
    }

    /// <summary>The least room left between two things that lining up would otherwise have put on top of each other,
    /// in WORLD units.</summary>
    public static readonly AdamantiumProperty AlignGapProperty = AdamantiumProperty.Register(nameof(AlignGap),
        typeof(Double), typeof(InfiniteCanvas), new PropertyMetadata(16.0));

    public Double AlignGap
    {
        get => GetValue<Double>(AlignGapProperty);
        set => SetValue(AlignGapProperty, value);
    }

    /// <summary>Lines the selection up on one edge - or through one middle - of the box round all of it, and OPENS THE
    /// LINE OUT so that nothing ends up on top of anything else.
    /// <para>Against the whole selection's box: "align left" has one obvious meaning, and picking a member to align to
    /// is a second question nobody asked. The opening out is what makes it usable on a GRAPH - a pile of nodes says
    /// less than the row did - and what was already clear of its neighbour is not moved at all.</para></summary>
    public bool Align(CanvasAlignment edge)
    {
        // ONLY WHAT HAS A PLACE. A wire is wherever its two sockets are - it cannot be lined up, and the room it covers
        // is not room anything has to be moved out of.
        var placed = Placed();

        if (placed.Count < 2 || Box(placed) is not { } frame) return false;

        // Which way the line RUNS: a left or right edge makes a column, a top or bottom edge makes a row.
        var column = edge is CanvasAlignment.Left or CanvasAlignment.Right or CanvasAlignment.HorizontalCenter;

        // Ordered BEFORE anything moves: afterwards every item shares one coordinate, and an order taken then would be
        // whatever the sort happened to do with the tie.
        var order = placed;
        order.Sort((a, b) =>
        {
            var first = column
                ? a.Bounds.Y.CompareTo(b.Bounds.Y)
                : a.Bounds.X.CompareTo(b.Bounds.X);

            return first != 0
                ? first
                : column ? a.Bounds.X.CompareTo(b.Bounds.X) : a.Bounds.Y.CompareTo(b.Bounds.Y);
        });

        BeginEdit("Align");
        try
        {
            foreach (var item in order)
            {
                var box = item.Bounds;
                var to = edge switch
                {
                    CanvasAlignment.Left => new Vector2(frame.X - box.X, 0),
                    CanvasAlignment.Right => new Vector2(frame.X + frame.Width - (box.X + box.Width), 0),
                    CanvasAlignment.HorizontalCenter =>
                        new Vector2(frame.X + frame.Width / 2 - (box.X + box.Width / 2), 0),
                    CanvasAlignment.Top => new Vector2(0, frame.Y - box.Y),
                    CanvasAlignment.Bottom => new Vector2(0, frame.Y + frame.Height - (box.Y + box.Height)),
                    _ => new Vector2(0, frame.Y + frame.Height / 2 - (box.Y + box.Height / 2))
                };

                if (to.X != 0 || to.Y != 0) item.Move(to);
            }

            Unstack(order, column);

            Scene?.Touch();
            Selected();
        }
        finally
        {
            EndEdit();
        }

        return true;
    }

    // What in the selection has a place of its own - which is everything that arranging the plane is ABOUT. See
    // ICanvasItem.IsPlaced for what the other kind is.
    private List<ICanvasItem> Placed()
    {
        var placed = new List<ICanvasItem>(_selection.Count);

        foreach (var item in _selection)
        {
            if (item.IsPlaced) placed.Add(item);
        }

        return placed;
    }

    // The box round a set of items, in world units.
    private static Rect? Box(List<ICanvasItem> items)
    {
        if (items.Count == 0) return null;

        var box = items[0].Bounds;

        for (var i = 1; i < items.Count; i++)
        {
            var next = items[i].Bounds;
            var left = Math.Min(box.X, next.X);
            var top = Math.Min(box.Y, next.Y);

            box = new Rect(left, top,
                Math.Max(box.X + box.Width, next.X + next.Width) - left,
                Math.Max(box.Y + box.Height, next.Y + next.Height) - top);
        }

        return box;
    }

    // Opens the line out ALONG the axis it was not lined up on, in the order given, so that no two of them overlap.
    // Only what is too close moves, and only far enough.
    private void Unstack(List<ICanvasItem> order, bool column)
    {
        var gap = Math.Max(0, AlignGap);
        var free = Double.NegativeInfinity;

        foreach (var item in order)
        {
            var box = item.Bounds;
            var from = column ? box.Y : box.X;
            var size = column ? box.Height : box.Width;

            if (from < free)
            {
                item.Move(column ? new Vector2(0, free - from) : new Vector2(free - from, 0));
                from = free;
            }

            free = from + size + gap;
        }
    }

    /// <summary>Puts EQUAL GAPS between the selection, along one axis. The two on the outside stay where they are -
    /// they are what the person has already placed - and equal GAPS rather than equal centres, which is what the eye
    /// actually reads when the things are different sizes.</summary>
    public bool Spread(CanvasSpread way)
    {
        var order = Placed();

        if (order.Count < 3) return false;
        order.Sort((a, b) => way == CanvasSpread.Horizontal
            ? a.Bounds.X.CompareTo(b.Bounds.X)
            : a.Bounds.Y.CompareTo(b.Bounds.Y));

        // The room the middle ones have to share: from the end of the first to the start of the last, less what they
        // themselves take up.
        var first = order[0].Bounds;
        var last = order[^1].Bounds;

        var from = way == CanvasSpread.Horizontal ? first.X + first.Width : first.Y + first.Height;
        var to = way == CanvasSpread.Horizontal ? last.X : last.Y;

        double taken = 0;
        for (var i = 1; i < order.Count - 1; i++)
        {
            var box = order[i].Bounds;
            taken += way == CanvasSpread.Horizontal ? box.Width : box.Height;
        }

        var gap = (to - from - taken) / (order.Count - 1);

        BeginEdit("Spread");
        try
        {
            var at = from + gap;
            for (var i = 1; i < order.Count - 1; i++)
            {
                var box = order[i].Bounds;
                var step = way == CanvasSpread.Horizontal
                    ? new Vector2(at - box.X, 0)
                    : new Vector2(0, at - box.Y);

                if (step.X != 0 || step.Y != 0) order[i].Move(step);

                at += (way == CanvasSpread.Horizontal ? box.Width : box.Height) + gap;
            }

            Scene?.Touch();
            Selected();
        }
        finally
        {
            EndEdit();
        }

        return true;
    }

    public void DeleteSelection()
    {
        if (_selection.Count == 0 || Scene == null) return;

        BeginEdit("Delete");
        try
        {
            Remove();
        }
        finally
        {
            EndEdit();
        }
    }

    private void Remove()
    {
        // A COPY, because taking a thing off the plane is heard: the canvas lets go of what has left the scene (see
        // Forget), and that writes to the very list this is walking.
        foreach (var item in new List<ICanvasItem>(_selection))
        {
            // A NODE TAKES ITS WIRES WITH IT: a socket whose node is gone is not somewhere a wire can end, and left
            // behind no gesture could ever reach it.
            if (item is ElementItem { Element: CanvasNode }) Unwire(item as ElementItem);

            // What the scene refuses is inside a GROUP - a group's children leave the scene when it is made. Without
            // this, deleting something reached by entering a group would quietly do nothing at all.
            if (!Scene.Remove(item)) RemoveFromGroups(item);
        }

        _selection.Clear();
        Selected();
        Scene.Touch();
    }

    // Every wire fastened to a node, taken out with it. Gathered into a LIST first: removing from the scene changes
    // what a walk over the scene is walking.
    private void Unwire(ElementItem node)
    {
        if (node == null || Scene == null) return;

        var wires = new List<ConnectionItem>();
        foreach (var item in Scene.ItemsIn(Everything))
        {
            if (item is ConnectionItem wire && wire.Holds(node)) wires.Add(wire);
        }

        foreach (var wire in wires) ConnectionItem.Cut(Scene, wire);
    }

    private void RemoveFromGroups(ICanvasItem item)
    {
        // Taken as a LIST first: emptying a group removes it from the store this is walking.
        var tops = new List<ICanvasItem>(Scene.ItemsIn(Everything));

        foreach (var top in tops)
        {
            if (top is not GroupItem group || !group.Remove(item)) continue;

            // A group with nothing left is not a group; leaving it would put an invisible thing in the paint order that
            // can still be selected by its own empty box.
            if (group.Children.Count == 0) Scene.Remove(group);
            return;
        }
    }

    /// <summary>The turn a single selected item wears, or nothing. A frame round SEVERAL things is never turned: their
    /// turns are not one turn, and a box that pretended otherwise would lie about every one of them.</summary>
    private CanvasTransform? SelectionTurn =>
        _selection.Count == 1 && _selection[0] is ICanvasTransformed { Transform.IsSomething: true } turned
            ? turned.Transform
            : null;

    // The middle of what is selected, in WORLD units - what a turn turns about.
    private Vector2 SelectionMiddle
    {
        get
        {
            var box = SelectionBounds ?? default;
            return new Vector2(box.X + box.Width / 2, box.Y + box.Height / 2);
        }
    }

    // A pointer position in the SHAPE's OWN frame: a grip drag is arithmetic on a box that stands square, so a turned
    // shape is resized by un-turning the pointer rather than by teaching the gesture about angles.
    private Vector2 InShapeSpace(Vector2 world) =>
        SelectionTurn is { } turn ? turn.Undo(world, SelectionMiddle) : world;

    /// <summary>Which of a single selected item's POINTS a screen position is on, or -1. Only one item at a time offers
    /// them: the points of two things at once are not one shape.</summary>
    public int PointHandleAt(Vector2 screen)
    {
        if (_selection.Count != 1 || _selection[0] is not ICanvasPoints shaped) return -1;

        var reach = Math.Max(4, HandleSize) / 2 + 2;
        var points = shaped.Points;

        for (var i = 0; i < points.Count; i++)
        {
            var at = WorldToScreen(points[i]);
            if (Math.Abs(screen.X - at.X) <= reach && Math.Abs(screen.Y - at.Y) <= reach) return i;
        }

        return -1;
    }

    /// <summary>Which grips what is selected offers - the AND of what every selected item offers, because a frame round
    /// several things can only do what all of them can.</summary>
    public CanvasHandles OfferedHandles
    {
        get
        {
            if (_selection.Count == 0) return CanvasHandles.None;

            // What offers NOTHING does not get a vote: a wire has no handles, and counted in it took the frame and the
            // body drag away from every node it was selected with.
            var offered = CanvasHandles.All;
            var said = false;

            foreach (var item in _selection)
            {
                if (item.Handles == CanvasHandles.None) continue;

                offered &= item.Handles;
                said = true;
            }

            return said ? offered : CanvasHandles.None;
        }
    }

    /// <summary>Which grip of the manipulation frame a SCREEN point is on, or <see cref="CanvasHandle.None"/>. Asked in
    /// screen pixels: a grip is something the hand aims at, so how close counts as on it is a distance on the glass at
    /// any zoom.</summary>
    public CanvasHandle HandleAt(Vector2 screen)
    {
        if (SelectionBounds is not { } bounds) return CanvasHandle.None;

        var offered = OfferedHandles;
        if (offered == CanvasHandles.None) return CanvasHandle.None;

        // Asked in the SHAPE's own frame: the grips are drawn on the turned box, so the pointer has to be brought back
        // to where the box stands square before it is compared with anything.
        if (SelectionTurn != null) screen = WorldToScreen(InShapeSpace(ScreenToWorld(screen)));

        // THE SAME frame the grips are drawn on, held off the selection by the same half grip.
        var frame = FrameOf(bounds);
        var topLeft = new Vector2(frame.X, frame.Y);
        var bottomRight = new Vector2(frame.X + frame.Width, frame.Y + frame.Height);
        var reach = Math.Max(4, HandleSize) / 2 + 2;

        var left = Math.Abs(screen.X - topLeft.X) <= reach;
        var right = Math.Abs(screen.X - bottomRight.X) <= reach;
        var top = Math.Abs(screen.Y - topLeft.Y) <= reach;
        var bottom = Math.Abs(screen.Y - bottomRight.Y) <= reach;

        var withinX = screen.X >= topLeft.X - reach && screen.X <= bottomRight.X + reach;
        var withinY = screen.Y >= topLeft.Y - reach && screen.Y <= bottomRight.Y + reach;

        if (!withinX || !withinY) return CanvasHandle.None;

        // Corners before edges: at a corner both are under the pointer, and the corner is the one that was aimed at.
        if (offered.HasFlag(CanvasHandles.Corners))
        {
            if (left && top) return CanvasHandle.TopLeft;
            if (right && top) return CanvasHandle.TopRight;
            if (left && bottom) return CanvasHandle.BottomLeft;
            if (right && bottom) return CanvasHandle.BottomRight;
        }

        if (!offered.HasFlag(CanvasHandles.Sides)) return CanvasHandle.None;

        var midX = Math.Abs(screen.X - (topLeft.X + bottomRight.X) / 2) <= reach;
        var midY = Math.Abs(screen.Y - (topLeft.Y + bottomRight.Y) / 2) <= reach;

        if (left && midY) return CanvasHandle.Left;
        if (right && midY) return CanvasHandle.Right;
        if (top && midX) return CanvasHandle.Top;
        if (bottom && midX) return CanvasHandle.Bottom;

        return CanvasHandle.None;
    }

    // Set from OUTSIDE - by an application restoring what was selected last time, or by a list beside the canvas. What
    // arrives is adopted; what the canvas published itself is recognised and ignored, or the two would push each other
    // round in circles.
    private static void OnSelectionSet(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not InfiniteCanvas canvas || canvas._publishing) return;

        var wanted = e.NewValue as IReadOnlyList<ICanvasItem>;
        canvas._selection.Clear();

        if (wanted != null)
        {
            foreach (var item in wanted)
            {
                if (item != null && !canvas._selection.Contains(item)) canvas._selection.Add(item);
            }
        }

        canvas.HasSelection = canvas._selection.Count > 0;
        canvas._chromeLayer?.SyncSelection();
        canvas.SelectionChanged?.Invoke(canvas, EventArgs.Empty);
        canvas.Repaint();
    }

    private void OnWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ZoomWithWheel || e.Handled) return;

        // A wheel turned over a panel or over a control standing on the drawing belongs to THAT, even when it did
        // nothing with it: a scroller at its end leaves the wheel unhandled so a page carries on scrolling, and here
        // the thing underneath is the camera.
        if (FromGlass(e.OriginalSource)) return;

        // ZoomStep is one standard notch (120) raised to how far the wheel turned, so a hi-res wheel firing many
        // fractional events zooms the same total as a standard one.
        ZoomAt(e.GetPosition(this), Math.Pow(ZoomStep, e.Delta / 120.0));
        e.Handled = true;
    }

    // WHAT A PRESS COSTS, when somebody is asking: written to the file named by ADAM_CANVAS_PRESSLOG, because a plate
    // over the drawing is in the way of the very thing being measured. Off unless the variable is set.
    private static readonly string PressLog = Environment.GetEnvironmentVariable("ADAM_CANVAS_PRESSLOG");

    private readonly System.Diagnostics.Stopwatch _sinceLastPress = new();
    private long _measuresAtLastPress;
    private long _templatesAtLastPress;
    private long _madeAtLastPress;
    private long _rebuildsAtLastPress;
    private long _refreshesAtLastPress;

    // Counting costs an interlocked increment per invalidation - nothing next to what it finds, and too much to leave
    // on for a run nobody is measuring.
    static InfiniteCanvas()
    {
        if (PressLog == null) return;

        Core.Diagnostics.LayoutTrace.Counting = true;

        // ...and WHO CALLED, when asked for. It walks the stack PER EVENT, and this application raises thousands a
        // second: switched on, it turned a running stand into one that took fifty-five seconds between two clicks.
        Core.Diagnostics.LayoutTrace.CountCallers =
            Environment.GetEnvironmentVariable("ADAM_CANVAS_PRESSCALLERS") == "1";
    }

    private void OnPointerDown(object sender, MouseButtonEventArgs e)
    {
        if (PressLog == null)
        {
            PointerDown(sender, e);
            return;
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        var wasMeasures = MeasurableUIComponent.TotalMeasureCores;

        // What happened BETWEEN this press and the last one, which is where the cost of a press actually lands: the
        // handler returns at once and leaves a layout pass and a re-record behind it.
        var since = wasMeasures - _measuresAtLastPress;
        var gap = _sinceLastPress.IsRunning ? _sinceLastPress.Elapsed.TotalMilliseconds : 0;

        PointerDown(sender, e);

        clock.Stop();

        _measuresAtLastPress = MeasurableUIComponent.TotalMeasureCores;
        _sinceLastPress.Restart();

        try
        {
            // A count of measures says the application is busy; it does not say what with, and the difference between
            // those two is the whole job. The counters are keyed by the type and the property that invalidated.
            var busiest = Core.Diagnostics.LayoutTrace.DumpCounts();
            Core.Diagnostics.LayoutTrace.ResetCounts();

            // The AGGREGATES first, then the same list with them taken out: a type says where the work landed, only the
            // property says what caused it, and the aggregates are by definition the biggest numbers.
            var lines = busiest.Split('\n');
            var top = new System.Text.StringBuilder();
            var named = new System.Text.StringBuilder();
            var shown = 0;
            var namedShown = 0;

            for (var i = 0; i < lines.Length; i++)
            {
                if (lines[i].Length == 0) continue;

                var aggregate = lines[i].Contains('*');

                if (aggregate && shown < 6)
                {
                    top.Append("    ").Append(lines[i]).Append("\r\n");
                    shown++;
                }
                else if (!aggregate && namedShown < 10)
                {
                    named.Append("      by ").Append(lines[i].Trim()).Append("\r\n");
                    namedShown++;
                }
            }

            top.Append(named);

            // TEMPLATES BUILT and CONTROLS MADE: the difference between "something was written to" and "everything was
            // built again".
            var builtNow = Base.TemplatedUIComponent.TemplatesBuilt;
            var madeNow = Base.TemplatedUIComponent.TemplatedControlsMade;

            System.IO.File.AppendAllText(PressLog,
                $"press {clock.Elapsed.TotalMilliseconds:F3} ms, measures in it {MeasurableUIComponent.TotalMeasureCores - wasMeasures}" +
                $" | since the last press: {since} measures over {gap:F0} ms" +
                $", templates built {builtNow - _templatesAtLastPress}, controls made {madeNow - _madeAtLastPress}" +
                $", inspector rebuilds {PropertyGrid.Rebuilds - _rebuildsAtLastPress}" +
                $", refreshes {PropertyGrid.Refreshes - _refreshesAtLastPress}\r\n" + top);

            _templatesAtLastPress = builtNow;
            _madeAtLastPress = madeNow;
            _rebuildsAtLastPress = PropertyGrid.Rebuilds;
            _refreshesAtLastPress = PropertyGrid.Refreshes;
        }
        catch
        {
            // An instrument that throws is worse than one that says nothing.
        }
    }

    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled) return;

        // ONE STEP per gesture: a press is where a change begins and the release is where it ends, so dragging ten
        // things across the plane is one thing to undo. Opened before the tool is asked anything.
        BeginEdit("Gesture");

        // Any press takes the focus, not only one that pans: the keys the canvas answers are useless until it has the
        // focus, and nobody drags a canvas to be allowed to press Home.
        Focus();

        // A double click is the CLICK COUNT on the press, which is how everything else in the engine reads one -
        // MouseDoubleClickEvent is never raised by anything, which is why this gesture used to never fire.
        if (e.ClickCount >= 2)
        {
            // The middle button pans, so double-clicking it is the gesture for "put it back".
            if (e.ChangedButton == MouseButtons.Middle)
            {
                ResetCamera();
                e.Handled = true;
                return;
            }

            // ...and a double click on EMPTY PLANE offers the same list a dropped wire does. Only where there is
            // nothing: over a thing, a double click is about that thing.
            if (e.ChangedButton == MouseButtons.Left && Mode == CanvasMode.Nodes && !_pressIsPlane)
            {
                var spot = ScreenToWorld(e.GetPosition(this));
                var reach = ScreenToWorldLength(4);
                var box = new Rect(spot.X - reach, spot.Y - reach, reach * 2, reach * 2);

                var empty = true;
                foreach (var item in ItemsHere(box))
                {
                    if (!item.HitTest(spot, reach)) continue;

                    empty = false;
                    break;
                }

                if (empty && OfferWire(null, null, spot))
                {
                    e.Handled = true;
                    return;
                }
            }
        }

        // The RIGHT button puts the tool back. Whatever was half done is given up first - a line still being placed
        // belongs to the pen, and the tool taking over has no way to end it.
        if (e.ChangedButton == MouseButtons.Right && DefaultTool is { } resting)
        {
            Tool?.Cancel(this);
            SetCurrentValue(ToolProperty, resting);

            e.Handled = true;
            return;
        }

        var pans = e.ChangedButton == MouseButtons.Middle || (e.ChangedButton == MouseButtons.Left && _spaceHeld);
        if (pans)
        {
            _panning = true;
            _panFrom = e.GetPosition(this);
            _panOffsetFrom = Offset;

            CaptureMouse();
            e.Handled = true;
            return;
        }

        // A press that started INSIDE a control on the plane is that control's - acting on it put a second control
        // wherever the first one was clicked. UNLESS THE LAYER SAID OTHERWISE: a node is dragged by its title strip,
        // and the layer hands that press here through PressFromElement.
        if (!_pressIsPlane && FromGlass(e.OriginalSource)) return;

        var at = e.GetPosition(this);

        // A GRIP of the manipulation frame, before any tool sees the press: the frame is the canvas's, so dragging one
        // is the same answer whatever tool is in hand. Only the GRIPS - a press inside the frame still means what the
        // tool says, or a shape could never be drawn over something already selected.
        if (e.ChangedButton == MouseButtons.Left && _selection.Count > 0)
        {
            // POINT handles first: they sit inside the frame, so asking about the box first would start a move and a
            // curve could never be reshaped without moving it.
            var point = PointHandleAt(at);
            if (point >= 0)
            {
                BeginEdit("Reshape");
                _draggingPoint = point;
                CaptureMouse();

                e.Handled = true;
                return;
            }

            var handle = HandleAt(at);
            if (handle is not (CanvasHandle.None or CanvasHandle.Body))
            {
                _frame.Begin(this, handle, InShapeSpace(ScreenToWorld(at)));
                CaptureMouse();

                e.Handled = true;
                return;
            }
        }

        // Everything else is the TOOL's. The canvas keeps the wheel, the middle button, space and Home - how you look
        // at a drawing - and knows nothing about what the plain left button means.
        var args = Describe(at, e.ChangedButton, e.ClickCount, e.Modifiers);
        Tool?.OnPressed(this, args);
        if (args.Handled) e.Handled = true;
    }

    private void OnPointerMove(object sender, MouseEventArgs e)
    {
        if (_panning)
        {
            // From where the drag STARTED, not from the last move: accumulating deltas drifts, and the camera has
            // nothing to clamp against that would hide it.
            SetCurrentValue(OffsetProperty, _panOffsetFrom + (e.GetPosition(this) - _panFrom));
            e.Handled = true;
            return;
        }

        var pointer = e.GetPosition(this);
        _pointer = pointer;

        // OVER A PANEL the pointer is the panel's: a crosshair over an inspector says a press there would draw, and it
        // would not. The mark and the readout go with it. A drag keeps the capture, so its moves are unaffected.
        if (FromGlass(e.OriginalSource))
        {
            var had = _snap != null || _pointerInside;

            _snap = null;
            _pointerInside = false;
            Cursor = Cursors.Arrow;

            if (had) Repaint();
            return;
        }

        _pointerInside = true;

        // Where the pointer would be PULLED to, worked out whatever the tool is doing - the mark has to appear before
        // the press, or nobody can aim at it.
        var pulled = TrySnap(ScreenToWorld(pointer), out var target) ? target : (Vector2?)null;
        if (pulled != _snap)
        {
            _snap = pulled;
            Repaint();
        }

        // A POINT taken before any tool saw the press is dragged before any tool sees the move, and pulled to the grid
        // like everything else the hand places.
        if (_draggingPoint >= 0 && _selection.Count == 1 && _selection[0] is ICanvasPoints shaped)
        {
            shaped.MovePoint(_draggingPoint, pulled ?? ScreenToWorld(pointer));
            Scene?.Touch();

            e.Handled = true;
            return;
        }

        // A grip taken before any tool saw the press is dragged before any tool sees the move.
        if (_frame.IsActive)
        {
            _frame.MoveTo(this, InShapeSpace(ScreenToWorld(pointer)));
            e.Handled = true;
            return;
        }

        ShowPointer(pointer);

        var args = Describe(pointer, MouseButtons.None, 0, e.Modifiers);
        Tool?.OnMoved(this, args);
        if (args.Handled) e.Handled = true;
    }

    // What the pointer is ABOUT to do. A grip looks the same whichever corner it is, so the cursor is the only thing
    // that says which way it will pull; anywhere else the pointer wears the TOOL.
    private void ShowPointer(Vector2 screen)
    {
        var overGrip = _selection.Count > 0 ? CanvasFrameGesture.CursorFor(HandleAt(screen)) : null;

        Cursor = overGrip ?? OverSelection(screen) ?? Tool?.Cursor ?? Cursors.Arrow;
    }

    /// <summary>Works out what the pointer should look like at a place on screen, and wears it. Public so that what the
    /// pointer PROMISES can be asked without a mouse: it is a promise about what the next press will do.</summary>
    public void ShowPointerAt(Vector2 screen) => ShowPointer(screen);

    // What is under the pointer WITHIN the selection: one of its points, or the thing itself. The only thing that says
    // so on a LINE, which has no box to say it by being drawn.
    private Cursor OverSelection(Vector2 screen)
    {
        // A SOCKET first, selected or not: a wire is pulled out of one with the select tool in hand.
        if (Tool is SelectTool select && select.Wires.Under(this, ScreenToWorld(screen)) != null) return Cursors.Crosshair;

        if (_selection.Count == 0) return null;

        if (PointHandleAt(screen) >= 0) return Cursors.Crosshair;

        // Only what MOVES with a press here: a cursor promising otherwise is the same lie a grip drawn where nothing
        // drags would be.
        if (!OfferedHandles.HasFlag(CanvasHandles.Body)) return null;

        // ...AND ONLY WITH SOMETHING THAT MOVES IT IN HAND. A press inside the frame belongs to the TOOL, so with a pen
        // in hand the body drags nothing - and wearing the move cursor there made drawing over a selected thing look
        // like dragging it.
        if (Tool is not (null or SelectTool)) return null;

        var world = ScreenToWorld(screen);
        var reach = ScreenToWorldLength(Math.Max(4, HandleSize) / 2 + 2);

        foreach (var item in _selection)
        {
            if (item.HitTest(InShapeSpace(world), reach)) return Cursors.SizeAll;
        }

        return null;
    }

    private void OnPointerLeft(object sender, MouseEventArgs e)
    {
        var had = _snap != null || _pointerInside;

        _snap = null;
        _pointerInside = false;

        if (had) Repaint();
    }

    private void OnPointerUp(object sender, MouseButtonEventArgs e)
    {
        // The step the press opened is closed HERE whatever the release turns out to mean. A gesture that changed
        // nothing records nothing.
        try
        {
            if (_draggingPoint >= 0)
            {
                _draggingPoint = -1;
                ReleaseMouseCapture();
                e.Handled = true;
                return;
            }

            if (_panning)
            {
                _panning = false;
                ReleaseMouseCapture();

                // When the gesture ends, not while it runs: a pan raises the camera on every mouse move.
                RememberLayout();
                e.Handled = true;
                return;
            }

            if (_frame.IsActive)
            {
                _frame.End();
                ReleaseMouseCapture();

                e.Handled = true;
                return;
            }

            var args = Describe(e.GetPosition(this), e.ChangedButton, e.ClickCount, e.Modifiers);
            Tool?.OnReleased(this, args);
            if (args.Handled) e.Handled = true;
        }
        finally
        {
            EndEdit();
        }
    }

    // Whether an event came from something the canvas CARRIES rather than from the plane. Their events bubble through
    // here and the ones that matter are not marked handled by whoever answered them, so where the event came from is
    // the only thing to go on.
    private bool FromGlass(object source)
    {
        if (_overlayRoot == null && _layers == null && _chromeLayer == null) return false;

        for (var at = source as IUIComponent; at != null; at = at.VisualParent)
        {
            // A PANE is glass; the layer holding them is not - it covers the entire canvas, so counting it as glass
            // threw away every press meant for the drawing. The pane is met first walking up, which is the whole fix.
            if (at is CanvasPane) return true;
            if (ReferenceEquals(at, _chromeLayer)) return false;

            // A LAYER THAT HOSTS CONTROLS, whichever of them it is: a press on any of them came from something standing
            // on the plane rather than from the plane.
            if (ReferenceEquals(at, _overlayRoot) || at is CanvasElementLayer) return true;
        }

        return false;
    }

    // The pointer as a TOOL sees it: in the world, already pulled to the grid when the grid pulls. Decided here and
    // once, so no tool has to know the setting exists and every tool obeys it the same way.
    private CanvasPointerEventArgs Describe(Vector2 screen, MouseButtons button, int clicks, InputModifiers modifiers)
    {
        var world = ScreenToWorld(screen);
        var snapped = TrySnap(world, out var pulled);

        return new CanvasPointerEventArgs
        {
            Screen = screen,
            World = snapped ? pulled : world,
            Pointer = world,
            IsSnapped = snapped,
            Button = button,
            ClickCount = clicks,
            Modifiers = modifiers
        };
    }

    private void OnKeyPressed(object sender, KeyEventArgs e)
    {
        if (!Ours(e)) return;

        // The TOOL first: the canvas spends Delete, Escape and space on its own things, and all three mean something
        // else to a tool with a caret in a word.
        Tool?.OnKey(this, e);
        if (e.Handled) return;

        // A TOOL IN THE MIDDLE OF SOMETHING OWNS THE KEYBOARD: everything it left unanswered would otherwise go on to
        // the canvas, and typing "vet" picked three tools and threw the word away.
        if (Tool is { IsBusy: true }) return;

        if (e.Key == Key.Space)
        {
            _spaceHeld = true;
            return;
        }

        // GROUPING and UNDO before the tool shortcuts: a tool letter with Ctrl held has never meant the tool anywhere.
        var control = (e.Modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0;
        var shift = (e.Modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0;

        if (control && e.Key == Key.G)
        {
            if (shift) UngroupSelection();
            else GroupSelection();

            e.Handled = true;
            return;
        }

        // Ctrl+Z, and BOTH of the two things the world calls redo: which one a person reaches for depends on what they
        // used last.
        if (control && (e.Key == Key.Z || e.Key == Key.Y))
        {
            if (e.Key == Key.Y || shift) Redo();
            else Undo();

            e.Handled = true;
            return;
        }

        // Copy, paste and duplicate - before the tool shortcuts, so a tool whose key is C is not picked by a copy.
        if (control && e.Key is Key.C or Key.V or Key.D)
        {
            if (e.Key == Key.C) Copy();
            else if (e.Key == Key.V) Paste();
            else Duplicate();

            e.Handled = true;
            return;
        }

        // AFTER the tool and BEFORE the canvas's own keys: a tool typing owns every letter, and picking a tool is the
        // commonest thing a person does here.
        if (PickByShortcut(e.Key))
        {
            e.Handled = true;
            return;
        }

        // WHERE IS MY WORK, on one key - what is selected, or the whole of it when nothing is.
        if (e.Key == Key.F && !control)
        {
            if (!FitSelection()) FitAll();

            e.Handled = true;
            return;
        }

        if (e.Key == Key.Delete)
        {
            if (_selection.Count == 0) return;

            // Through the REQUEST, so a key press meets the same question a button does.
            RequestDeleteSelection();
            e.Handled = true;
            return;
        }

        // Escape gives up whatever is half done and only then lets go of the selection: two presses rather than one, so
        // abandoning a gesture does not also lose what was selected.
        if (e.Key == Key.Escape)
        {
            if (Tool is { IsBusy: true } busy)
            {
                busy.Cancel(this);
                e.Handled = true;
                return;
            }

            if (_selection.Count == 0) return;

            ClearSelection();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Home) return;

        ResetCamera();
        e.Handled = true;
    }

    // The FIRST tool claiming the key wins. Not an error to report: which keys a tool answers to is the application's
    // to arrange, and a control that threw over it would be one that cannot be experimented with.
    private bool PickByShortcut(Key key)
    {
        if (!AreToolShortcutsEnabled || key == Key.None || _tools == null) return false;

        foreach (var tool in _tools)
        {
            if (tool == null || tool.Shortcut != key) continue;

            // ...and it must WORK HERE, or a letter hands a graph a pen - the one thing a mode exists to prevent.
            if (!tool.WorksIn(Mode)) continue;

            if (ReferenceEquals(tool, Tool)) return true;

            SetCurrentValue(ToolProperty, tool);
            return true;
        }

        return false;
    }

    private void OnKeyReleased(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space) _spaceHeld = false;
    }

    // Characters go straight to the tool: the canvas has nothing to type into, and what a key means on the user's own
    // keyboard is a question only the layout can answer - which is what this event already did.
    private void OnTextTyped(object sender, TextInputEventArgs e)
    {
        if (!Ours(e)) return;

        Tool?.OnText(this, e);
    }

    /// <summary>Whether a keyboard event is the CANVAS'S to answer, rather than one belonging to something standing on
    /// it or on the glass over it. Keyboard events bubble, so every letter typed into a field inside a node arrives
    /// here as well - and the canvas answers letters.</summary>
    private bool Ours(RoutedEventArgs e) => ReferenceEquals(e.OriginalSource, this);

    private static Double Shift(Double from, Double to, Double viewport)
    {
        if (from < 0 && to <= viewport) return -from;
        if (to > viewport && from >= 0) return viewport - to;

        return 0;
    }
}
