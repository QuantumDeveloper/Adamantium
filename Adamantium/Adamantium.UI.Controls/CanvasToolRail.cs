using System;
using System.Collections.Generic;
using Adamantium.UI.Controls.Panels;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Resources;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>A button per tool of an <see cref="InfiniteCanvas"/>, built from the canvas's own
/// <see cref="InfiniteCanvas.Tools"/>: the picture, the name and the key all come off the tool, and the button that is
/// marked is the one holding <see cref="InfiniteCanvas.Tool"/>.
/// <para>Built here rather than written out in markup, and that is the whole saving. Every fact a button needs is a
/// fact about the TOOL, so a hand-written row of buttons is a second copy of the tool list that has to be kept in step
/// by hand - which is how adding one tool turned into six edits, five of them about showing it.</para>
/// <para>An application that wants something else puts its own content in the pane: this is what a rail does when
/// nobody says otherwise, not the only thing a rail may be.</para></summary>
/// <para>A WRAPPING panel and not a stack: how many tools there are is the application's business, and a rail taller
/// than the viewport is one whose last tools cannot be reached. Too many for one column become a second, which is what
/// the hand-written panel did by splitting the buttons into rows - except that this one does it at whatever size the
/// canvas happens to be.</para>
public class CanvasToolRail : WrapPanel
{
    private readonly List<ToggleButton> _buttons = new();
    private readonly Dictionary<ButtonBase, ICanvasTool> _of = new();
    private readonly Dictionary<ToggleButton, List<ICanvasTool>> _families = new();
    private readonly List<ButtonBase> _choices = new();

    private InfiniteCanvas _canvas;
    private Popup _popup;
    private ToggleButton _openFor;

    public static readonly AdamantiumProperty CanvasProperty = AdamantiumProperty.Register(nameof(Canvas),
        typeof(InfiniteCanvas), typeof(CanvasToolRail), new PropertyMetadata(null, OnCanvasChanged));

    // TWO numbers and not one Size: a Size has no type parser, so a theme stating one as "30,28" throws while the
    // template is being built - and a template that throws is a theme that does not apply, which shows up as the
    // control having no panel at all rather than as a wrong number.
    public static readonly AdamantiumProperty ButtonWidthProperty = AdamantiumProperty.Register(nameof(ButtonWidth),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(30.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty ButtonHeightProperty = AdamantiumProperty.Register(nameof(ButtonHeight),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(28.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty IconSizeProperty = AdamantiumProperty.Register(nameof(IconSize),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(13.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty ButtonGapProperty = AdamantiumProperty.Register(nameof(ButtonGap),
        typeof(Double), typeof(CanvasToolRail),
        new PropertyMetadata(4.0, PropertyMetadataOptions.AffectsMeasure, OnLookChanged));

    public static readonly AdamantiumProperty GroupIconProperty = AdamantiumProperty.Register(nameof(GroupIcon),
        typeof(String), typeof(CanvasToolRail),
        new PropertyMetadata("ToolAddIcon", PropertyMetadataOptions.AffectsRender, OnLookChanged));

    /// <summary>The canvas whose tools these are. Set by the pane the rail sits in, which got it from the layer.
    /// </summary>
    public InfiniteCanvas Canvas
    {
        get => GetValue<InfiniteCanvas>(CanvasProperty);
        set => SetValue(CanvasProperty, value);
    }

    /// <summary>How wide one tool button is.</summary>
    public Double ButtonWidth
    {
        get => GetValue<Double>(ButtonWidthProperty);
        set => SetValue(ButtonWidthProperty, value);
    }

    /// <summary>How tall one tool button is.</summary>
    public Double ButtonHeight
    {
        get => GetValue<Double>(ButtonHeightProperty);
        set => SetValue(ButtonHeightProperty, value);
    }

    /// <summary>How big the picture inside it is.</summary>
    public Double IconSize
    {
        get => GetValue<Double>(IconSizeProperty);
        set => SetValue(IconSizeProperty, value);
    }

    /// <summary>The space between two buttons.</summary>
    public Double ButtonGap
    {
        get => GetValue<Double>(ButtonGapProperty);
        set => SetValue(ButtonGapProperty, value);
    }

    /// <summary>The picture on the button that OPENS a family of tools. A plus by default, which is what that button
    /// means: not a tool, but more of them.</summary>
    public String GroupIcon
    {
        get => GetValue<String>(GroupIconProperty);
        set => SetValue(GroupIconProperty, value);
    }

    private static void OnCanvasChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e)
    {
        if (component is not CanvasToolRail rail) return;

        // Unhooked from the OLD one and not from whatever is current: the canvas is not the rail's to own, and a rail
        // that let go of nothing would go on rebuilding itself for a canvas that has moved on.
        if (rail._canvas != null)
        {
            rail._canvas.ToolsChanged -= rail.OnToolsChanged;
            rail._canvas.ToolChanged -= rail.OnToolChanged;
        }

        rail._canvas = e.NewValue as InfiniteCanvas;

        if (rail._canvas != null)
        {
            rail._canvas.ToolsChanged += rail.OnToolsChanged;
            rail._canvas.ToolChanged += rail.OnToolChanged;
        }

        rail.Rebuild();
    }

    private static void OnLookChanged(AdamantiumComponent component, AdamantiumPropertyChangedEventArgs e) =>
        (component as CanvasToolRail)?.Rebuild();

    private void OnToolsChanged(object sender, EventArgs e) => Rebuild();

    private void OnToolChanged(object sender, EventArgs e) => MarkCurrent();

    private void Rebuild()
    {
        foreach (var button in _buttons) button.Click -= OnToolPicked;
        foreach (var choice in _choices) choice.Click -= OnChoicePicked;

        _buttons.Clear();
        _of.Clear();
        _families.Clear();
        _choices.Clear();
        Children.Clear();
        ClosePopup();

        if (_canvas?.Tools is not { } tools) return;

        // A family takes ONE button, placed where its FIRST member would have gone: the rail keeps the order the
        // application stated, and a family does not jump to the end just because it is a family.
        var families = new Dictionary<string, List<ICanvasTool>>();
        var order = new List<object>();

        foreach (var tool in tools)
        {
            if (tool == null) continue;

            var group = tool.Group;
            if (string.IsNullOrEmpty(group))
            {
                order.Add(tool);
                continue;
            }

            if (families.TryGetValue(group, out var family))
            {
                family.Add(tool);
                continue;
            }

            families[group] = new List<ICanvasTool> { tool };
            order.Add(group);
        }

        var gap = ButtonGap;
        foreach (var entry in order)
        {
            if (entry is string name)
            {
                Children.Add(FamilyButton(name, families[name], gap));
                continue;
            }

            var tool = (ICanvasTool)entry;
            var iconic = !string.IsNullOrEmpty(tool.Icon);

            var button = new ToggleButton
            {
                // A tool with a PICTURE gets the stated square; one without shows its name and has to be as wide as
                // that name is - forced to the square, "Button" came out cut in half.
                Width = iconic ? ButtonWidth : Double.NaN,
                Height = ButtonHeight,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(iconic ? 0 : 8, 0, iconic ? 0 : 8, 0),
                ToolTip = Tip(tool)
            };

            // On the TRAILING sides only. Two neighbours still make exactly one gap between them, whichever way the
            // rail runs and wherever it wraps - but the first button's left and top edges stay flush, so the column of
            // tools lines up with the pane's own handles above it. Half a gap on every side put the tools two pixels
            // right of the handles, which reads as crooked and is.
            button.Margin = new Thickness(0, 0, gap, gap);

            if (iconic)
            {
                var image = new Image { Width = IconSize, Height = IconSize };

                // LIVE and not a one-shot lookup: a theme swap has to reach the rail's pictures like it reaches
                // everything else, and the tool names a key precisely so the theme can answer differently.
                new ObservableResource(tool.Icon).Apply(image, nameof(Image.Source));
                button.Content = image;
            }
            else
            {
                button.Content = tool.Name;
            }

            button.Click += OnToolPicked;

            _buttons.Add(button);
            _of[button] = tool;
            Children.Add(button);
        }

        MarkCurrent();
    }

    // The button that stands for a whole family, and the list it opens. A ToggleButton like the rest, so a family
    // holding the tool in hand is marked exactly as a lone tool would be - what is in hand should be visible without
    // opening anything.
    private ToggleButton FamilyButton(string name, List<ICanvasTool> family, double gap)
    {
        var button = new ToggleButton
        {
            Width = ButtonWidth,
            Height = ButtonHeight,
            MinWidth = 0,
            MinHeight = 0,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 0, gap, gap),
            ToolTip = name
        };

        var image = new Image { Width = IconSize, Height = IconSize };
        new ObservableResource(GroupIcon).Apply(image, nameof(Image.Source));
        button.Content = image;

        button.Click += OnToolPicked;

        _buttons.Add(button);
        _families[button] = family;

        return button;
    }

    private void MarkCurrent()
    {
        var current = _canvas?.Tool;

        foreach (var button in _buttons)
        {
            bool wanted;
            if (_families.TryGetValue(button, out var family))
            {
                wanted = false;
                foreach (var tool in family)
                {
                    if (!ReferenceEquals(tool, current)) continue;

                    wanted = true;
                    break;
                }
            }
            else
            {
                wanted = _of.TryGetValue(button, out var tool) && ReferenceEquals(tool, current);
            }

            if (button.IsChecked != wanted) button.IsChecked = wanted;
        }
    }

    private void OnToolPicked(object sender, RoutedEventArgs e)
    {
        if (_canvas == null || sender is not ToggleButton button) return;

        if (_families.TryGetValue(button, out var family))
        {
            // A SECOND press on the button that opened the list closes it. Opening only, the list could be got rid of
            // by picking something from it and no other way - and picking something is exactly what somebody who
            // opened it by mistake does not want to do.
            if (ReferenceEquals(_openFor, button)) ClosePopup();
            else OpenFamily(button, family);

            // Put the mark back where it belongs: a ToggleButton ticks itself on the press, and opening a list is not
            // taking a tool in hand. The list being on screen is what says it is open.
            MarkCurrent();
            e.Handled = true;
            return;
        }

        if (!_of.TryGetValue(button, out var tool)) return;

        _canvas.SetCurrentValue(InfiniteCanvas.ToolProperty, tool);

        // Marked from the CANVAS's answer and not from the press: the tool that ends up in hand is the canvas's to say,
        // and a button that ticked itself would lie the moment anything refused.
        MarkCurrent();
        e.Handled = true;
    }

    private void OpenFamily(ToggleButton button, List<ICanvasTool> family)
    {
        ClosePopup();

        var list = new Panels.StackPanel { Orientation = Orientation.Vertical, Margin = new Thickness(4) };

        foreach (var tool in family)
        {
            var choice = new Buttons.Button
            {
                Content = Row(tool),
                MinWidth = 0,
                MinHeight = 0,
                Height = ButtonHeight,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Padding = new Thickness(6, 0, 10, 0),
                Margin = new Thickness(0, 0, 0, 2),
                ToolTip = Tip(tool)
            };

            choice.Click += OnChoicePicked;
            _of[choice] = tool;
            _choices.Add(choice);
            list.Children.Add(choice);
        }

        // A PLATE under the list, and the same one every other flyout in the theme stands on: without it the rows are
        // drawn straight over the drawing, and a list you can see the canvas through is not a list.
        var plate = new Decorators.Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new ProceduralGeometry.CornerRadius(4),
            Child = list
        };

        new ObservableResource("FlyoutSurfaceFill").Apply(plate, nameof(Decorators.Border.Background));
        new ObservableResource("ControlStrokeColorDefault").Apply(plate, nameof(Decorators.Border.BorderBrush));

        // LIGHT-DISMISSED, which is the one thing every other flyout in the application already does: a press anywhere
        // else puts the list away. IgnoreTargetPress keeps the button that opened it out of that, so its own press
        // reaches the click above and TOGGLES - otherwise the press would close the list and the release would open it
        // straight back, and the button would look like it did nothing.
        _popup = new Popup
        {
            PlacementTarget = button,
            Placement = PlacementMode.Right,
            Child = plate,
            KeepOpen = false,
            IgnoreTargetPress = true,
            IsOpen = true
        };

        _popup.Closed += OnPopupClosed;
        _openFor = button;
    }

    // The list can go away without the rail asking - a press outside it - and the rail has to hear that, or the next
    // press on the button would think the list was still open and refuse to open it.
    private void OnPopupClosed(object sender, EventArgs e)
    {
        if (sender is Popup popup) popup.Closed -= OnPopupClosed;

        _popup = null;
        _openFor = null;
        MarkCurrent();
    }

    // One line of the family list: the tool's own picture beside its own name. The name is written out here and not
    // only tucked into a tip, because a family is opened exactly when the pictures alone were not enough.
    private IMeasurableComponent Row(ICanvasTool tool)
    {
        var row = new Panels.StackPanel { Orientation = Orientation.Horizontal };

        if (!string.IsNullOrEmpty(tool.Icon))
        {
            var image = new Image
            {
                Width = IconSize,
                Height = IconSize,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };

            new ObservableResource(tool.Icon).Apply(image, nameof(Image.Source));
            row.Children.Add(image);
        }

        row.Children.Add(new Text.TextBlock
        {
            Text = string.IsNullOrEmpty(tool.Name) ? tool.GetType().Name : tool.Name,
            VerticalAlignment = VerticalAlignment.Center,
            VerticalTextAlignment = Graphics.Fonts.VerticalTextAlignment.Center
        });

        return row;
    }

    private void OnChoicePicked(object sender, RoutedEventArgs e)
    {
        if (_canvas == null || sender is not ButtonBase choice || !_of.TryGetValue(choice, out var tool)) return;

        _canvas.SetCurrentValue(InfiniteCanvas.ToolProperty, tool);
        ClosePopup();
        MarkCurrent();
        e.Handled = true;
    }

    private void ClosePopup()
    {
        if (_popup == null) return;

        // Let go BEFORE closing: closing raises Closed, and a handler still attached would run back through here on a
        // field that has not been cleared yet.
        var popup = _popup;
        _popup = null;
        _openFor = null;

        popup.Closed -= OnPopupClosed;
        popup.IsOpen = false;
    }

    private static string Tip(ICanvasTool tool)
    {
        var name = string.IsNullOrEmpty(tool.Name) ? tool.GetType().Name : tool.Name;
        var key = tool.Shortcut == Key.None ? string.Empty : $" ({tool.Shortcut})";
        var about = string.IsNullOrEmpty(tool.Description) ? string.Empty : $" - {tool.Description}";

        return name + key + about;
    }
}
