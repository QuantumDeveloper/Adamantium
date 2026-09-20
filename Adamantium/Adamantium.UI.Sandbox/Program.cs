using Adamantium.UI;
using System;
using Adamantium.UI.Controls.DrawingBoard;

namespace Adamantium.UI.Sandbox;

public class Program
{
    // OLE (the OS drag-drop bridge) requires the UI thread to be a single-threaded apartment - the same requirement
    // WPF/WinForms put on their entry point. Without it the app still runs; only drags to/from other applications are off.
    [STAThread]
    public static void Main(string[] args)
    {
        // A dragged picture should also travel as a file: many targets (Paint 3D, packaged apps) ask for a file list
        // and never look at a bitmap. Off by default in the engine because it writes to disk - an application opts in.
        UI.Input.DragDropOptions.OfferImagesAsFiles = true;

        // TEMP bisection: ADAM_OFF=InkCollector,CanvasGridCollector turns whole batches off by their static Enabled.
        // One build and N cheap runs instead of N builds - which is the only affordable way to find the guilty pass
        // when the failure is INTERMITTENT and each verdict needs half a dozen starts. Reflection because the
        // collectors are internal to Adamantium.UI.
        if (Environment.GetEnvironmentVariable("ADAM_OFF") is { Length: > 0 } off)
        {
            var assembly = typeof(UI.Controls.DrawingBoard.InfiniteCanvas).Assembly.GetName().Name == "Adamantium.UI"
                ? typeof(UI.Controls.DrawingBoard.InfiniteCanvas).Assembly
                : System.Reflection.Assembly.Load("Adamantium.UI");

            foreach (var name in off.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var type = assembly.GetType("Adamantium.UI.Rendering." + name.Trim());
                type?.GetField("Enabled", System.Reflection.BindingFlags.Public |
                                          System.Reflection.BindingFlags.Static)?.SetValue(null, false);
                Console.WriteLine($"ADAM_OFF: {name.Trim()} -> {(type == null ? "NOT FOUND" : "disabled")}");
            }
        }

        var gameApp = new AdamantiumGameApplication();

        // ADAM_START_TAB=<header>: open ON that tab instead of the first one. A measurement of a particular tab starts
        // by getting to it, and getting to it by hand is a click, a build and a settle that are part of neither the
        // before nor the after - so two readings of "the same" tab are taken from two different places. By HEADER, not
        // by index: the strip is reordered whenever a tab is added, and an index would then quietly measure a
        // different tab than the one the last run measured.
        var startTab = Environment.GetEnvironmentVariable("ADAM_START_TAB");
        if (!string.IsNullOrEmpty(startTab)
            || Environment.GetEnvironmentVariable("ADAM_START_MAXIMIZED") == "1"
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ADAM_START_AT")))
        {
            var opener = new System.Threading.Thread(() =>
            {
                // The window exists before its content is templated, and the strip before its items are generated;
                // there is no signal for "the tab control is ready", so this asks until it is - briefly, and then
                // gives up rather than spinning for the life of the application.
                for (var attempt = 0; attempt < 200; attempt++)
                {
                    System.Threading.Thread.Sleep(100);
                    if (Adamantium.UI.UIApplication.Current?.MainWindow is not { Content: Adamantium.UI.Core.IUIComponent root })
                        continue;
                    if (Find<Adamantium.UI.Controls.TabControl>(root) is not { Items.Count: > 0 } tabs) continue;

                    var index = -1;
                    for (var i = 0; i < tabs.Items.Count && !string.IsNullOrEmpty(startTab); i++)
                    {
                        if (tabs.Items[i] is not ViewModels.TabPageViewModel page) continue;
                        if (!string.Equals(page.Header, startTab, StringComparison.OrdinalIgnoreCase)) continue;
                        index = i;
                        break;
                    }

                    var wanted = index;
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                    {
                        if (wanted >= 0) tabs.SelectedIndex = wanted;
                        PlaceForMeasurement();
                    });
                    return;   // the strip is up; a header that matches nothing leaves the selection alone
                }
            })
            { IsBackground = true, Name = "start-tab" };
            opener.Start();
        }

        // ADAM_CANVAS_PRESSLOG=1: write down every press on the plane - what tool was in hand, what was selected, and
        // whether anything else had already taken it. A press is a hand's gesture and this harness touches no mouse, so
        // a report about presses can only be answered by watching real ones.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_PRESSLOG") == "1")
        {
            var watcher = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } wait ? int.Parse(wait) : 8000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas == null)
                {
                    System.IO.File.AppendAllText(beside + ".press.txt", "no canvas on this tab" + Environment.NewLine);
                    return;
                }

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                {
                    // THE TUNNELLING PHASE, before anything has had the press. Watched on the bubbling one, the log
                    // stayed empty - which is itself a fact: by then the press has been marked handled and a plain
                    // handler is not called at all.
                    canvas.PreviewMouseDown += (_, e) =>
                    {
                        System.IO.File.AppendAllText(beside + ".press.txt",
                            $"press: tool={canvas.Tool?.GetType().Name ?? "none"}"
                            + $" button={e.ChangedButton} handled={e.Handled}"
                            + $" source={e.OriginalSource?.GetType().Name ?? "none"}"
                            + $" selected={canvas.Selection.Count}" + Environment.NewLine);
                    };

                    System.IO.File.AppendAllText(beside + ".press.txt", "watching presses" + Environment.NewLine);
                });
            })
            { IsBackground = true, Name = "press-log" };

            watcher.Start();
        }

        // ADAM_CANVAS_PICTURE=<file>: put a texture down, give it that file, draw a stroke over it, and gather the two -
        // the sequence a picture is reported to vanish from. Every step is the one a hand takes, through the tools and
        // through the panel's own write.
        //
        // A STEP PER PASS, each in its own turn of the dispatcher with a wait between: done in ONE turn the whole
        // sequence costs a single frame, and a frame that draws the finished state writes it from nothing. What a hand
        // does is spread over frames, so every step is drawn, and the frame after a change is PATCHED from the one
        // before it - which is the only path on which anything here could go dark.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_PICTURE") is { } picture)
        {
            var hand = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 9000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas?.Scene == null)
                {
                    System.IO.File.AppendAllText(beside + ".canvas.txt", "no canvas for the picture steps" + Environment.NewLine);
                    return;
                }

                Adamantium.UI.Controls.DrawingBoard.ElementItem put = null;

                void Step(Action what)
                {
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(what);
                    System.Threading.Thread.Sleep(900);
                }

                Step(() =>
                {
                    var texture = new Adamantium.UI.Controls.DrawingBoard.TextureTool();

                    canvas.Tool = texture;
                    texture.OnPressed(canvas, At(canvas, 60, 60));
                    texture.OnMoved(canvas, At(canvas, 260, 200));
                    texture.OnReleased(canvas, At(canvas, 260, 200));

                    foreach (var item in canvas.ItemsHere()) put = item as Adamantium.UI.Controls.DrawingBoard.ElementItem ?? put;
                });

                Step(() =>
                {
                    if (put?.Painted is Adamantium.UI.Controls.Image surface && System.IO.File.Exists(picture))
                    {
                        surface.Source = new Adamantium.UI.Core.Media.Imaging.BitmapImage(new Uri(picture));
                    }
                });

                Step(() =>
                {
                    // TWO of them, which is the condition: with one stroke the picture survives being gathered, with
                    // two or more it does not - one edit more leaves the plane standing in its half-state once more.
                    var pen = new Adamantium.UI.Controls.DrawingBoard.PenTool();

                    canvas.Tool = pen;
                    pen.OnPressed(canvas, At(canvas, 90, 90));
                    pen.OnMoved(canvas, At(canvas, 220, 170));
                    pen.OnReleased(canvas, At(canvas, 220, 170));

                    pen.OnPressed(canvas, At(canvas, 90, 170));
                    pen.OnMoved(canvas, At(canvas, 220, 90));
                    pen.OnReleased(canvas, At(canvas, 220, 90));
                });

                Step(() =>
                {
                    var both = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>();

                    foreach (var item in canvas.ItemsHere())
                    {
                        if (ReferenceEquals(item, put) || item is Adamantium.UI.Controls.DrawingBoard.StrokeItem) both.Add(item);
                    }

                    canvas.SelectMany(both, false);
                });

                // WHAT THE OP STREAM WAS DOING ON EITHER SIDE OF THE GESTURE. A picture that stops being drawn while its
                // control is in the tree, visible and the right size - and comes back on any full walk - is a picture
                // whose place in the stream was taken, not a picture that was never recorded. The counters say whether
                // a layer moved in the arena at that moment and how many slots had to be renumbered with it.
                Step(() => System.IO.File.AppendAllText(beside + ".canvas.txt",
                    "--- before gathering: " + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine));

                Step(() =>
                {
                    canvas.GroupSelection();

                    var told = new System.Text.StringBuilder("--- gathered a step at a time: ");

                    void Seen(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.DrawingBoard.CanvasDrawLayer drawn)
                            told.Append($"draw({drawn.Painted.Length}) ");
                        if (node is Adamantium.UI.Controls.DrawingBoard.CanvasElementLayer host)
                            told.Append($"host({host.VisualChildren.Count}) ");

                        foreach (var child in node.VisualChildren) Seen(child);
                    }

                    Seen(canvas);

                    told.Append($"| picture visible={put?.Element?.Visibility} size={put?.Element?.RenderSize}"
                                + $" parent={(put?.Element as Adamantium.UI.Core.IUIComponent)?.VisualParent?.GetType().Name ?? "NONE"}");

                    System.IO.File.AppendAllText(beside + ".canvas.txt", told + Environment.NewLine);
                });

                Step(() => System.IO.File.AppendAllText(beside + ".canvas.txt",
                    "--- after gathering:  " + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine));

                // ADAM_CANVAS_FACE=structure: turn the panel to the page that lists the plane, and open the group that
                // was just made. Both are properties, not gestures - this harness touches no mouse.
                if (Environment.GetEnvironmentVariable("ADAM_CANVAS_FACE") != "structure") return;

                Step(() =>
                {
                    Adamantium.UI.Controls.DrawingBoard.CanvasInspector panel = null;

                    void Look(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.DrawingBoard.CanvasInspector found) panel ??= found;

                        foreach (var child in node.VisualChildren) Look(child);
                    }

                    Look(canvas);

                    if (panel != null) panel.ShowsStructure = true;
                });

                Step(() =>
                {
                    var rows = new System.Collections.Generic.List<Adamantium.UI.Controls.TreeViewItem>();

                    void Look(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.TreeViewItem row) rows.Add(row);

                        foreach (var child in node.VisualChildren) Look(child);
                    }

                    Look(canvas);

                    var said = new System.Text.StringBuilder($"--- the structure shows {rows.Count} rows: ");

                    foreach (var row in rows)
                    {
                        said.Append($"[{(row.Header as Adamantium.UI.Controls.DrawingBoard.ICanvasItem)?.Title ?? "?"}"
                                    + $"{(row.HasItems ? " +" : "")}] ");

                        if (row.HasItems) row.IsExpanded = true;
                    }

                    System.IO.File.AppendAllText(beside + ".canvas.txt", said + Environment.NewLine);
                });
            })
            { IsBackground = true, Name = "picture-steps" };

            hand.Start();
        }

        // ADAM_CANVAS_SEEN=1: what is on the plane against what is actually being drawn - the scene, what the camera
        // calls visible, and what each layer holds.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_SEEN") is "1" or "cycle" or "watch")
        {
            var eyes = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 9000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas?.Scene == null) return;

                // ADAM_CANVAS_SEEN=watch: a line every couple of seconds for as long as the stand is up, so the moment
                // a drawing stops being drawn is in the file whatever the hand did to get there.
                if (Environment.GetEnvironmentVariable("ADAM_CANVAS_SEEN") == "watch")
                {
                    var last = "";

                    while (true)
                    {
                        var line = "";

                        Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                        {
                            var seen = 0;
                            var draws = 0;
                            var hosts = 0;

                            foreach (var _ in canvas.ItemsHere(canvas.VisibleWorld)) seen++;

                            void Layers(Adamantium.UI.Core.IUIComponent node)
                            {
                                if (node is Adamantium.UI.Controls.DrawingBoard.CanvasDrawLayer drawn) draws += drawn.Painted.Length;
                                if (node is Adamantium.UI.Controls.DrawingBoard.CanvasElementLayer host) hosts += host.VisualChildren.Count;

                                foreach (var child in node.VisualChildren) Layers(child);
                            }

                            Layers(canvas);

                            // ...and WHERE THE PANEL'S SECTIONS ARE, laid out: one reported flying to the window's own
                            // corner while the rest stayed put, and the layout says they are all in place - so the
                            // moment it happens, this line says which of the two is lying.
                            var panel = new System.Text.StringBuilder();

                            void Sections(Adamantium.UI.Core.IUIComponent node)
                            {
                                if (node is Adamantium.UI.Controls.PropertySection section
                                    && section.Visibility != Adamantium.UI.Core.Visibility.Collapsed)
                                {
                                    var x = section.Bounds.X;
                                    var y = section.Bounds.Y;

                                    for (var up = ((Adamantium.UI.Core.IUIComponent)section).VisualParent;
                                         up != null; up = up.VisualParent)
                                    {
                                        x += up.Bounds.X;
                                        y += up.Bounds.Y;
                                    }

                                    panel.Append($" [{section.Header}@{x:0},{y:0}]");
                                }

                                foreach (var child in node.VisualChildren) Sections(child);
                            }

                            Sections(canvas);

                            line = $"{DateTime.Now:HH:mm:ss} scale={canvas.Scale:0.####} offset={canvas.Offset}"
                                   + $" visible={seen} draws={draws} hosts={hosts}"
                                   + $" | sections{panel}"
                                   + $" | {Adamantium.UI.Rendering.LayerProbe.Dump()}";
                        });

                        System.Threading.Thread.Sleep(2000);

                        if (line.Length > 0 && line[9..] != last)
                        {
                            last = line[9..];
                            System.IO.File.AppendAllText(beside + ".watch.txt", line + Environment.NewLine);
                        }
                    }
                }

                // ADAM_CANVAS_SEEN=cycle: in to the ceiling and back out, the way the wheel does it - the reported
                // way a drawing stops being drawn.
                if (Environment.GetEnvironmentVariable("ADAM_CANVAS_SEEN") == "cycle")
                {
                    var steps = 0;

                    while (canvas.Scale < canvas.MaxScale - 1e-6 && steps++ < 40)
                    {
                        Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => canvas.ZoomBy(2));
                        System.Threading.Thread.Sleep(200);
                    }

                    System.Threading.Thread.Sleep(2000);
                    System.IO.File.AppendAllText(beside + ".canvas.txt",
                        $"--- at the ceiling: {canvas.Scale:0.###}x after {steps} steps" + Environment.NewLine);

                    while (canvas.Scale > 1.0 + 1e-6 && steps-- > 0)
                    {
                        Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => canvas.ZoomBy(0.5));
                        System.Threading.Thread.Sleep(200);
                    }

                    System.Threading.Thread.Sleep(2500);

                    // Back to where it started, said plainly - the easing overshoots either end and this is about what
                    // is DRAWN at 1x, not about where the wheel leaves the camera.
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                    {
                        canvas.Scale = 1;
                        canvas.CenterOn(Adamantium.Mathematics.Vector2.Zero);
                    });

                    System.Threading.Thread.Sleep(2500);
                }

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                {
                    var said = new System.Text.StringBuilder();
                    var here = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>(canvas.ItemsHere());
                    var seen = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>(
                        canvas.ItemsHere(canvas.VisibleWorld));

                    var all = 0;

                    foreach (var _ in canvas.Scene.ItemsIn(new Adamantium.UI.Core.Rect(
                        -1e9, -1e9, 2e9, 2e9)))
                    {
                        all++;
                    }

                    said.AppendLine($"--- scene={all} here={here.Count} visible={seen.Count}"
                                    + $" camera={canvas.Scale:0.###}x offset={canvas.Offset} world={canvas.VisibleWorld}"
                                    + $" size={canvas.RenderSize}");

                    foreach (var item in here)
                    {
                        said.AppendLine($"    {item.Sort} {item.Title}: bounds={item.Bounds}"
                                        + $" screen={canvas.WorldToScreen(new Adamantium.Mathematics.Vector2(item.Bounds.X, item.Bounds.Y))}"
                                        + $" drawn={seen.Contains(item)}");
                    }

                    void Layers(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.DrawingBoard.CanvasDrawLayer drawn)
                            said.AppendLine($"    layer draws {drawn.Painted.Length}");
                        if (node is Adamantium.UI.Controls.DrawingBoard.CanvasElementLayer host)
                            said.AppendLine($"    layer hosts {host.VisualChildren.Count}");

                        foreach (var child in node.VisualChildren) Layers(child);
                    }

                    Layers(canvas);
                    System.IO.File.AppendAllText(beside + ".canvas.txt", said.ToString());
                });
            })
            { IsBackground = true, Name = "seen-probe" };

            eyes.Start();
        }

        // ADAM_CANVAS_PAINT=1: draw a shape, select it, and recolour it THROUGH THE PANEL'S OWN ROW - the write a hand
        // makes, on the live renderer. Reported: a colour picked for a shape changes nothing, while the same row on a
        // control does.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_PAINT") is "1")
        {
            var brushHand = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 9000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas?.Scene == null)
                {
                    System.IO.File.AppendAllText(beside + ".canvas.txt", "no canvas to paint on" + Environment.NewLine);
                    return;
                }

                void Step(Action what)
                {
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(what);
                    System.Threading.Thread.Sleep(900);
                }

                Adamantium.UI.Controls.DrawingBoard.ShapeItem drawn = null;

                Step(() =>
                {
                    // ADAM_CANVAS_PAINT_ON=drawn: a shape made right now. Otherwise one that was ALREADY on the plane
                    // when the page opened - a different thing entirely, since its brushes came from somewhere else.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_PAINT_ON") == "drawn")
                    {
                        foreach (var known in canvas.Tools)
                        {
                            if (known is Adamantium.UI.Controls.DrawingBoard.ShapeTool shapes) canvas.Tool = shapes;
                        }

                        if (canvas.Tool is Adamantium.UI.Controls.DrawingBoard.ShapeTool tool)
                        {
                            tool.OnPressed(canvas, At(canvas, 120, 120));
                            tool.OnMoved(canvas, At(canvas, 320, 260));
                            tool.OnReleased(canvas, At(canvas, 320, 260));
                        }

                        foreach (var item in canvas.ItemsHere())
                        {
                            drawn = item as Adamantium.UI.Controls.DrawingBoard.ShapeItem ?? drawn;
                        }
                    }
                    else
                    {
                        foreach (var item in canvas.ItemsHere())
                        {
                            drawn ??= item as Adamantium.UI.Controls.DrawingBoard.ShapeItem;
                        }
                    }

                    // ADAM_CANVAS_PAINT_ON=many: several at once, which is what a rubber band leaves behind.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_PAINT_ON") == "many")
                    {
                        var all = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>();

                        foreach (var item in canvas.ItemsHere())
                        {
                            if (item is Adamantium.UI.Controls.DrawingBoard.ShapeItem) all.Add(item);
                        }

                        canvas.SelectMany(all, false);
                        System.IO.File.AppendAllText(beside + ".canvas.txt",
                            $"--- selected {all.Count} shapes at once" + Environment.NewLine);
                        return;
                    }

                    if (drawn != null) canvas.Select(drawn, false);
                });

                Adamantium.UI.Controls.PropertyRow fill = null;
                Adamantium.UI.Controls.PropertyRow stroke = null;

                Step(() =>
                {
                    void Look(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.PropertyRow row
                            && row.Definition is Adamantium.UI.Controls.SolidColorBrushProperty)
                        {
                            if (row.Definition.Header as string == "Fill") fill = row;
                            if (row.Definition.Header as string == "Stroke") stroke = row;
                        }

                        foreach (var child in node.VisualChildren) Look(child);
                    }

                    Look(canvas);

                    System.IO.File.AppendAllText(beside + ".canvas.txt",
                        $"--- shape={drawn?.Shape.ToString() ?? "NONE"}"
                        + $" fillRow={(fill == null ? "missing" : "found")} strokeRow={(stroke == null ? "missing" : "found")}"
                        + $" grid={(canvas.Inspector == null ? "NOT WIRED" : "wired")}"
                        + $" was fill={(drawn?.Fill as Adamantium.UI.Core.Media.SolidColorBrush)?.Color.ToString() ?? "none"}"
                        + $" stroke={(drawn?.Stroke as Adamantium.UI.Core.Media.SolidColorBrush)?.Color.ToString() ?? "none"}"
                        + Environment.NewLine);
                });

                // THROUGH THE SWATCH, which is what a hand touches: the row commits what its editor says, and that is
                // a different half from writing the row itself.
                Step(() =>
                {
                    Adamantium.UI.Controls.ColorPickerButton Swatch(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.ColorPickerButton picker) return picker;

                        foreach (var child in node.VisualChildren)
                        {
                            if (Swatch(child) is { } found) return found;
                        }

                        return null;
                    }

                    var one = fill == null ? null : Swatch(fill);
                    var two = stroke == null ? null : Swatch(stroke);

                    System.IO.File.AppendAllText(beside + ".canvas.txt",
                        $"--- swatches: fill={(one == null ? "missing" : one.SelectedColor.ToString())}"
                        + $" stroke={(two == null ? "missing" : two.SelectedColor.ToString())}" + Environment.NewLine);

                    // ADAM_CANVAS_PAINT_HOW=picker: through the FLYOUT, which is what a hand opens - the picker inside
                    // it answers the swatch through a binding of its own, and that link is the last one untested.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_PAINT_HOW") == "picker" && one != null)
                    {
                        one.IsOpen = true;
                        System.Threading.Thread.Sleep(600);

                        var popup = one.GetTemplateChild("PART_Popup") as Adamantium.UI.Controls.Popup;
                        var picker = popup?.Child as Adamantium.UI.Core.IUIComponent;

                        Adamantium.UI.Controls.ColorPicker Inside(Adamantium.UI.Core.IUIComponent node)
                        {
                            if (node is Adamantium.UI.Controls.ColorPicker found) return found;

                            foreach (var child in node.VisualChildren)
                            {
                                if (Inside(child) is { } deeper) return deeper;
                            }

                            return null;
                        }

                        var wheel = picker as Adamantium.UI.Controls.ColorPicker ?? (picker == null ? null : Inside(picker));

                        System.IO.File.AppendAllText(beside + ".canvas.txt",
                            $"--- flyout: popup={(popup == null ? "missing" : "found")}"
                            + $" picker={(wheel == null ? "missing" : wheel.SelectedColor.ToString())}" + Environment.NewLine);

                        if (wheel != null) wheel.SelectedColor = Adamantium.Mathematics.Colors.Red;

                        return;
                    }

                    if (one != null) one.SelectedColor = Adamantium.Mathematics.Colors.Red;
                    if (two != null) two.SelectedColor = Adamantium.Mathematics.Colors.Lime;
                });

                Step(() =>
                {
                    var said = new System.Text.StringBuilder("--- every shape now: ");

                    foreach (var item in canvas.ItemsHere())
                    {
                        if (item is not Adamantium.UI.Controls.DrawingBoard.ShapeItem one) continue;

                        said.Append($"[{one.Shape} fill={(one.Fill as Adamantium.UI.Core.Media.SolidColorBrush)?.Color.ToString() ?? "none"}] ");
                    }

                    System.IO.File.AppendAllText(beside + ".canvas.txt", said + Environment.NewLine);
                });

                // ADAM_CANVAS_FACE=structure: turn the panel to the page that lists the plane.
                Step(() =>
                {
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_FACE") != "structure") return;

                    void Look(Adamantium.UI.Core.IUIComponent node)
                    {
                        if (node is Adamantium.UI.Controls.DrawingBoard.CanvasInspector panel) panel.ShowsStructure = true;

                        foreach (var child in node.VisualChildren) Look(child);
                    }

                    Look(canvas);
                });

                Step(() => System.IO.File.AppendAllText(beside + ".canvas.txt",
                    $"--- after the write: fill={(drawn?.Fill as Adamantium.UI.Core.Media.SolidColorBrush)?.Color.ToString() ?? "none"}"
                    + $" stroke={(drawn?.Stroke as Adamantium.UI.Core.Media.SolidColorBrush)?.Color.ToString() ?? "none"}"
                    + $" shared={(drawn?.Fill as Adamantium.UI.Core.Media.Brush)?.IsShared}"
                    + $" frozen={(drawn?.Fill as Adamantium.UI.Core.Media.Brush)?.IsFrozen}"
                    + Environment.NewLine));
            })
            { IsBackground = true, Name = "paint-steps" };

            brushHand.Start();
        }

        // ADAM_CANVAS_KEEP=set: move a panel and the camera and let the canvas write itself down - what a hand does
        // before closing the application. ADAM_CANVAS_KEEP=show: say where everything came back, which is the question
        // the whole feature is about and can only be asked on a SECOND run.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_KEEP") is "set" or "show" or "view")
        {
            var keeper = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 9000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas == null) return;

                Adamantium.UI.Controls.DrawingBoard.CanvasPane Pane(string name)
                {
                    foreach (var pane in canvas.Panes)
                    {
                        if (pane.Name == name) return pane;
                    }

                    return null;
                }

                // ADAM_CANVAS_KEEP=view: keep THIS view as the one Home goes back to - what the bookmark does.
                if (Environment.GetEnvironmentVariable("ADAM_CANVAS_KEEP") == "view")
                {
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                    {
                        canvas.Scale = 2;
                        canvas.CenterOn(new Adamantium.Mathematics.Vector2(180, 60));
                        canvas.RememberView();
                    });

                    System.Threading.Thread.Sleep(1200);
                }

                if (Environment.GetEnvironmentVariable("ADAM_CANVAS_KEEP") == "set")
                {
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                    {
                        if (Pane("PART_Inspector") is { } panel)
                        {
                            panel.Placement = Adamantium.UI.Controls.DrawingBoard.CanvasPanePlacement.Free;
                            panel.Anchor = new Adamantium.Mathematics.Vector2(0.15, 0.55);
                            panel.Width = 360;
                        }

                        if (Pane("PART_MiniMap") is { } map) map.IsOpen = false;

                        canvas.Scale = 3;
                        canvas.CenterOn(new Adamantium.Mathematics.Vector2(240, -120));
                        canvas.RememberLayout();
                    });

                    System.Threading.Thread.Sleep(1200);
                }

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                {
                    var panel = Pane("PART_Inspector");
                    var map = Pane("PART_MiniMap");

                    System.IO.File.AppendAllText(beside + ".canvas.txt",
                        $"--- inspector at {panel?.Placement.ToString() ?? "?"} {panel?.Anchor}"
                        + $" width={panel?.Width:0} bounds={panel?.Bounds}"
                        + $" | map open={map?.IsOpen}"
                        + $" | camera {canvas.Scale:0.###}x looking at {canvas.Looking}"
                        + $" | tool={canvas.Tool?.GetType().Name ?? "none"}" + Environment.NewLine);
                });
            })
            { IsBackground = true, Name = "keep-steps" };

            keeper.Start();
        }

        // ADAM_CANVAS_ZOOM=<scale>: put the camera at that scale and write down where every hosted control ends up -
        // its screen corner, the size it was laid out at, and the scale that reached it. What a control looks like at
        // 8x is a question about those three numbers together.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_ZOOM") is { } zoom
            && double.TryParse(zoom, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var asked))
        {
            var lens = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 9000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas == null) return;

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => canvas.Scale = asked);
                System.Threading.Thread.Sleep(1200);

                // ADAM_CANVAS_MAKE=1: and then put one down with a tool, dragging out the same screen box whatever the
                // camera is at - what a hand does, and the thing reported to come out wrong away from 1:1.
                if (Environment.GetEnvironmentVariable("ADAM_CANVAS_MAKE") is "1" or "control" or "click")
                {
                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                    {
                        // ADAM_CANVAS_MAKE=control: one of the application's own controls instead of a texture - a
                        // texture has no size of its own, so it cannot show what a control's floors do to the box.
                        Adamantium.UI.Controls.DrawingBoard.ElementTool tool = null;

                        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_MAKE") is "control" or "click")
                        {
                            foreach (var known in canvas.Tools)
                            {
                                if (known is Adamantium.UI.Controls.DrawingBoard.ElementTool made
                                    && known is not Adamantium.UI.Controls.DrawingBoard.TextureTool) tool = made;
                            }
                        }

                        tool ??= new Adamantium.UI.Controls.DrawingBoard.TextureTool();

                        canvas.Tool = tool;

                        // A CLICK is the other way one is made - no drag at all, and the tool gives it the size it asks
                        // for. ADAM_CANVAS_MAKE=click.
                        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_MAKE") == "click")
                        {
                            tool.OnPressed(canvas, OnScreen(canvas, 150, 150));
                            tool.OnReleased(canvas, OnScreen(canvas, 150, 150));
                            return;
                        }

                        tool.OnPressed(canvas, OnScreen(canvas, 150, 150));
                        tool.OnMoved(canvas, OnScreen(canvas, 350, 290));
                        tool.OnReleased(canvas, OnScreen(canvas, 350, 290));
                    });

                    System.Threading.Thread.Sleep(1200);
                }

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                {
                    var said = new System.Text.StringBuilder($"--- at {canvas.Scale}x, origin {canvas.Offset}:" + Environment.NewLine);

                    foreach (var item in canvas.ItemsHere())
                    {
                        if (item is not Adamantium.UI.Controls.DrawingBoard.ElementItem carried) continue;
                        if (carried.Element is not Adamantium.UI.Core.IUIComponent control) continue;

                        var corner = canvas.WorldToScreen(new Adamantium.Mathematics.Vector2(carried.World.X, carried.World.Y));

                        said.AppendLine($"  {carried.Title}: world={carried.World} wanted={corner}"
                                        + $" at={control.Bounds.Location} size={control.RenderSize}"
                                        + $" scale={(control.RenderTransform?.ScaleX ?? 1):0.###}"
                                        + $" in {control.VisualParent?.GetType().Name ?? "NOWHERE"}#{control.VisualParent?.GetHashCode()}");
                    }

                    System.IO.File.AppendAllText(beside + ".canvas.txt", said.ToString());
                });
            })
            { IsBackground = true, Name = "zoom-probe" };

            lens.Start();
        }

        // ADAM_CANVAS_SVG=<file>: read a drawing onto the plane the way the button does, and say what came in. The
        // import is a file dialog otherwise, which this harness cannot open.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_SVG") is { } drawing)
        {
            var reader = new System.Threading.Thread(() =>
            {
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 9000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas?.Scene == null || !System.IO.File.Exists(drawing)) return;

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                {
                    var made = Adamantium.UI.Controls.DrawingBoard.CanvasSvg.Load(
                        System.IO.File.ReadAllText(drawing), out var skipped);

                    canvas.Scene.Reset(Array.Empty<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>());

                    // ADAM_CANVAS_ONLY=<n>: put ONE of the drawing's paths down instead of all of them. A shape that
                    // is right alone and wrong beside its neighbours is a fact about the batch, not about the shape.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_ONLY") is { } single
                        && int.TryParse(single, out var which))
                    {
                        var flat = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>();

                        void Pick(Adamantium.UI.Controls.DrawingBoard.ICanvasItem item)
                        {
                            if (item is Adamantium.UI.Controls.DrawingBoard.GroupItem group)
                            {
                                foreach (var child in group.Children) Pick(child);
                                return;
                            }

                            flat.Add(item);
                        }

                        foreach (var item in made) Pick(item);

                        made = new[] { flat[Math.Clamp(which, 0, flat.Count - 1)] };
                    }

                    foreach (var item in made) canvas.Scene.Add(item);

                    canvas.SelectMany(made, false);
                    canvas.FitSelection();

                    var said = new System.Text.StringBuilder($"--- read {made.Count} things, skipped {skipped}");

                    void Look(Adamantium.UI.Controls.DrawingBoard.ICanvasItem item)
                    {
                        if (item is Adamantium.UI.Controls.DrawingBoard.GroupItem group)
                        {
                            foreach (var child in group.Children) Look(child);
                            return;
                        }

                        if (item is Adamantium.UI.Controls.DrawingBoard.PathItem path)
                            said.Append($" | path rule={path.FillRule} box={path.World}");
                    }

                    foreach (var item in made) Look(item);

                    System.IO.File.AppendAllText(beside + ".canvas.txt", said + Environment.NewLine);
                });
            })
            { IsBackground = true, Name = "svg-read" };

            reader.Start();
        }

        // ADAM_CANVAS_SELECT=<n>: select the nth thing on the canvas, counting from the back, and write what the canvas
        // holds - in order, with the number each item carries - beside the screenshot.
        // The panel's second page, the one that shows what is SELECTED, cannot be reached from here any other way: a
        // selection is made with a mouse, and this harness does not touch one. So a shot taken without it shows the
        // first page only, and every question about a row on the second page is unanswerable. Its own thread rather
        // than a step of the frame probe, because it is about what is on the plane and not about what a frame costs -
        // and the probe measures for twenty seconds before it would get here.
        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_SELECT") is { } which)
        {
            var picker = new System.Threading.Thread(() =>
            {
                // Long enough for the tab to have built. The canvas fills its scene as the page comes up, and asking a
                // canvas with nothing on it yet which thing is third gets a truthful "there is no third thing".
                System.Threading.Thread.Sleep(
                    Environment.GetEnvironmentVariable("ADAM_CANVAS_SETTLE") is { } warm ? int.Parse(warm) : 8000);

                var beside = Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") ?? "canvas";
                var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                var canvas = win?.Content is Adamantium.UI.Core.IUIComponent root ? FindCanvas(root) : null;

                if (canvas?.Scene == null)
                {
                    System.IO.File.WriteAllText(beside + ".canvas.txt",
                        "no canvas on this tab - is the Infinite canvas tab open?" + Environment.NewLine);
                    return;
                }

                var at = int.TryParse(which, out var parsed) ? parsed : 0;

                Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                {
                    var here = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>();

                    foreach (var item in canvas.ItemsHere()) here.Add(item);

                    if (here.Count == 0) return;

                    var picked = here[Math.Clamp(at, 0, here.Count - 1)];

                    canvas.Select(picked, false);

                    // ADAM_CANVAS_MOVE=<x>,<y>: and shove it that far across the world first. What a question about
                    // paint order needs is two things standing in the SAME place, and a page laid out to be readable
                    // has nothing overlapping anything.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_MOVE")?.Split(',') is { Length: 2 } far
                        && double.TryParse(far[0], out var dx) && double.TryParse(far[1], out var dy))
                    {
                        picked.Move(new Adamantium.Mathematics.Vector2(dx, dy));
                        canvas.Scene.Touch();
                    }

                    // ADAM_CANVAS_PICTURE is taken A STEP AT A TIME, on its own thread - see Picture below.

                    // ADAM_CANVAS_GROUP=1: gather everything on the plane into one group, the way the bar's button
                    // does. What a picture does when it is gathered up cannot be answered any other way from here: the
                    // gesture is a rubber band and two clicks, and this harness touches no mouse.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_GROUP") == "1")
                    {
                        // WHAT IS THERE NOW, not what was there when this thread started: the steps above put things on
                        // the plane, and "select everything" means everything - which is the gesture the picture is
                        // reported to vanish from.
                        var all = new System.Collections.Generic.List<Adamantium.UI.Controls.DrawingBoard.ICanvasItem>();

                        foreach (var item in canvas.ItemsHere()) all.Add(item);

                        canvas.SelectMany(all, false);
                        canvas.GroupSelection();

                        // RIGHT HERE, in the same breath as the gesture. A picture that comes back when anything else
                        // is done to the plane is a picture the LAYERS were never told about - and a reading taken a
                        // moment later is a reading of whatever fixed it.
                        var straight = new System.Text.StringBuilder("--- right after grouping: ");

                        void Layers(Adamantium.UI.Core.IUIComponent node)
                        {
                            if (node is Adamantium.UI.Controls.DrawingBoard.CanvasDrawLayer drawn)
                                straight.Append($"draw({drawn.Painted.Length}) ");
                            if (node is Adamantium.UI.Controls.DrawingBoard.CanvasElementLayer host)
                                straight.Append($"host({host.VisualChildren.Count}) ");

                            foreach (var child in node.VisualChildren) Layers(child);
                        }

                        Layers(canvas);

                        // ...AND WHERE EVERY HOSTED THING STANDS. A control that is not in a layer is not on the plane
                        // at all, and one that is in a layer but not drawn is a different defect entirely - the two
                        // read the same from the outside, so both are written down.
                        foreach (var item in canvas.ItemsHere())
                        {
                            if (item is not Adamantium.UI.Controls.DrawingBoard.GroupItem made) continue;

                            void Held(Adamantium.UI.Controls.DrawingBoard.ICanvasItem thing)
                            {
                                if (thing is Adamantium.UI.Controls.DrawingBoard.GroupItem nested)
                                {
                                    foreach (var child in nested.Children) Held(child);
                                    return;
                                }

                                if (thing is not Adamantium.UI.Controls.DrawingBoard.ElementItem carried) return;

                                var control = carried.Element;

                                straight.Append(
                                    $"| {control?.GetType().Name} parent={(control as Adamantium.UI.Core.IUIComponent)?.VisualParent?.GetType().Name ?? "NONE"} "
                                    + $"visible={control?.Visibility} size={(control as Adamantium.UI.Core.IUIComponent)?.RenderSize} ");
                            }

                            foreach (var child in made.Children) Held(child);
                        }

                        System.IO.File.AppendAllText(beside + ".canvas.txt", straight + Environment.NewLine);
                    }

                    // ADAM_CANVAS_ORDER=<n>: and then send it to that place, the way the panel does - write the number
                    // on the item and tell the canvas an edit happened. This is the whole write path a typed number
                    // takes, so a shot after it shows what a person would see having typed one.
                    if (Environment.GetEnvironmentVariable("ADAM_CANVAS_ORDER") is { } sent && int.TryParse(sent, out var place))
                    {
                        var was = picked.Order;

                        picked.Order = place;
                        canvas.SettleOrder();

                        // ADAM_CANVAS_REDRAW=1: and then ask for the whole subtree to be recorded again. If the picture
                        // is right only WITH this, what moved is the tree and what stayed behind is the record of it -
                        // which says the defect is in the patch and not in the order.
                        if (Environment.GetEnvironmentVariable("ADAM_CANVAS_REDRAW") == "1") canvas.InvalidateRender(true);

                        System.IO.File.AppendAllText(beside + ".canvas.txt",
                            $"asked {was} -> {place}, ended at {picked.Order}, selection holds {canvas.Selection.Count}"
                            + Environment.NewLine);
                    }
                });

                // WHAT THE PLANE HOLDS, twice: once now and once after it has had a moment. A page that fills its scene
                // in stages would otherwise be read halfway through and the numbers blamed on whoever wrote them.
                for (var pass = 0; pass < 2; pass++)
                {
                    System.Threading.Thread.Sleep(pass == 0 ? 400 : 3000);

                    var said = new System.Text.StringBuilder();

                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() =>
                    {
                        said.AppendLine($"--- the whole scene, {(canvas.Scene as Adamantium.UI.Controls.DrawingBoard.CanvasScene)?.Items.Count} of it");

                        // THE STACK ITSELF, in the order it is drawn - which is the half of the question the scene
                        // cannot answer. A scene put in order and a stack that did not follow look identical from here
                        // unless both are written down side by side.
                        var layers = new System.Collections.Generic.List<string>();

                        void Walk(Adamantium.UI.Core.IUIComponent node)
                        {
                            if (node is Adamantium.UI.Controls.DrawingBoard.CanvasDrawLayer drawn)
                                layers.Add($"draw({drawn.Painted.Length})");
                            if (node is Adamantium.UI.Controls.DrawingBoard.CanvasElementLayer host)
                                layers.Add($"host({host.VisualChildren.Count})");

                            foreach (var child in node.VisualChildren) Walk(child);
                        }

                        Walk(canvas);
                        said.AppendLine("--- the stack: " + string.Join(" | ", layers));

                        if (canvas.Scene is Adamantium.UI.Controls.DrawingBoard.CanvasScene scene)
                        {
                            foreach (var item in scene.Items)
                            {
                                var mine = canvas.IsSelected(item) ? " <- selected" : "";

                                said.AppendLine($"{item.Order}\t{item.GetType().Name}{mine}");
                            }
                        }

                        System.IO.File.AppendAllText(beside + ".canvas.txt", said.ToString());
                    });
                }
            })
            { IsBackground = true, Name = "canvas-select" };

            picker.Start();
        }

        if (Environment.GetEnvironmentVariable("ADAM_PROBE_LOG") is { } log)
        {
            var t = new System.Threading.Thread(() =>
            {
                // Let the first tab SETTLE. Six seconds was enough once and is not any more - a heavy tab now builds for
                // longer than that, and a probe that starts inside the build measures the build. Configurable, because
                // "how long does this take to settle" is exactly what changes.
                System.Threading.Thread.Sleep(Environment.GetEnvironmentVariable("ADAM_PROBE_SETTLE") is { } warm ? int.Parse(warm) : 6000);

                // WHO enters or leaves the drawn set while the probe runs. Counted for the whole window, not just the
                // self-driven pan: a spike a hand reproduces has to be attributable the same way one the harness makes is.
                var churn = new System.Collections.Generic.Dictionary<string, int>();
                void Note(string what, Adamantium.UI.Core.IUIComponent c)
                {
                    var key = what + " " + c.GetType().Name + " '" + (c as Adamantium.UI.Controls.Base.UIComponent)?.Name + "' -> "
                              + c.Visibility;
                    lock (churn) churn[key] = churn.TryGetValue(key, out var had) ? had + 1 : 1;
                }

                // TEMP: elements ENTERING and LEAVING the tree, per second. Answers "are containers recreated when the
                // cell size changes" with a number instead of a reading of the recycler: a size change moves rectangles,
                // and if the tree churns at the same time, something is rebuilding what it should be reusing.
                long attachCount = 0, detachCount = 0;
                Adamantium.UI.Core.VisualTreeNotifications.Attached += _ => System.Threading.Interlocked.Increment(ref attachCount);
                Adamantium.UI.Core.VisualTreeNotifications.Detached += _ => System.Threading.Interlocked.Increment(ref detachCount);
                long lastAttach = 0, lastDetach = 0;
                double secRecord = 0, secApply = 0, secPre = 0, secProc = 0;
                double secStructural = 0, secReRender = 0, secGlyph = 0, secBuild = 0, secMerge = 0, secUnit = 0;
                var worstInserts = 0; var worstGroups = 0;
                var worstUnit = "-"; double worstUnitMs = 0;
                double secRecRender = 0; var worstEmptyDraws = 0; var worstReranks = 0;
                double secRecPlan = 0, secRecCopy = 0, secRecSnap = 0; var worstDirty = 0; var worstSkips = 0;
                double secSnapDraws = 0, secSnapDirty = 0; var worstPublished = 0;
                double secLayoutMs = 0;
                double lastLaySty = 0, lastLayMea = 0, lastLayArr = 0; var lastLayIter = 0; var lastLayPass = 0;
                double lastCpUpd = 0, lastCpCache = 0, lastCpBase = 0; var lastCpHits = 0; var lastCpFull = 0;
                double secRecPlace = 0, secRenumber = 0; var worstMarks = 0; long worstScans = 0; var worstRuns = 0; var worstParents = 0;
                long secAllocStart = GC.GetTotalAllocatedBytes(); long secRecBytes = 0, secApplyBytes = 0;
                long lastPreBytes = 0, lastDrawBytes = 0, lastOpsBytes = 0, lastSetupBytes = 0, lastLayoutBytes = 0;
                var lastOpBytes = new long[4]; var lastOpCounts = new int[4];
                long lastApplyB = 0, lastDrawB = 0; var lastApplyN = 0;
                long lastSegSc = 0, lastSegBind = 0, lastSegDraw = 0; var lastSegN = 0;
                long lastTxtStride = 0, lastTxtSetup = 0, lastTxtAD = 0, lastTxtState = 0, lastTxtRes = 0; var lastTxtN = 0;
                long lastXlate = 0, lastFeat = 0, lastWords = 0, lastTail = 0, lastTbOvr = 0, lastFont = 0, lastGuard = 0, lastShape = 0; var lastProcN = 0; var lastTbN = 0; var lastRebuild = 0; var lastGuardN = 0; var lastShapeN = 0;
                string[] opNames = { "scis", "unit", "seg", "flush" };
                long lastUnitsCreated = 0, lastUnitsUpdated = 0, lastUGrow = 0, lastUMism = 0;
                double lastUCreMs = 0, lastUUpdMs = 0;
                var worstPackets = 0;
                var worstKind = "-"; var worstDraws = 0;
                long lastPark = 0;
                var lastGcPause = GC.GetTotalPauseDuration();
                int lastG0 = 0, lastG1 = 0, lastG2 = 0;
                Adamantium.UI.Core.VisualTreeNotifications.Attached += c => Note("attached", c);
                Adamantium.UI.Core.VisualTreeNotifications.Detached += c => Note("detached", c);
                Adamantium.UI.Core.VisualTreeNotifications.VisibilityChanged += c => Note("collapsed-flip", c);
                Adamantium.UI.Core.VisualTreeNotifications.ShownOrHidden += c => Note("hidden-flip", c);
                Adamantium.UI.Core.VisualTreeNotifications.ClipChanged += c => Note("clip", c);

                var startFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var limit = Environment.GetEnvironmentVariable("ADAM_PROBE_SECONDS") is { } sec ? double.Parse(sec) : 20;
                // WHO marks layout dirty, by type + the property that changed. The per-second columns say how MUCH layout
                // there is; only this says whose it is - e.g. whether a label beside the slider, re-measuring as its text
                // changes width, is shoving its neighbours and cascading into the whole window.
                var countLayout = Environment.GetEnvironmentVariable("ADAM_LAYOUT_COUNT") == "1";
                if (countLayout) Adamantium.UI.Core.Diagnostics.LayoutTrace.Counting = true;
                // ADAM_LAYOUT_CALLERS=1: also walk the stack for WHO asked. Slow, and the only thing that answers
                // "this subtree is dirty every frame and no property changed".
                if (Environment.GetEnvironmentVariable("ADAM_LAYOUT_CALLERS") == "1")
                    Adamantium.UI.Core.Diagnostics.LayoutTrace.CountCallers = true;
                var secondIndex = 0; long lastRetained = 0;   // retainMB: forced-collection sample, taken every 8th second

                // TEMP (ADAM_THEME_FLIP=N): swap the theme N times WHILE the measurement window runs - on its own thread,
                // because the other drivers in this file start only after the window closes and a leak has to be watched
                // as it grows, not counted once at the end.
                if (Environment.GetEnvironmentVariable("ADAM_THEME_FLIP") is { } flipCount &&
                    int.TryParse(flipCount, out var flips) && flips > 0)
                {
                    new System.Threading.Thread(() =>
                    {
                        var themes = Adamantium.UI.Core.UIAppContext.Current?.ThemeManager;
                        // Written straight to a FILE, one line per swap. Redirected stdout is block-buffered, so a probe
                        // line printed here is lost unless the process exits cleanly - which is exactly how the previous
                        // run threw away its whole series. And BOTH halves of the memory on the same line: an earlier
                        // hunt read only the managed one and concluded from it alone.
                        var report = Environment.GetEnvironmentVariable("ADAM_THEME_FLIP_LOG") ?? "flips.csv";
                        var self = System.Diagnostics.Process.GetCurrentProcess();

                        void Sample(string tag)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            self.Refresh();
                            var taken = Adamantium.UI.Core.Media.Brush.LinksTaken;
                            var given = Adamantium.UI.Core.Media.Brush.LinksGivenUp;
                            var hooks = Adamantium.UI.Core.Data.BindingExpressionBase.SourceHooks;
                            var unhooks = Adamantium.UI.Core.Data.BindingExpressionBase.SourceUnhooks;
                            var before = GC.GetTotalMemory(true) / 1048576;

                            // TEMP experiment, NOT a fix: the live census says the layout queues list thousands more
                            // departed controls after every swap. Listing is not HOLDING - so empty them and collect. A
                            // number that falls says the queues hold; a number that does not says they only list, and
                            // the holder is elsewhere. (The same experiment on the static focus fields moved nothing,
                            // which is why the root every gcroot path pointed at was NOT the root.)
                            var layoutBefore = Adamantium.UI.Core.LayoutManager.LayoutHeld;
                            Adamantium.UI.Core.LayoutManager.DropAllQueuesForTheExperiment();

                            // ...and BOTH focus fields. The first run of this experiment cleared only FocusManager's and
                            // concluded focus was innocent - while the keyboard device went on holding the very same
                            // element, so the test could not have freed anything either way. Two holders, one question.
                            Adamantium.UI.Core.Input.FocusManager.ResetFocus();
                            Adamantium.UI.Core.Input.KeyboardDevice.CurrentDevice?.SetFocusedElement(null);

                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                            var after = GC.GetTotalMemory(true) / 1048576;

                            var layoutAfter = Adamantium.UI.Core.LayoutManager.LayoutHeld;

                            // WHAT is retained, and where its parent chain ends. The top of a retained element's visual
                            // chain is the subtree root somebody still holds - and the type of THAT is the question no
                            // dump has answered, because gcroot only reports the one path it walked first.
                            if (tag != "base")
                            {
                                var byTop = new System.Collections.Generic.Dictionary<string, int>();
                                var attached = 0; var orphan = 0; var stillHolding = 0; var unstyled = 0;
                                foreach (var node in Adamantium.UI.Core.LayoutManager.LiveManagerRoots())
                                {
                                    if (node.IsAttachedToVisualTree)
                                    {
                                        attached++;
                                        // The check that would have caught the first attempt without anyone looking at
                                        // the screen: an element STILL IN THE TREE whose style never got applied is an
                                        // element wearing the previous theme.
                                        if (node is Adamantium.UI.Core.FundamentalUIComponent { IsStyleApplied: false } bare)
                                        {
                                            unstyled++;
                                            var uk = "UNSTYLED " + bare.GetType().Name + " under " +
                                                     (node.VisualParent?.GetType().Name ?? "-");
                                            byTop[uk] = byTop.TryGetValue(uk, out var seen) ? seen + 1 : 1;
                                        }

                                        // A contradiction in terms, measured AT REST: told it was destroyed, yet on
                                        // screen. Everything hung on OnDiscarded is only as sound as this being zero.
                                        if (node is Adamantium.UI.Core.FundamentalUIComponent { IsDiscarded: true } zombie)
                                        {
                                            var zk = "ZOMBIE " + zombie.GetType().Name;
                                            byTop[zk] = byTop.TryGetValue(zk, out var z) ? z + 1 : 1;
                                        }
                                        continue;
                                    }
                                    orphan++;

                                    // The question the whole hunt turns on: did this orphan ever GIVE UP its render
                                    // attachments? One surviving link into the web keeps the whole web, so a partial
                                    // release is worth nothing - only zero counts.
                                    if (node is Adamantium.UI.Core.AdamantiumComponent { RenderAttachmentsReleased: false })
                                        stillHolding++;

                                    var top = node;
                                    var guard = 0;
                                    while (top.VisualParent != null && ++guard < 512) top = top.VisualParent;
                                    var key = top.GetType().Name + (ReferenceEquals(top, node) ? " (alone)" : "") +
                                              (top.IsAttachedToVisualTree ? " [IN TREE]" : "");
                                    byTop[key] = byTop.TryGetValue(key, out var had) ? had + 1 : 1;
                                }

                                var lines = new System.Collections.Generic.List<string>();
                                foreach (var pair in byTop) lines.Add($"{pair.Value} x {pair.Key}");
                                lines.Sort((a, b) => int.Parse(b.Split(' ')[0]).CompareTo(int.Parse(a.Split(' ')[0])));
                                System.IO.File.AppendAllText(report + ".tops.txt",
                                    $"== {tag}: attached={attached} UNSTYLED-IN-TREE={unstyled} orphan={orphan} stillHoldingBrushes={stillHolding}\n" +
                                    Adamantium.UI.Core.FundamentalUIComponent.SurvivingDiscarded() + "\n" +
                                    DeadOutsideTheCache() + "\n  " +
                                    string.Join("\n  ", lines.GetRange(0, Math.Min(40, lines.Count))) + "\n");
                            }

                            self.Refresh();
                            System.IO.File.AppendAllText(report,
                                $"{tag},{before},{after},{self.PrivateMemorySize64 / 1048576}," +
                                $"{taken - given},{hooks - unhooks}," +
                                $"{layoutBefore.Nodes},{layoutBefore.Managers}," +
                                $"{Adamantium.UI.Controls.ParkedVisuals.Count}," +
                                $"{Adamantium.UI.Controls.Base.TemplatedUIComponent.TemplatesBuilt}," +
                                $"{Adamantium.UI.Controls.Base.TemplatedUIComponent.TemplatedControlsMade}," +
                                $"{Adamantium.UI.Core.FundamentalUIComponent.ThemeApplications}," +
                                $"{Adamantium.UI.Core.Resources.Triggers.TriggerActivatorBase.Made - Adamantium.UI.Core.Resources.Triggers.TriggerActivatorBase.TornDown}," +
                                $"{themes?.CurrentTheme?.Name}\n");
                        }

                        System.Threading.Thread.Sleep(9000);
                        Sample("base");

                        // TEMP (ADAM_SWAP_TO=Name): drive a real theme SWAP from here, which is a different path from
                        // starting ON a theme - a theme whose template cannot finish building stalls the swap's layout
                        // cascade, and the application then sits with the overlay up rather than failing. Sampled after,
                        // so a stalled swap shows as a sample that never arrives.
                        if (Environment.GetEnvironmentVariable("ADAM_SWAP_TO") is { Length: > 0 } wantedTheme
                            && themes?[wantedTheme] is { } target)
                        {
                            // TIMED, not slept-through: a swap that takes seconds and one that never finishes look the
                            // same from a fixed sleep, and that is exactly the difference worth knowing. ThemeChanged
                            // fires when the cascade has SETTLED (every window's layout drained), so it is the honest
                            // end of the swap - SetTheme returning is not.
                            var swapWatch = System.Diagnostics.Stopwatch.StartNew();
                            var settled = new System.Threading.ManualResetEventSlim(false);
                            void OnSettled(object s, Adamantium.UI.Core.Resources.ThemeChangedEventArgs e) => settled.Set();
                            themes.ThemeChanged += OnSettled;

                            System.IO.File.AppendAllText(report, $"-- swapping to {wantedTheme}\n");
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => themes.SetTheme(target));

                            var arrived = settled.Wait(TimeSpan.FromSeconds(60));
                            themes.ThemeChanged -= OnSettled;
                            System.IO.File.AppendAllText(report,
                                arrived
                                    ? $"-- swap to {wantedTheme} SETTLED in {swapWatch.ElapsedMilliseconds} ms\n"
                                    : $"-- swap to {wantedTheme} DID NOT SETTLE within 60 s (still {themes.CurrentTheme?.Name})\n");

                            System.Threading.Thread.Sleep(2000);
                            Sample($"swapped-to-{wantedTheme}");
                        }

                        for (var i = 0; i < flips && themes != null; i++)
                        {
                            // TEMP: reproduce the merged-theme swap under the probe instead of by hand. First step goes
                            // to the merged theme; every step after it toggles its VARIANT, which is the cheap path.
                            if (Environment.GetEnvironmentVariable("ADAM_MERGED_THEME") == "1")
                            {
                                if (themes.CurrentTheme?.Name != "Fluent")
                                {
                                    var merged = themes["Fluent"];
                                    if (merged == null) break;
                                    System.IO.File.AppendAllText(report, $"-- switching to merged theme\n");
                                    themes.SetTheme(merged);
                                }
                                else
                                {
                                    var current = (themes.CurrentTheme as Adamantium.UI.Core.Resources.Theme)?.CurrentVariant;
                                    var wanted = current == Adamantium.UI.Core.Resources.ThemeVariant.Dark
                                        ? Adamantium.UI.Core.Resources.ThemeVariant.Light
                                        : Adamantium.UI.Core.Resources.ThemeVariant.Dark;
                                    System.IO.File.AppendAllText(report, $"-- variant -> {wanted}\n");
                                    Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => themes.SetVariant(wanted));
                                }

                                System.Threading.Thread.Sleep(4000);
                                Sample($"swap{i + 1}");
                                continue;
                            }

                            // Light and dark are VARIANTS of the one Fluent theme now, so this drives a variant switch.
                            // The counters below then read what a variant costs: near zero templates, where the old
                            // two-theme swap rebuilt every one of them.
                            var currentVariant = (themes.CurrentTheme as Adamantium.UI.Core.Resources.Theme)?.CurrentVariant;
                            var wantedVariant = currentVariant == Adamantium.UI.Core.Resources.ThemeVariant.Dark
                                ? Adamantium.UI.Core.Resources.ThemeVariant.Light
                                : Adamantium.UI.Core.Resources.ThemeVariant.Dark;

                            // Count WHAT gets rebuilt, for this swap alone: reset immediately before it, dump once it
                            // has settled. A swap builds 2.5x more templates than the whole tree contains, and only a
                            // per-type breakdown says which control is rebuilt more than once.
                            Adamantium.UI.Controls.Base.TemplatedUIComponent.BuildsByType.Clear();
                            Adamantium.UI.Controls.Base.TemplatedUIComponent.RemovesByType.Clear();

                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => themes.SetVariant(wantedVariant));
                            // Long enough for the swap to settle before it is read, so every step is a settled state
                            // rather than a mid-cascade one.
                            System.Threading.Thread.Sleep(9000);
                            System.IO.File.AppendAllText(report + ".builds.txt",
                                $"\n===== swap{i + 1} builds =====\n" + Adamantium.UI.Controls.Base.TemplatedUIComponent.DumpBuilds() +
                                $"\n----- swap{i + 1} REBUILDS (existing controls re-templated) -----\n" +
                                Adamantium.UI.Controls.Base.TemplatedUIComponent.DumpRemoves() + "\n");
                            Sample($"swap{i + 1}");
                        }
                    }) { IsBackground = true, Name = "theme-flip" }.Start();
                }
                // TEMP (leak hunt): the containers OUTSIDE the render cache that key on a component. Same rule as the one
                // that found _applySnap - count the DEAD keys, never the size.
                static string DeadOutsideTheCache()
                {
                    int g = 0, p = 0, m = 0, n = 0, s = 0;
                    foreach (var scope in Adamantium.UI.Core.RenderDirtyRouter.All())
                    {
                        var d = scope.DeadMarks();
                        g += d.Geometry; p += d.Paint; m += d.Moved; n += d.Node; s += d.Structural;
                    }

                    var anim = Adamantium.UI.Core.Media.Animation.AnimationManager.DeadHolders();
                    var brushes = Adamantium.UI.Core.Media.Brush.DeadOwnerCensus();
                    return $"dead marks: geom={g} paint={p} moved={m} node={n} structural={s}" +
                           $" | anim targets={anim.Targets} dead={anim.Dead} heldByDead={anim.Held}" +
                           $" | brushes live={brushes.LiveBrushes} withDeadOwners={brushes.Brushes} deadOwners={brushes.DeadOwners}";
                }

                var busiestLayout = 0; var busiestLayoutDump = "";
                double layout = 0;
                double sumBegin = 0, sumEnd = 0, sumSubmit = 0, sumPresent = 0, sumFence = 0, sumAcquire = 0, sumSetup = 0;
                double sumPre = 0;
                double sumRecord = 0, sumApply = 0, sumProc = 0, sumDraw = 0, sumProcessors = 0, sumLayout = 0;
                double maxDraw = 0, maxApply = 0, maxRecord = 0;
                long samples = 0;
                var secondStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var secondFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                var worstSecond = long.MaxValue;

                // LOOP responsiveness, measured from OUTSIDE the loop: post a no-op and time how long it takes to come
                // back. Frames-per-second cannot see this - the RENDER thread goes on presenting a replayed stream while
                // the loop is stuck, so a window that answers nothing for ten seconds still reports hundreds of frames a
                // second. That is exactly the report "the colour picker hangs the app", and it is why the first probe
                // found nothing: it was watching the wrong thread.
                var loopDispatcher = Adamantium.UI.Threading.Dispatcher.CurrentDispatcher;
                var loopSentAt = 0L;         // when the outstanding no-op was posted; 0 = none in flight
                double loopWorstMs = 0;      // the longest the loop took to answer during this second
                var loopBindings = Adamantium.UI.Core.Diagnostics.RuntimeStats.BindingUpdatesApplied;
                var loopMeasures = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalMeasureCores;
                var loopArranges = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalArrangeCores;
                while (sw.Elapsed.TotalSeconds < limit)
                {
                    var st = typeof(Adamantium.UI.Core.Diagnostics.RuntimeStats);
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs > layout) layout = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs > secLayoutMs) secLayoutMs = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
                    sumPre += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPreRenderMs;
                    sumBegin += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastBeginDrawMs;
                    sumEnd += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastEndDrawMs;
                    sumSubmit += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastSubmitMs;
                    sumPresent += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPresentMs;
                    sumFence += Adamantium.Graphics.GraphicsDevice.LastFenceWaitMs;
                    sumAcquire += Adamantium.Graphics.GraphicsDevice.LastAcquireMs;
                    sumSetup += Adamantium.Graphics.GraphicsDevice.LastBeginSetupMs;
                    sumLayout += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastLayoutPassMs;
                    sumRecord += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs;
                    sumApply += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs;
                    sumProc += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderProcMs;
                    sumDraw += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs;
                    sumProcessors += Adamantium.UI.Core.Diagnostics.RuntimeStats.LastProcessorsMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs > maxDraw) maxDraw = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs > maxApply) maxApply = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs > maxRecord) maxRecord = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs;
                    samples++;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs > secRecord)
                    {
                        secRecord = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordMs;
                        secRecRender = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordRenderMs;
                        worstEmptyDraws = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordEmptyDraws;
                        worstReranks = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordReranks;
                        secRecPlan = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordPlanMs;
                        secRecPlace = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordPlanOnlyMs;
                        worstMarks = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordStructuralMarks;
                        worstScans = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordPlanScans;
                        worstRuns = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordPlanRuns;
                        worstParents = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordPlanParents;
                        secRenumber = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordRenumberMs;
                        secRecBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordRenderBytes + Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordCopyBytes + Adamantium.UI.Core.Diagnostics.RuntimeStats.LastSnapBytes;
                        secApplyBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyBytes;
                        secRecCopy = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordCopyMs;
                        secRecSnap = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordSnapMs;
                        worstDirty = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordDirty;
                        worstSkips = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRecordClassifySkips;
                        secSnapDraws = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastSnapDrawsMs;
                        secSnapDirty = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastSnapDirtyMs;
                        worstPublished = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastSnapPublished;
                    }
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs > secApply)
                    {
                        secApply = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMs;
                        secStructural = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyStructuralMs;
                        secReRender = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyReRenderMs;
                        worstKind = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyKind;
                        worstDraws = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyDraws;
                        worstPackets = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyPackets;
                        secGlyph = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyGlyphMs;
                        secBuild = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyBuildMs;
                        secUnit = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyUnitMs;
                        worstUnit = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplySlowestUnit;
                        worstUnitMs = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplySlowestUnitMs;
                        secMerge = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyMergeMs;
                        worstInserts = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyInserts;
                        worstGroups = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastApplyGroups;
                    }
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPreRenderMs > secPre) secPre = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastPreRenderMs;
                    if (Adamantium.UI.Core.Diagnostics.RuntimeStats.LastProcessorsMs > secProc) secProc = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastProcessorsMs;

                    // One no-op in flight at a time. While it is outstanding the loop has not answered, so the stall is
                    // reported LIVE rather than only once it ends - a hang that outlasts the run would otherwise vanish.
                    if (loopSentAt == 0)
                    {
                        var sentAt = System.Diagnostics.Stopwatch.GetTimestamp();
                        loopSentAt = sentAt;
                        loopDispatcher?.Post(() =>
                        {
                            var ms = System.Diagnostics.Stopwatch.GetElapsedTime(sentAt).TotalMilliseconds;
                            if (ms > loopWorstMs) loopWorstMs = ms;
                            loopSentAt = 0;
                        });
                    }
                    else
                    {
                        // Read ONCE: the dispatcher clears this field from the other thread, and reading it twice let a
                        // zero land in the elapsed call - which is how a stall came out as 994 560 350 ms.
                        var outstanding = loopSentAt;
                        if (outstanding != 0)
                        {
                            var waiting = System.Diagnostics.Stopwatch.GetElapsedTime(outstanding).TotalMilliseconds;
                            if (waiting > loopWorstMs) loopWorstMs = waiting;
                        }
                    }

                    System.Threading.Thread.Sleep(15);

                    // The WORST SECOND, not the average: a drop that lasts a few seconds disappears into a 40-second
                    // mean, and the per-frame ring only holds the last couple of thousand frames - at a thousand a
                    // second that is the last two. A minimum survives both.
                    if (System.Diagnostics.Stopwatch.GetElapsedTime(secondStart).TotalSeconds >= 1.0)
                    {
                        var thisSecond = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - secondFrames;
                        if (thisSecond < worstSecond) worstSecond = thisSecond;

                        // TEMP: one line PER SECOND. A single "worst second" over a long window cannot say WHEN it
                        // happened, so a drop while building the tab and a drop while dragging a slider read the same -
                        // and three times in a row I explained the wrong moment. The timeline tells them apart: find the
                        // seconds where the fps matches what the plate showed, and read what those seconds were doing.
                        var bindsNow = Adamantium.UI.Core.Diagnostics.RuntimeStats.BindingUpdatesApplied;
                        var measuresNow = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalMeasureCores;
                        var arrangesNow = Adamantium.UI.Controls.Base.MeasurableUIComponent.TotalArrangeCores;

                        // Formatted UNDER THE LOCK the writers take: these two are Dictionaries filled from the record
                        // thread, and enumerating one while it is being written crashed the probe (a NullReferenceException
                        // inside the enumerator, on a tab switch). Snapshot to strings here, then log without holding it.
                        // Finalizers run on their own thread AFTER a collection, so a live count read in the same breath
                        // as the collection is still counting the dead. Give them the queue, then read.
                        if (secondIndex % 8 == 0)
                        {
                            GC.Collect();
                            GC.WaitForPendingFinalizers();
                            GC.Collect();
                        }

                        var themeConsumers = Adamantium.UI.Core.Data.ThemeResourceExpression.RoutedConsumers;
                        var mainWindow = Adamantium.UI.UIApplication.Current?.MainWindow as Adamantium.UI.Core.IUIComponent;
                        var queued = mainWindow != null
                            ? Adamantium.UI.Core.LayoutManager.For(mainWindow).QueuedCounts()
                            : default;
                        string emptyText, recByText, layByText, uByText;
                        lock (Adamantium.UI.Core.Diagnostics.RuntimeStats.HistogramLock)
                        {
                            emptyText = string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.RuntimeStats.EmptyDrawsByType, kv => kv.Value), 4), kv => $"{kv.Key.Name}:{kv.Value}"));
                            recByText = string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.RuntimeStats.RecordMsByType, kv => kv.Value), 6), kv => $"{kv.Key.Name}:{kv.Value:0}"));
                            layByText = string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutMsByType, kv => kv.Value), 6), kv => $"{kv.Key.Name}:{kv.Value:0.0}ms/{Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutCountByType[kv.Key]}"));
                            uByText = string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Take(System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreatedByKind, kv => kv.Value.Ms), 5), kv => $"{kv.Key}:{kv.Value.Count}/{kv.Value.Ms:0.0}ms"));
                        }

                        System.IO.File.AppendAllText(log + ".seconds.txt",
                            $"t={sw.Elapsed.TotalSeconds:00} fps={thisSecond,5} " +
                            $"loopMs={loopWorstMs,7:0} " +
                            $"att={attachCount - lastAttach,6} det={detachCount - lastDetach,6} " +
                            $"binds={bindsNow - loopBindings,8} " +
                            $"measure={measuresNow - loopMeasures,8} arrange={arrangesNow - loopArranges,8} " +
                            $"recMs={secRecord,7:0.0} applyMs={secApply,7:0.0} preMs={secPre,7:0.0} procMs={secProc,7:0.0} " +
                            $"aStruct={secStructural,7:0.0} aRe={secReRender,7:0.0} aGlyph={secGlyph,7:0.0} " +
                            $"aBuild={secBuild,7:0.0} aUnit={secUnit,7:0.0} aMerge={secMerge,7:0.0} ins={worstInserts,7} grp={worstGroups,7} " +
                            $"slowest={worstUnit,-52} slowMs={worstUnitMs,6:0.00} " +
                            $"rRender={secRecRender,7:0.0} rCopy={secRecCopy,7:0.0} rPlan={secRecPlan,7:0.0} rPlace={secRecPlace,7:0.0} rRenum={secRenumber,6:0.0} rMarks={worstMarks,6} rScans={worstScans,9} rRuns={worstRuns,6} rPar={worstParents,5} rSnap={secRecSnap,7:0.0} " +
                            $"sDraws={secSnapDraws,7:0.0} sDirty={secSnapDirty,7:0.0} sPub={worstPublished,7} " +
                            $"rDirty={worstDirty,7} rSkip={worstSkips,7} rEmpty={worstEmptyDraws,7} rRerank={worstReranks,7} " +
                            $"kind={worstKind,-10} pkts={worstPackets,4} draws={worstDraws,6} " +
                            // CREATED versus UPDATED units: building a unit from scratch allocates GPU buffers, updating
                            // one writes into buffers that already exist. Same loop, an order of magnitude apart - which
                            // is exactly the spread aBuild shows per draw (1.5us to 16us), so this is the column that
                            // tells the two apart.
                            $"uCre={Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreated - lastUnitsCreated,7} uGrow={Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreatedGrow - lastUGrow,7} uMism={Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreatedMismatch - lastUMism,7} uCreMs={Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitCreateMs - lastUCreMs,7:0.0} uUpdMs={Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitUpdateMs - lastUUpdMs,7:0.0} " +
                            $"uUpd={Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsUpdated - lastUnitsUpdated,8} " +
                            $"park={Adamantium.UI.Controls.Panels.VirtualizingPanel.ParkCalls - lastPark,7} " +
                            $"gcPause={(GC.GetTotalPauseDuration() - lastGcPause).TotalMilliseconds,7:0.0} " +
                            $"g0={GC.CollectionCount(0) - lastG0,5} g1={GC.CollectionCount(1) - lastG1,5} g2={GC.CollectionCount(2) - lastG2,4} " +
                            $"heapMB={GC.GetTotalMemory(false) / 1048576,6} " +
                            // RETAINED, not merely allocated: GetTotalMemory(true) forces a full collection first, so
                            // this is what the heap still HOLDS. heapMB beside it counts uncollected garbage too, and the
                            // process working set (what Task Manager shows) counts pages the runtime has not returned to
                            // the OS - three different numbers that a "memory grew and never came back" report can mean.
                            // Only a leak moves this one. Forced every 8th second: a gen2 collection is far too expensive
                            // to do per second, and this whole line only exists under ADAM_PROBE_LOG anyway.
                            $"retainMB={(secondIndex++ % 8 == 0 ? lastRetained = GC.GetTotalMemory(true) / 1048576 : lastRetained),6} " +
                            $"consumers={themeConsumers.Entries,6}/{themeConsumers.Alive,6} holders={Adamantium.UI.Core.Media.Animation.AnimationManager.HolderTargets,7} parked={Adamantium.UI.Controls.ParkedVisuals.Count,4} " +
                            // Sampled right after retainMB's forced collection, so finalizers have had their chance and
                            // this counts what is genuinely still reachable rather than what is merely uncollected.
                            $"live={Adamantium.UI.Controls.Base.UIComponent.LiveComponents,8} " +
                            $"queued={queued.Style,5}/{queued.Measure,5}/{queued.Arrange,5}/{queued.NextPass,5}/{queued.Deferred,5} " +
                            $"allocMB={(GC.GetTotalAllocatedBytes() - secAllocStart) / 1048576.0,7:0.0} recKB={secRecBytes / 1024,7} aplKB={secApplyBytes / 1024,7} layMB={(Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutBytes - lastLayoutBytes) / 1048576.0,7:0.0} " +
                            $"preKB={(Adamantium.UI.Core.Diagnostics.RuntimeStats.PreRenderBytes - lastPreBytes) / 1024,8} drawKB={(Adamantium.UI.Core.Diagnostics.RuntimeStats.DrawBytes - lastDrawBytes) / 1024,8} " +
                            $"opsKB={(Adamantium.UI.Core.Diagnostics.RuntimeStats.ExecuteOpsBytes - lastOpsBytes) / 1024,8} setupKB={(Adamantium.UI.Core.Diagnostics.RuntimeStats.DrawSetupBytes - lastSetupBytes) / 1024,8} ops={Adamantium.UI.Core.Diagnostics.RuntimeStats.LastOpsExecuted,6} " +
                            $"txt=[state:{(Adamantium.Graphics.Fonts.FontRenderer.BatchStateBytes - lastTxtState) / 1024}KB res:{(Adamantium.Graphics.Fonts.FontRenderer.BatchResourceBytes - lastTxtRes) / 1024}KB setv:{(Adamantium.Graphics.Fonts.FontRenderer.BatchSetupBytes - lastTxtSetup) / 1024}KB applyDraw:{(Adamantium.Graphics.Fonts.FontRenderer.BatchApplyDrawBytes - lastTxtAD) / 1024}KB n:{Adamantium.Graphics.Fonts.FontRenderer.BatchDrawCount - lastTxtN}] " +
                            $"inSeg=[scis:{(Adamantium.UI.Core.Diagnostics.RuntimeStats.SegScissorBytes - lastSegSc) / 1024}KB bind:{(Adamantium.UI.Core.Diagnostics.RuntimeStats.SegBindBytes - lastSegBind) / 1024}KB draw:{(Adamantium.UI.Core.Diagnostics.RuntimeStats.SegDrawBytes - lastSegDraw) / 1024}KB n:{Adamantium.UI.Core.Diagnostics.RuntimeStats.SegCount - lastSegN}] " +
                            $"inDraw=[apply:{(Adamantium.UI.Core.Diagnostics.RuntimeStats.PassApplyBytes - lastApplyB) / 1024}KB draw:{(Adamantium.UI.Core.Diagnostics.RuntimeStats.DeviceDrawBytes - lastDrawB) / 1024}KB n:{Adamantium.UI.Core.Diagnostics.RuntimeStats.PassApplyCount - lastApplyN}] " +
                            $"byKind=[{string.Join(" ", System.Linq.Enumerable.Select(System.Linq.Enumerable.Range(0, 4), k => $"{opNames[k]}:{(Adamantium.UI.Core.Diagnostics.RuntimeStats.OpBytesByKind[k] - lastOpBytes[k]) / 1024}KB/{Adamantium.UI.Core.Diagnostics.RuntimeStats.OpCountByKind[k] - lastOpCounts[k]}"))}] " +
                            $"uBy=[{uByText}] " +
                            $"tbOvr=[{(Adamantium.UI.Controls.Text.TextBlock.OverrideBytes - lastTbOvr) / 1024}KB n:{Adamantium.UI.Controls.Text.TextBlock.OverrideCount - lastTbN}] " +
                            $"tbIn=[font:{(Adamantium.UI.Controls.Text.TextBlock.FontResolveBytes - lastFont) / 1024}KB/{Adamantium.UI.Controls.Text.TextBlock.LayoutRebuilds - lastRebuild} guard:{(Adamantium.UI.Controls.Text.TextBlock.GuardBytes - lastGuard) / 1024}KB/{Adamantium.UI.Controls.Text.TextBlock.GuardHits - lastGuardN} shape:{(Adamantium.UI.Controls.Text.TextBlock.ShapeBytes - lastShape) / 1024}KB/{Adamantium.UI.Controls.Text.TextBlock.ShapeCalls - lastShapeN}] " +
                            $"txtLay=[xlate:{(Adamantium.Graphics.Fonts.TextLayout.TranslateBytes - lastXlate) / 1024}KB feat:{(Adamantium.Graphics.Fonts.TextLayout.FeatureBytes - lastFeat) / 1024}KB words:{(Adamantium.Graphics.Fonts.TextLayout.WordLoopBytes - lastWords) / 1024}KB tail:{(Adamantium.Graphics.Fonts.TextLayout.TailBytes - lastTail) / 1024}KB n:{Adamantium.Graphics.Fonts.TextLayout.ProcessCount - lastProcN}] " +
                            $"cp=[upd:{(Adamantium.UI.Controls.ContentPresenter.UpdateContentMs - lastCpUpd),6:0.0}ms cache:{(Adamantium.UI.Controls.ContentPresenter.CacheHitMs - lastCpCache),6:0.0}ms/{Adamantium.UI.Controls.ContentPresenter.CacheHits - lastCpHits} base:{(Adamantium.UI.Controls.ContentPresenter.BaseMeasureMs - lastCpBase),6:0.0}ms/{Adamantium.UI.Controls.ContentPresenter.FullMeasures - lastCpFull}] " +
                            $"layBy=[{layByText}] " +
                            $"empty=[{emptyText}] " +
                            $"recBy=[{recByText}] " +
                            $"walks={Adamantium.UI.Core.Diagnostics.FrameTrace.Walks} " +
                            $"drawMs={Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs:0.00}" + Environment.NewLine);

                        // Keep the BUSIEST second, not whichever one the run happened to end on - the interesting second
                        // is rarely the last (see LayoutTrace.TotalCount).
                        if (countLayout)
                        {
                            var thisCount = Adamantium.UI.Core.Diagnostics.LayoutTrace.TotalCount();
                            if (thisCount > busiestLayout)
                            {
                                busiestLayout = thisCount;
                                busiestLayoutDump = Adamantium.UI.Core.Diagnostics.LayoutTrace.DumpCounts();
                            }
                            Adamantium.UI.Core.Diagnostics.LayoutTrace.ResetCounts();
                        }

                        loopWorstMs = 0;
                        lastAttach = attachCount;
                        lastDetach = detachCount;
                        secRecord = secApply = secPre = secProc = secLayoutMs = 0;
                        secStructural = secReRender = secGlyph = secBuild = secMerge = secUnit = 0;
                        worstKind = "-"; worstDraws = 0; worstPackets = 0; worstInserts = 0; worstGroups = 0;
                        worstUnit = "-"; worstUnitMs = 0;
                        secRecRender = 0; worstEmptyDraws = 0; worstReranks = 0;
                        secRecPlan = secRecCopy = secRecSnap = secRecPlace = secRenumber = 0; worstDirty = 0; worstSkips = 0; worstMarks = 0; worstScans = 0; worstRuns = 0; worstParents = 0;
                        lock (Adamantium.UI.Core.Diagnostics.RuntimeStats.HistogramLock)
                        {
                            Adamantium.UI.Core.Diagnostics.RuntimeStats.EmptyDrawsByType.Clear();
                            Adamantium.UI.Core.Diagnostics.RuntimeStats.RecordMsByType.Clear();
                            Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutBytesByType.Clear();
                            Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutMsByType.Clear();
                            Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutCountByType.Clear();
                            Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreatedByKind.Clear();
                        }
                        secSnapDraws = secSnapDirty = 0; worstPublished = 0;
                        lastPark = Adamantium.UI.Controls.Panels.VirtualizingPanel.ParkCalls;
                        lastUnitsCreated = Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreated;
                        lastUGrow = Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreatedGrow;
                        lastUMism = Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsCreatedMismatch;
                        lastUCreMs = Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitCreateMs;
                        lastUUpdMs = Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitUpdateMs;
                        lastUnitsUpdated = Adamantium.UI.Core.Diagnostics.RuntimeStats.UnitsUpdated;
                        secAllocStart = GC.GetTotalAllocatedBytes(); secRecBytes = 0; secApplyBytes = 0;
                        lastLayoutBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutBytes;
                        lastLaySty = Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutStyleMs;
                        lastLayMea = Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutMeasureMs;
                        lastLayArr = Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutArrangeMs;
                        lastLayIter = Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutIterations;
                        lastLayPass = Adamantium.UI.Core.Diagnostics.RuntimeStats.LayoutPasses;
                        lastCpUpd = Adamantium.UI.Controls.ContentPresenter.UpdateContentMs;
                        lastCpCache = Adamantium.UI.Controls.ContentPresenter.CacheHitMs;
                        lastCpBase = Adamantium.UI.Controls.ContentPresenter.BaseMeasureMs;
                        lastCpHits = Adamantium.UI.Controls.ContentPresenter.CacheHits;
                        lastCpFull = Adamantium.UI.Controls.ContentPresenter.FullMeasures;
                        lastPreBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.PreRenderBytes;
                        lastDrawBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.DrawBytes;
                        lastOpsBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.ExecuteOpsBytes;
                        lastSetupBytes = Adamantium.UI.Core.Diagnostics.RuntimeStats.DrawSetupBytes;
                        lastApplyB = Adamantium.UI.Core.Diagnostics.RuntimeStats.PassApplyBytes;
                        lastDrawB = Adamantium.UI.Core.Diagnostics.RuntimeStats.DeviceDrawBytes;
                        lastApplyN = Adamantium.UI.Core.Diagnostics.RuntimeStats.PassApplyCount;
                        lastSegSc = Adamantium.UI.Core.Diagnostics.RuntimeStats.SegScissorBytes;
                        lastSegBind = Adamantium.UI.Core.Diagnostics.RuntimeStats.SegBindBytes;
                        lastSegDraw = Adamantium.UI.Core.Diagnostics.RuntimeStats.SegDrawBytes;
                        lastSegN = Adamantium.UI.Core.Diagnostics.RuntimeStats.SegCount;
                        lastTbOvr = Adamantium.UI.Controls.Text.TextBlock.OverrideBytes;
                        lastFont = Adamantium.UI.Controls.Text.TextBlock.FontResolveBytes;
                        lastGuard = Adamantium.UI.Controls.Text.TextBlock.GuardBytes;
                        lastShape = Adamantium.UI.Controls.Text.TextBlock.ShapeBytes;
                        lastRebuild = Adamantium.UI.Controls.Text.TextBlock.LayoutRebuilds;
                        lastGuardN = Adamantium.UI.Controls.Text.TextBlock.GuardHits;
                        lastShapeN = Adamantium.UI.Controls.Text.TextBlock.ShapeCalls;
                        lastTbN = Adamantium.UI.Controls.Text.TextBlock.OverrideCount;
                        lastXlate = Adamantium.Graphics.Fonts.TextLayout.TranslateBytes;
                        lastFeat = Adamantium.Graphics.Fonts.TextLayout.FeatureBytes;
                        lastWords = Adamantium.Graphics.Fonts.TextLayout.WordLoopBytes;
                        lastTail = Adamantium.Graphics.Fonts.TextLayout.TailBytes;
                        lastProcN = Adamantium.Graphics.Fonts.TextLayout.ProcessCount;
                        lastTxtState = Adamantium.Graphics.Fonts.FontRenderer.BatchStateBytes;
                        lastTxtRes = Adamantium.Graphics.Fonts.FontRenderer.BatchResourceBytes;
                        lastTxtSetup = Adamantium.Graphics.Fonts.FontRenderer.BatchSetupBytes;
                        lastTxtAD = Adamantium.Graphics.Fonts.FontRenderer.BatchApplyDrawBytes;
                        lastTxtN = Adamantium.Graphics.Fonts.FontRenderer.BatchDrawCount;
                        for (var k = 0; k < 4; k++) { lastOpBytes[k] = Adamantium.UI.Core.Diagnostics.RuntimeStats.OpBytesByKind[k]; lastOpCounts[k] = Adamantium.UI.Core.Diagnostics.RuntimeStats.OpCountByKind[k]; }
                        lastGcPause = GC.GetTotalPauseDuration();
                        lastG0 = GC.CollectionCount(0); lastG1 = GC.CollectionCount(1); lastG2 = GC.CollectionCount(2);
                        loopBindings = bindsNow;
                        loopMeasures = measuresNow;
                        loopArranges = arrangesNow;

                        secondFrames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                        secondStart = System.Diagnostics.Stopwatch.GetTimestamp();
                    }
                }
                if (countLayout)
                {
                    Adamantium.UI.Core.Diagnostics.LayoutTrace.Counting = false;
                    System.IO.File.WriteAllText(log + ".layout.txt",
                        $"busiest second: {busiestLayout} layout invalidations" + Environment.NewLine + busiestLayoutDump);
                }
                var inv = samples > 0 ? 1.0 / samples : 0;

                // TEMP (ADAM_VISIT_TABS=1): open every tab in turn, then report which ones the app survived.
                // Shader objects are created LAZILY, at the first draw that needs a pass - so a run that never leaves the
                // home tab never creates the gradient / pattern / fractal / image shaders at all, and "it started" proves
                // nothing about them. Anything that only breaks on those passes needs the tab to be visited.
                if (Environment.GetEnvironmentVariable("ADAM_VISIT_TABS") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var tabs = win?.Content is Adamantium.UI.Core.IUIComponent c ? Find<Adamantium.UI.Controls.TabControl>(c) : null;
                    var visited = new System.Text.StringBuilder();

                    if (tabs != null)
                    {
                        for (var i = 0; i < tabs.Items.Count; i++)
                        {
                            var index = i;
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => tabs.SelectedIndex = index);
                            System.Threading.Thread.Sleep(1500);   // let it build, lay out and DRAW at least once
                            visited.Append(index).Append(':')
                                   .Append(Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames).Append(' ');
                            System.IO.File.WriteAllText(log + ".tabs.txt", visited.ToString());
                        }
                    }

                    System.IO.File.WriteAllText(log + ".tabs.txt",
                        (tabs == null ? "no TabControl found" : "survived tabs " + visited) + Environment.NewLine);
                }


                // TEMP (ADAM_STRIP_SCROLL=1): pan the tab STRIP back and forth while a heavy tab is open - the reported
                // drop from ~700 fps to ~100. Driven from here so the cost can be attributed without a hand on the mouse.
                if (Environment.GetEnvironmentVariable("ADAM_STRIP_SCROLL") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var strip = win?.Content is Adamantium.UI.Core.IUIComponent c ? FindStrip(c) : null;
                    if (strip != null)
                    {
                        var startedAt = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
                        var clock = System.Diagnostics.Stopwatch.StartNew();
                        // Park just inside the far end, so the small oscillation below cannot reach either end whatever
                        // the strip's range turns out to be (it differs run to run with the headers' widths).
                        Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => { strip.Pan(1e6); strip.Pan(-24); });
                        System.Threading.Thread.Sleep(300);
                        Adamantium.UI.Core.Diagnostics.LayoutTrace.Counting = true;

                        // TWO costs, measured apart, because they answer different questions and only one of them is the
                        // strip's own. MOVING: a small oscillation that stays inside the range - nothing crosses the clip,
                        // no chevron flips, so what it costs is the move itself. TRAVELLING: full sweeps that reach both
                        // ends - headers cross the clip and the chevrons appear and disappear, each of which is a
                        // structural change. Mixed into one number they hid each other, and the mix swung run to run with
                        // however long the strip happened to be.
                        var moveFrames = Run(4, () => strip.Pan(_pan = -_pan));
                        var moveFps = moveFrames / 4.0;
                        var movePans = _panned;

                        var travelFrames = Run(4, () =>
                        {
                            if (strip.Pan(_sweep)) return true;
                            _sweep = -_sweep;   // reached an end - turn around
                            return strip.Pan(_sweep);
                        });
                        var travelFps = travelFrames / 4.0;

                        var panned = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - startedAt;
                        Adamantium.UI.Core.Diagnostics.LayoutTrace.Counting = false;
                        System.IO.File.AppendAllText(log + ".strip.txt",
                            $"moving the strip (no clip crossings): {moveFps:0} fps, {movePans} pans" + Environment.NewLine
                            + $"travelling end to end: {travelFps:0} fps, {_panned - movePans} pans" + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + Environment.NewLine
                            + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.LayoutTrace.DumpCounts() + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_LIST_SCROLL=1): scroll the heavy tab's own list, which is the workload the per-layer arena
                // (§5a phase 3) was argued from - slot renumbering, segment cuts, layers relocated out of their room.
                // Panning the tab strip barely touches any of that, so it cannot answer whether that rewrite is worth it.
                if (Environment.GetEnvironmentVariable("ADAM_LIST_SCROLL") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var viewer = win?.Content is Adamantium.UI.Core.IUIComponent c ? Find<Adamantium.UI.Controls.ScrollViewer>(c) : null;
                    if (viewer != null)
                    {
                        Adamantium.UI.Rendering.LayerProbe.Reset();
                        var down = 40.0;
                        var listFrames = Run(8, () =>
                        {
                            var at = viewer.ScrollOffset;
                            if (at.Y + down < 0) down = -down;
                            viewer.SetScrollOffset(new Adamantium.Mathematics.Vector2(at.X, at.Y + down));
                            if (viewer.ScrollOffset.Y == at.Y) down = -down;   // hit an end - turn around
                            return true;
                        });
                        System.IO.File.AppendAllText(log + ".list.txt",
                            $"scrolling the list: {listFrames / 8.0:0} fps" + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + Environment.NewLine
                            + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_CLOSE_FLIP=1): show and hide a tab's close button, which is what HOVERING one does - the
                // reported "the plate stops updating and the frame gets worse when the pointer rests on a close button".
                // Driven from here so it can be attributed without a hand on the mouse.
                if (Environment.GetEnvironmentVariable("ADAM_CLOSE_FLIP") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var button = win?.Content is Adamantium.UI.Core.IUIComponent c ? FindNamed(c, "PART_CloseButton") : null;
                    if (button != null)
                    {
                        Adamantium.UI.Rendering.LayerProbe.Reset();
                        var show = true;
                        var flipFrames = Run(8, () =>
                        {
                            button.Visibility = show ? Adamantium.UI.Core.Visibility.Visible : Adamantium.UI.Core.Visibility.Hidden;
                            show = !show;
                            return true;
                        });
                        System.IO.File.AppendAllText(log + ".flip.txt",
                            $"flipping a close button: {flipFrames / 8.0:0} fps" + Environment.NewLine
                            + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + Environment.NewLine
                            + Adamantium.UI.Rendering.LayerProbe.Dump() + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_SPINNER_KIND=Dots|Ripple|...): press "+25 of that kind" and report the frames it costs.
                // The A/B the Animations tab was built for: equal counts of indicators that animate DIFFERENT things -
                // Dots move transforms only (composited), Ripple also animates element Opacity (no channel yet). Driven
                // from here so the two numbers are measured the same way rather than read off a plate by eye.
                if (Environment.GetEnvironmentVariable("ADAM_SPINNER_KIND") is { } kind)
                {
                    // Reached through the BUTTON rather than the view-model: the button is what a hand would press, and
                    // it needs nothing to be visible from here that the markup does not already expose.
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var button = win?.Content is Adamantium.UI.Core.IUIComponent root
                        ? FindButton(root, "+25 " + kind)
                        : null;

                    if (button != null)
                    {
                        // 25 at a time is below the noise - two runs of the same kind differed more than the two kinds
                        // did. Pressed repeatedly instead, because the difference between a composited channel and a
                        // re-bake is a PER-INSTANCE cost and only shows once there are enough instances to see it.
                        var presses = Environment.GetEnvironmentVariable("ADAM_SPINNER_PRESSES") is { } p ? int.Parse(p) : 10;
                        var idle = Run(3, () => false);

                        for (var i = 0; i < presses; i++)
                        {
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => button.Command?.Execute(null));
                            System.Threading.Thread.Sleep(400);
                        }

                        System.Threading.Thread.Sleep(3000);   // let them realize and settle before counting

                        var busy = Run(8, () => false);
                        System.IO.File.AppendAllText(log + ".spinner.txt",
                            $"{kind} x{presses * 25}: idle {idle / 3.0:0} fps -> running {busy / 8.0:0} fps " +
                            $"({1000.0 / Math.Max(1, idle / 3.0):0.00} -> {1000.0 / Math.Max(1, busy / 8.0):0.00} ms)" + Environment.NewLine);
                    }
                    else
                    {
                        System.IO.File.AppendAllText(log + ".spinner.txt",
                            $"{kind}: button not found - is the Animations tab open?" + Environment.NewLine);
                    }
                }

                // TEMP (ADAM_OPACITY_FADE=1): fade the DEEPEST-rooted container on this tab and report what one Opacity
                // change costs against the size of the subtree under it. This is the case element Opacity is actually
                // about: the value multiplies down the whole chain and is baked into every descendant's colour, so one
                // write re-bakes N units. Measuring it on flat leaf spinners - as the first attempt did - measures the
                // one shape where the cost cannot appear.
                if (Environment.GetEnvironmentVariable("ADAM_OPACITY_FADE") == "1")
                {
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    var target = win?.Content is Adamantium.UI.Core.IUIComponent root ? Heaviest(root) : null;

                    if (target != null)
                    {
                        var under = Descendants(target);
                        var still = Run(4, () => false);

                        var phase = 0.0;
                        var fading = Run(8, () =>
                        {
                            phase += 0.08;
                            target.Opacity = 0.55 + 0.45 * Math.Sin(phase);
                            return true;
                        });

                        System.IO.File.AppendAllText(log + ".fade.txt",
                            $"faded {target.GetType().Name} over {under} descendants: " +
                            $"still {still / 4.0:0} fps ({1000.0 / Math.Max(1, still / 4.0):0.00} ms) -> " +
                            $"fading {fading / 8.0:0} fps ({1000.0 / Math.Max(1, fading / 8.0):0.00} ms)" + Environment.NewLine);
                    }
                }

                // TEMP self-check (ADAM_THEME_SWAP=1): swap the theme from here and report the tab strip's height after
                // each swap. ~36 is a strip; anything larger is the "page inside a tab header" fault this hunt closed.
                var stripReport = "(not asked)";
                if (Environment.GetEnvironmentVariable("ADAM_THEME_SWAP") == "1")
                {
                    var themes = Adamantium.UI.Core.UIAppContext.Current?.ThemeManager;
                    var win = Adamantium.UI.UIApplication.Current?.MainWindow;
                    if (themes != null && win != null)
                    {
                        stripReport = string.Empty;
                        for (var lap = 0; lap < 2; lap++)
                        {
                            var currentVariant = (themes.CurrentTheme as Adamantium.UI.Core.Resources.Theme)?.CurrentVariant;
                            var wanted = currentVariant == Adamantium.UI.Core.Resources.ThemeVariant.Dark
                                ? Adamantium.UI.Core.Resources.ThemeVariant.Light
                                : Adamantium.UI.Core.Resources.ThemeVariant.Dark;
                            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => themes.SetVariant(wanted));
                            System.Threading.Thread.Sleep(6000);
                            stripReport += $"after {wanted}: strip {StripHeight(win):0} px; ";
                        }
                    }
                }

                var frames = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - startFrames;
                var s = Adamantium.UI.Core.Diagnostics.RuntimeStats.LastRenderDrawMs;
                System.IO.File.WriteAllText(log,
                    $"layout peak {layout:0} ms | WORST SECOND {(worstSecond == long.MaxValue ? 0 : worstSecond)} fps | presented {frames} in {sw.Elapsed.TotalSeconds:0.0} s = {frames / sw.Elapsed.TotalSeconds:0} fps" + System.Environment.NewLine
                    + $"sampled avg ms: prerender {sumPre * inv:0.00} layout {sumLayout * inv:0.00} record {sumRecord * inv:0.00} apply {sumApply * inv:0.00} proc {sumProc * inv:0.00} draw {sumDraw * inv:0.00} processors {sumProcessors * inv:0.00}" + System.Environment.NewLine
                    + $"frame steps avg ms: beginDraw {sumBegin * inv:0.00} = fence {sumFence * inv:0.00} + setup {sumSetup * inv:0.00} + record/apply/prerender {(sumRecord + sumApply + sumPre) * inv:0.00} + acquire {sumAcquire * inv:0.00}" + System.Environment.NewLine
                    + $"                    endDraw {sumEnd * inv:0.00} submit {sumSubmit * inv:0.00} present {sumPresent * inv:0.00}" + System.Environment.NewLine
                    + $"sampled max ms: record {maxRecord:0.0} apply {maxApply:0.0} draw {maxDraw:0.0} | frame budget at {frames / sw.Elapsed.TotalSeconds:0} fps = {1000.0 / (frames / sw.Elapsed.TotalSeconds):0.00} ms" + System.Environment.NewLine
                    + Adamantium.UI.Core.Diagnostics.FrameTrace.Percentiles() + System.Environment.NewLine
                    + Adamantium.UI.Rendering.LayerProbe.Dump() + System.Environment.NewLine
                    + Adamantium.UI.Rendering.LayerProbe.LastOpDump + System.Environment.NewLine
                    + "theme swap: " + stripReport + System.Environment.NewLine
                    + "churn:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.OrderByDescending(churn, p => p.Value), p => $"  {p.Value,5}  {p.Key}"))
                    + System.Environment.NewLine);

                // Every frame that ran LONG, one line each: what kind of build it was, why it could not replay, and how
                // much of it was layout and record. A spike a hand reproduces is only worth anything if it names itself.
                System.IO.File.WriteAllText(log + ".frames.txt",
                    "presentation extensions: " + DescribePresentationSupport() + System.Environment.NewLine
                    + "patch STILL re-bakes (by unit type, whole run):" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.Take(
                            System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.FrameTrace.Patched, p => p.Value), 8),
                        p => $"  {p.Value,7}  {p.Key}")) + System.Environment.NewLine
                    + "patch refusals by reason:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.FrameTrace.Refusals, p => p.Value),
                        p => $"  {p.Value,5}  {p.Key}")) + System.Environment.NewLine
                    + "not node-aware:" + System.Environment.NewLine
                    + string.Join(System.Environment.NewLine, System.Linq.Enumerable.Select(
                        System.Linq.Enumerable.OrderByDescending(Adamantium.UI.Core.Diagnostics.FrameTrace.NotAware, p => p.Value),
                        p => $"  {p.Value,5}  {p.Key}")) + System.Environment.NewLine
                    + Adamantium.UI.Core.Diagnostics.FrameTrace.DumpIncidents());
                if (Environment.GetEnvironmentVariable("ADAM_PROBE_EXIT") == "1") Environment.Exit(0);
            }) { IsBackground = true };
            t.Start();
        }
        gameApp.IsFixedTimeStep = false;
        SetUp(gameApp);
    }

    private static int _panned;   // TEMP: pans that actually moved the strip - a harness that pans nothing measures nothing
    private static double _pan = 8;      // the small oscillation's current direction
    private static double _sweep = 48;   // the end-to-end sweep's current direction (a wheel notch)

    // TEMP: post one pan per frame-ish for the given seconds, and report the frames presented while doing it.
    // TEMP (ADAM_OPACITY_FADE): how many visual descendants a node carries, and the node carrying the most of them -
    // the subtree whose fade is worth timing.
    private static int Descendants(Adamantium.UI.Core.IUIComponent node)
    {
        var count = 0;
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var child in n.VisualChildren) { count++; stack.Push(child); }
        }

        return count;
    }

    private static Adamantium.UI.Controls.Base.UIComponent Heaviest(Adamantium.UI.Core.IUIComponent root)
    {
        Adamantium.UI.Controls.Base.UIComponent best = null;
        var bestCount = 0;
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            foreach (var child in n.VisualChildren) stack.Push(child);

            // The tab's CONTENT, not the shell around it. Picking "whatever has the most descendants" picked a Grid of
            // 379 - the tab's own frame - while the thing worth fading is the panel holding the items. An ItemsControl
            // is that panel by construction, so the search is restricted to one.
            if (n is not Adamantium.UI.Controls.ItemsControl ui || ReferenceEquals(n, root)) continue;

            var under = Descendants(n);
            if (under <= bestCount) continue;

            best = ui;
            bestCount = under;
        }

        return best;
    }

    // TEMP (ADAM_SPINNER_KIND): the button whose Content reads exactly this, anywhere under root.
    private static Adamantium.UI.Controls.Primitives.ButtonBase FindButton(Adamantium.UI.Core.IUIComponent root, string content)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.Primitives.ButtonBase b && Equals(b.Content, content)) return b;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // A PRESS AT A PLACE IN THE WORLD, for a probe that drives a tool without touching a mouse.
    // A point the way a HAND gives one: screen pixels, turned into world by the camera. At() below takes world
    // coordinates, which says nothing about what a gesture does at 8x.
    private static Adamantium.UI.Controls.DrawingBoard.CanvasPointerEventArgs OnScreen(
        Adamantium.UI.Controls.DrawingBoard.InfiniteCanvas canvas, double x, double y)
    {
        var screen = new Adamantium.Mathematics.Vector2(x, y);
        var world = canvas.ScreenToWorld(screen);

        return new Adamantium.UI.Controls.DrawingBoard.CanvasPointerEventArgs
        {
            World = world,
            Pointer = world,
            Screen = screen,
            Button = Adamantium.UI.Core.Input.MouseButtons.Left,
            ClickCount = 1
        };
    }

    private static Adamantium.UI.Controls.DrawingBoard.CanvasPointerEventArgs At(
        Adamantium.UI.Controls.DrawingBoard.InfiniteCanvas canvas, double x, double y)
    {
        var world = new Adamantium.Mathematics.Vector2(x, y);

        return new Adamantium.UI.Controls.DrawingBoard.CanvasPointerEventArgs
        {
            World = world,
            Pointer = world,
            Screen = canvas.WorldToScreen(world),
            Button = Adamantium.UI.Core.Input.MouseButtons.Left,
            ClickCount = 1
        };
    }

    private static Adamantium.UI.Controls.DrawingBoard.InfiniteCanvas FindCanvas(Adamantium.UI.Core.IUIComponent root)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.DrawingBoard.InfiniteCanvas canvas) return canvas;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    private static long Run(double seconds, Func<bool> pan)
    {
        var from = Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            Adamantium.UI.Threading.Dispatcher.CurrentDispatcher?.Post(() => { if (pan()) System.Threading.Interlocked.Increment(ref _panned); });
            System.Threading.Thread.Sleep(16);
        }

        return Adamantium.UI.Core.Diagnostics.RuntimeStats.PresentedFrames - from;
    }

    // TEMP: the first control of a kind under a root - the harnesses need to reach a viewer or a strip by type.
    // ADAM_START_AT=<x>,<y> and ADAM_START_MAXIMIZED=1: put the window where the measurement is taken and open it at the
    // size it is taken at.
    //
    // WHY THIS IS PART OF THE INSTRUMENT and not a convenience: this tab's cost scales with the number of tiles on
    // screen, so the window's SIZE is an input to every number the run reports - and the size a run happens to open at
    // is not the size anybody measures. Two readings taken on two different monitors are two different experiments, and
    // comparing them is how a shrug becomes a regression and back again. Moving the window by hand before each run makes
    // the placement part of the protocol; stating it here makes it part of the run.
    private static void PlaceForMeasurement()
    {
        if (Adamantium.UI.UIApplication.Current?.MainWindow is not Adamantium.UI.Controls.WindowBase window) return;

        if (Environment.GetEnvironmentVariable("ADAM_START_AT") is { Length: > 0 } at)
        {
            var parts = at.Split(',');
            if (parts.Length == 2
                && double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x)
                && double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y))
            {
                window.Left = x;
                window.Top = y;
            }
        }

        if (Environment.GetEnvironmentVariable("ADAM_START_MAXIMIZED") == "1") window.Maximize();
    }

    private static T Find<T>(Adamantium.UI.Core.IUIComponent root) where T : class
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is T hit) return hit;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // TEMP: the first control with this Name under a root.
    /// <summary>Which presentation extensions this MACHINE turned out to have - asked of the device, so a report from a
    /// different GPU or a Mac says what was true there rather than what the wish list hoped for.</summary>
    private static string DescribePresentationSupport()
    {
        var service = Adamantium.UI.UIApplication.Current?.Container
            ?.Resolve<Adamantium.Graphics.Core.IGraphicsDeviceService>();
        var main = (service as Adamantium.UI.Services.GraphicsDeviceService)?.MainGraphicsDevice;
        if (main == null) return "no device";

        return $"swapchainMaintenance {main.SupportsSwapchainMaintenance} | presentWait {main.SupportsPresentWait}"
             + $" | incrementalPresent {main.SupportsIncrementalPresent}";
    }

    private static Adamantium.UI.Controls.Base.UIComponent FindNamed(Adamantium.UI.Core.IUIComponent root, string name)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.Base.UIComponent ui && ui.Name == name) return ui;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // TEMP: the tab strip, for the pan self-check above.
    private static Adamantium.UI.Controls.TabStripScroller FindStrip(Adamantium.UI.Core.IUIComponent root)
    {
        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node is Adamantium.UI.Controls.TabStripScroller strip) return strip;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return null;
    }

    // TEMP: the tab strip's height, for the self-check above.
    private static double StripHeight(object win)
    {
        var content = win.GetType().GetProperty("Content")?.GetValue(win) as Adamantium.UI.Core.IUIComponent;
        if (content == null) return -1;

        var stack = new System.Collections.Generic.Stack<Adamantium.UI.Core.IUIComponent>();
        stack.Push(content);
        while (stack.Count > 0)
        {
            var node = stack.Pop();
            if (node.GetType().Name == "TabStripScroller") return node.RenderSize.Height;
            foreach (var child in node.VisualChildren) stack.Push(child);
        }

        return -1;
    }

    private static void SetUp(AdamantiumGameApplication gameApp)
    {
        gameApp.EnableGraphicsDebug = Environment.GetEnvironmentVariable("ADAM_VK_DEBUG") == "1";
        gameApp.DesiredFPS = 300;
        gameApp.StartupType = typeof(MainWindow);
        gameApp.Run();
    }
}
