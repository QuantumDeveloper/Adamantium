using System;
using System.Globalization;
using Adamantium.UI.Controls.Primitives;
using Adamantium.UI.Controls.Text;
using Adamantium.UI.Core;
using Adamantium.UI.Core.Input;
using Adamantium.UI.Core.Media;
using Adamantium.UI.Core.RoutedEvents;

namespace Adamantium.UI.Controls;

/// <summary>
/// A number with two buttons that step it. The limits and the two step sizes come from <see cref="RangeLimitsBase"/>,
/// which is also where a Slider gets them; this control owns the text side of it - formatting, parsing, what a keystroke
/// is allowed to be - and the stepping.
/// <para>Its <see cref="Value"/> is NULLABLE, because an entry box can be empty and empty is not zero. That is why it
/// does not sit on <see cref="RangeBase"/>, whose Value is a plain double: the limits live one level up precisely so a
/// control can bring its own idea of what is selected inside them (a RangeSlider brings two). MahApps had to go all the
/// way down to Control for the same reason, having no such rung.</para>
/// <para>Where the buttons sit is <see cref="ButtonsPlacement"/>, and WHICH of the pair is which is the separate
/// <see cref="AreButtonsSwapped"/>: keeping them apart means every placement can be had in either order, instead of one
/// enum having to spell out the product of the two.</para>
/// </summary>
public class NumericUpDown : RangeLimitsBase
{
    private TextBox _text;
    private RepeatButton _increase;
    private RepeatButton _decrease;
    private bool _writingText;     // the value is writing the text; the text must not write back
    private long _lastStepTime;    // for the speed-up: repeats arrive close together, a fresh press does not
    private int _heldSteps;

    /// <summary>The number, or NOTHING: an entry box can be cleared, and an empty box is not a box holding zero. Every
    /// number that gets in is clamped into [Minimum, Maximum]; null passes straight through, being outside neither.</summary>
    public static readonly AdamantiumProperty ValueProperty = AdamantiumProperty.Register(nameof(Value),
        typeof(double?), typeof(NumericUpDown),
        new PropertyMetadata(null, PropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged, CoerceValue));

    public static readonly AdamantiumProperty ButtonsPlacementProperty = AdamantiumProperty.Register(
        nameof(ButtonsPlacement), typeof(NumericButtonsPlacement), typeof(NumericUpDown),
        new PropertyMetadata(NumericButtonsPlacement.Split));

    /// <summary>Swaps the two buttons, whatever <see cref="ButtonsPlacement"/> they are in.</summary>
    public static readonly AdamantiumProperty AreButtonsSwappedProperty = AdamantiumProperty.Register(
        nameof(AreButtonsSwapped), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    public static readonly AdamantiumProperty AreButtonsVisibleProperty = AdamantiumProperty.Register(
        nameof(AreButtonsVisible), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(true));

    public static readonly AdamantiumProperty ButtonsWidthProperty = AdamantiumProperty.Register(
        nameof(ButtonsWidth), typeof(double), typeof(NumericUpDown), new PropertyMetadata(28.0));

    /// <summary>Whether the buttons take keyboard focus when clicked. Off by default: focus belongs in the text, so a
    /// click on a button does not cost you the caret.</summary>
    public static readonly AdamantiumProperty AreButtonsFocusableProperty = AdamantiumProperty.Register(
        nameof(AreButtonsFocusable), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IncreaseButtonContentProperty = AdamantiumProperty.Register(
        nameof(IncreaseButtonContent), typeof(object), typeof(NumericUpDown), new PropertyMetadata("+"));

    public static readonly AdamantiumProperty DecreaseButtonContentProperty = AdamantiumProperty.Register(
        nameof(DecreaseButtonContent), typeof(object), typeof(NumericUpDown), new PropertyMetadata("−"));

    /// <summary>How the value is written out: either a plain .NET numeric format ("N2", "P0") or a full composite
    /// format string ("{0:N2} kg"). Empty = the culture's own default.</summary>
    public static readonly AdamantiumProperty StringFormatProperty = AdamantiumProperty.Register(
        nameof(StringFormat), typeof(string), typeof(NumericUpDown), new PropertyMetadata(null, OnFormatChanged));

    /// <summary>Culture used to write and to read the number back. Null = the current culture.</summary>
    public static readonly AdamantiumProperty CultureProperty = AdamantiumProperty.Register(
        nameof(Culture), typeof(CultureInfo), typeof(NumericUpDown), new PropertyMetadata(null, OnFormatChanged));

    public static readonly AdamantiumProperty InputModeProperty = AdamantiumProperty.Register(
        nameof(InputMode), typeof(NumericInputMode), typeof(NumericUpDown),
        new PropertyMetadata(NumericInputMode.Decimals));

    public static readonly AdamantiumProperty ParsingNumberStyleProperty = AdamantiumProperty.Register(
        nameof(ParsingNumberStyle), typeof(NumberStyles), typeof(NumericUpDown),
        new PropertyMetadata(NumberStyles.Any));

    public static readonly AdamantiumProperty IsManualEntryEnabledProperty = AdamantiumProperty.Register(
        nameof(IsManualEntryEnabled), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(true));

    /// <summary>Read the number back on every keystroke rather than when the entry is committed. Off by default: a
    /// half-typed number is a number too, so "5" on its way to "50" is clamped to a Minimum of 10 and the rest of the
    /// keystrokes have nothing left to build on.</summary>
    public static readonly AdamantiumProperty ChangesValueWhileTypingProperty = AdamantiumProperty.Register(
        nameof(ChangesValueWhileTyping), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    public static readonly AdamantiumProperty AreArrowKeysEnabledProperty = AdamantiumProperty.Register(
        nameof(AreArrowKeysEnabled), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(true));

    public static readonly AdamantiumProperty IsMouseWheelEnabledProperty = AdamantiumProperty.Register(
        nameof(IsMouseWheelEnabled), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(true));

    /// <summary>Let the wheel step the value while the pointer is merely OVER the control, without it holding focus.
    /// Off by default, because a control that eats the wheel unasked stops the page it sits on from scrolling.</summary>
    public static readonly AdamantiumProperty TracksMouseWheelWhenOverProperty = AdamantiumProperty.Register(
        nameof(TracksMouseWheelWhenOver), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsSpeedupEnabledProperty = AdamantiumProperty.Register(
        nameof(IsSpeedupEnabled), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(true));

    /// <summary>Press the box and drag sideways to run the value up and down, the way a property panel in a 3D tool
    /// does. OFF by default and deliberately so: it takes the press over, so dragging inside the box no longer selects
    /// text (double- and triple-click still do). Pair it with <see cref="AreButtonsVisible"/> off for the bare field
    /// those tools actually show.</summary>
    public static readonly AdamantiumProperty IsDragScrubEnabledProperty = AdamantiumProperty.Register(
        nameof(IsDragScrubEnabled), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    /// <summary>How far the pointer travels for one step of the drag. In pixels PER STEP rather than units per pixel, so
    /// the feel stays put when <see cref="RangeLimitsBase.SmallChange"/> changes.</summary>
    public static readonly AdamantiumProperty ScrubPixelsPerStepProperty = AdamantiumProperty.Register(
        nameof(ScrubPixelsPerStep), typeof(double), typeof(NumericUpDown), new PropertyMetadata(4.0));

    /// <summary>The pointer shown while the drag is running LEFT (the value going down), and the one for RIGHT. Two,
    /// because the drag has a direction and the pointer is the only thing that says which way it is currently reading -
    /// the pair of one-way shapes every 3D tool uses. The catalog has no such pair yet, so both start as the plain
    /// double-headed <see cref="Cursors.SizeEWE"/>; point them at your own <c>.cur</c> (<c>new Cursor(path)</c>) or at
    /// any other catalog shape to change that.</summary>
    public static readonly AdamantiumProperty ScrubLeftCursorProperty = AdamantiumProperty.Register(
        nameof(ScrubLeftCursor), typeof(Cursor), typeof(NumericUpDown), new PropertyMetadata(Cursors.SizeEWE));

    public static readonly AdamantiumProperty ScrubRightCursorProperty = AdamantiumProperty.Register(
        nameof(ScrubRightCursor), typeof(Cursor), typeof(NumericUpDown), new PropertyMetadata(Cursors.SizeEWE));

    /// <summary>Round the value to a whole number of <see cref="RangeLimitsBase.SmallChange"/> steps.</summary>
    public static readonly AdamantiumProperty SnapsToSmallChangeProperty = AdamantiumProperty.Register(
        nameof(SnapsToSmallChange), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    public static readonly AdamantiumProperty IsReadOnlyProperty = AdamantiumProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    public static readonly AdamantiumProperty DelayProperty = AdamantiumProperty.Register(nameof(Delay),
        typeof(int), typeof(NumericUpDown), new PropertyMetadata(500));

    public static readonly AdamantiumProperty RepeatIntervalProperty = AdamantiumProperty.Register(
        nameof(RepeatInterval), typeof(int), typeof(NumericUpDown), new PropertyMetadata(33));

    public event EventHandler<NullableValueChangedEventArgs> ValueChanged;

    /// <summary>The value ran into <see cref="RangeLimitsBase.Minimum"/> - a step asked for less than the range holds.</summary>
    public event EventHandler MinimumReached;

    /// <summary>The value ran into <see cref="RangeLimitsBase.Maximum"/>.</summary>
    public event EventHandler MaximumReached;

    public double? Value
    {
        get => GetValue<double?>(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    static NumericUpDown()
    {
        // Unbounded unless told otherwise: a spinner over the 0..1 that RangeLimitsBase defaults to could not count to
        // two. Via metadata, never a constructor set - a set writes Local priority and would mask a {Binding} forever.
        MinimumProperty.OverrideMetadata(typeof(NumericUpDown), new PropertyMetadata(double.MinValue));
        MaximumProperty.OverrideMetadata(typeof(NumericUpDown), new PropertyMetadata(double.MaxValue));
    }

    public NumericUpDown()
    {
        AddHandler(Keyboard.PreviewTextInputEvent, new TextInputEventHandler(OnPreviewTextInput));
        AddHandler(Mouse.MouseWheelEvent, new MouseWheelEventHandler(OnMouseWheelStep));
    }

    public NumericButtonsPlacement ButtonsPlacement
    {
        get => GetValue<NumericButtonsPlacement>(ButtonsPlacementProperty);
        set => SetValue(ButtonsPlacementProperty, value);
    }

    public bool AreButtonsSwapped
    {
        get => GetValue<bool>(AreButtonsSwappedProperty);
        set => SetValue(AreButtonsSwappedProperty, value);
    }

    public bool AreButtonsVisible
    {
        get => GetValue<bool>(AreButtonsVisibleProperty);
        set => SetValue(AreButtonsVisibleProperty, value);
    }

    public double ButtonsWidth
    {
        get => GetValue<double>(ButtonsWidthProperty);
        set => SetValue(ButtonsWidthProperty, value);
    }

    public bool AreButtonsFocusable
    {
        get => GetValue<bool>(AreButtonsFocusableProperty);
        set => SetValue(AreButtonsFocusableProperty, value);
    }

    public object IncreaseButtonContent
    {
        get => GetValue<object>(IncreaseButtonContentProperty);
        set => SetValue(IncreaseButtonContentProperty, value);
    }

    public object DecreaseButtonContent
    {
        get => GetValue<object>(DecreaseButtonContentProperty);
        set => SetValue(DecreaseButtonContentProperty, value);
    }

    public string StringFormat
    {
        get => GetValue<string>(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    public CultureInfo Culture
    {
        get => GetValue<CultureInfo>(CultureProperty);
        set => SetValue(CultureProperty, value);
    }

    public NumericInputMode InputMode
    {
        get => GetValue<NumericInputMode>(InputModeProperty);
        set => SetValue(InputModeProperty, value);
    }

    public NumberStyles ParsingNumberStyle
    {
        get => GetValue<NumberStyles>(ParsingNumberStyleProperty);
        set => SetValue(ParsingNumberStyleProperty, value);
    }

    public bool IsManualEntryEnabled
    {
        get => GetValue<bool>(IsManualEntryEnabledProperty);
        set => SetValue(IsManualEntryEnabledProperty, value);
    }

    public bool ChangesValueWhileTyping
    {
        get => GetValue<bool>(ChangesValueWhileTypingProperty);
        set => SetValue(ChangesValueWhileTypingProperty, value);
    }

    public bool AreArrowKeysEnabled
    {
        get => GetValue<bool>(AreArrowKeysEnabledProperty);
        set => SetValue(AreArrowKeysEnabledProperty, value);
    }

    public bool IsMouseWheelEnabled
    {
        get => GetValue<bool>(IsMouseWheelEnabledProperty);
        set => SetValue(IsMouseWheelEnabledProperty, value);
    }

    public bool TracksMouseWheelWhenOver
    {
        get => GetValue<bool>(TracksMouseWheelWhenOverProperty);
        set => SetValue(TracksMouseWheelWhenOverProperty, value);
    }

    public bool IsSpeedupEnabled
    {
        get => GetValue<bool>(IsSpeedupEnabledProperty);
        set => SetValue(IsSpeedupEnabledProperty, value);
    }

    public bool IsDragScrubEnabled
    {
        get => GetValue<bool>(IsDragScrubEnabledProperty);
        set => SetValue(IsDragScrubEnabledProperty, value);
    }

    public double ScrubPixelsPerStep
    {
        get => GetValue<double>(ScrubPixelsPerStepProperty);
        set => SetValue(ScrubPixelsPerStepProperty, value);
    }

    public Cursor ScrubLeftCursor
    {
        get => GetValue<Cursor>(ScrubLeftCursorProperty);
        set => SetValue(ScrubLeftCursorProperty, value);
    }

    public Cursor ScrubRightCursor
    {
        get => GetValue<Cursor>(ScrubRightCursorProperty);
        set => SetValue(ScrubRightCursorProperty, value);
    }

    public bool SnapsToSmallChange
    {
        get => GetValue<bool>(SnapsToSmallChangeProperty);
        set => SetValue(SnapsToSmallChangeProperty, value);
    }

    public bool IsReadOnly
    {
        get => GetValue<bool>(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Milliseconds a button must be held before it starts repeating.</summary>
    public int Delay
    {
        get => GetValue<int>(DelayProperty);
        set => SetValue(DelayProperty, value);
    }

    /// <summary>Milliseconds between repeats once a held button has started repeating.</summary>
    public int RepeatInterval
    {
        get => GetValue<int>(RepeatIntervalProperty);
        set => SetValue(RepeatIntervalProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        DetachParts();   // a template swap re-runs this; drop the old wiring first

        _text = GetTemplateChild("PART_TextBox") as TextBox;
        _increase = GetTemplateChild("PART_Increase") as RepeatButton;
        _decrease = GetTemplateChild("PART_Decrease") as RepeatButton;

        if (_text != null)
        {
            _text.EnterPressed += OnTextEnterPressed;
            _text.LostFocus += OnTextLostFocus;
            _text.PropertyChanged += OnTextChanged;
        }
        if (_increase != null) _increase.Click += OnIncreaseClick;
        if (_decrease != null) _decrease.Click += OnDecreaseClick;

        UpdateText();
        UpdateButtons();
    }

    /// <summary>Let the template's parts go when the template does - see ScrollBar.OnRemoveTemplate.</summary>
    public override void OnRemoveTemplate()
    {
        base.OnRemoveTemplate();
        DetachParts();
    }

    private void DetachParts()
    {
        if (_text != null)
        {
            _text.EnterPressed -= OnTextEnterPressed;
            _text.LostFocus -= OnTextLostFocus;
            _text.PropertyChanged -= OnTextChanged;
            _text = null;
        }
        if (_increase != null)
        {
            _increase.Click -= OnIncreaseClick;
            _increase = null;
        }
        if (_decrease != null)
        {
            _decrease.Click -= OnDecreaseClick;
            _decrease = null;
        }
    }

    // Null is not out of range - it is the absence of one - so it passes through untouched; a number is clamped, the
    // same rule RangeBase applies to its own Value.
    private static object CoerceValue(AdamantiumComponent d, object baseValue)
    {
        if (baseValue is not double value) return baseValue;

        var box = (NumericUpDown)d;
        if (value < box.Minimum) value = box.Minimum;
        if (value > box.Maximum) value = box.Maximum;
        return value;
    }

    private static void OnValueChanged(AdamantiumComponent d, AdamantiumPropertyChangedEventArgs e)
    {
        var box = (NumericUpDown)d;
        // The sentinel a property carries before it was ever set is not a number and not null-the-value; `as` maps both
        // to null here, which is what an empty box holds anyway.
        var oldValue = e.OldValue as double?;
        var newValue = e.NewValue as double?;

        box.UpdateText();
        box.UpdateButtons();

        if (newValue.HasValue)
        {
            if (newValue.Value <= box.Minimum) box.MinimumReached?.Invoke(box, EventArgs.Empty);
            if (newValue.Value >= box.Maximum) box.MaximumReached?.Invoke(box, EventArgs.Empty);
        }

        box.ValueChanged?.Invoke(box, new NullableValueChangedEventArgs(oldValue, newValue));
    }

    /// <summary>A limit moved: re-map the request through the coercion, so a value clamped by the old range is restored
    /// when the new one has room for it.</summary>
    protected override void ReCoerceSelection() => CoerceValue(ValueProperty);

    protected override void OnLimitsChanged()
    {
        // The value may have been re-clamped, and either end may have just become reachable or unreachable.
        UpdateText();
        UpdateButtons();
    }

    private static void OnFormatChanged(AdamantiumComponent a, AdamantiumPropertyChangedEventArgs e)
    {
        (a as NumericUpDown)?.UpdateText();
    }

    // --- Text <-> value ---------------------------------------------------------------------------------------------

    private CultureInfo EffectiveCulture => Culture ?? CultureInfo.CurrentCulture;

    /// <summary>The value as it is shown. A format holding a placeholder is a composite one ("{0:N2} kg"); anything
    /// else is a plain numeric format ("N2") - the same two shapes MahApps accepts.</summary>
    private string FormatValue(double value)
    {
        var format = StringFormat;
        if (string.IsNullOrEmpty(format)) return value.ToString(EffectiveCulture);
        return format.Contains('{')
            ? string.Format(EffectiveCulture, format, value)
            : value.ToString(format, EffectiveCulture);
    }

    private bool TryParse(string text, out double value)
        => double.TryParse(text, ParsingNumberStyle, EffectiveCulture, out value);

    private void UpdateText()
    {
        if (_text == null) return;

        var text = Value.HasValue ? FormatValue(Value.Value) : string.Empty;
        if (_text.Text == text) return;

        _writingText = true;
        try
        {
            _text.Text = text;
            // The caret indexed the number that was there BEFORE, so leaving it be strands it somewhere arbitrary
            // inside the new one - park it after the last digit, which is where anyone about to type wants it anyway.
            // This is not only about the drag: a stepped 9 -> 10 left the caret between the digits just the same.
            _text.CaretIndex = text.Length;
            _text.SelectionStart = text.Length;
            _text.SelectionLength = 0;
        }
        finally { _writingText = false; }
    }

    private void OnTextChanged(object sender, AdamantiumPropertyChangedEventArgs e)
    {
        if (e.Property != TextBoxBase.TextProperty) return;
        // Our own write, echoed back. Reading it as an edit would re-parse what we just formatted, and a format that
        // rounds ("N0" on 2.4) would quietly move the value every time it was displayed.
        if (_writingText || !ChangesValueWhileTyping || !IsManualEntryEnabled) return;

        ReadEntry();
    }

    /// <summary>Take what is typed as the value. An empty box is EMPTY, not zero - that is the whole reason the value is
    /// nullable - and anything that is not a number at all is simply not taken.</summary>
    private void ReadEntry()
    {
        if (string.IsNullOrWhiteSpace(_text.Text)) SetCurrentValue(ValueProperty, null);
        else if (TryParse(_text.Text, out var parsed)) SetValueFromInput(parsed);
    }

    private void OnTextEnterPressed(object sender, KeyEventArgs e) => CommitText();

    private void OnTextLostFocus(object sender, RoutedEventArgs e) => CommitText();

    /// <summary>Read the entry back, then write the value out again - which both re-formats what parsed and restores
    /// what did not, so the box can never be left showing something the value is not.</summary>
    private void CommitText()
    {
        if (_text == null) return;

        if (IsManualEntryEnabled && !IsReadOnly) ReadEntry();
        UpdateText();
    }

    // --- Stepping ---------------------------------------------------------------------------------------------------

    private void OnIncreaseClick(object sender, RoutedEventArgs e) => Step(+1, HeldStep());

    private void OnDecreaseClick(object sender, RoutedEventArgs e) => Step(-1, HeldStep());

    private void Step(int direction, double amount)
    {
        if (IsReadOnly) return;
        SetValueFromInput((Value ?? StartingPoint()) + direction * amount);
    }

    /// <summary>Where a step starts from when the box is empty: zero, unless the range does not reach it, in which case
    /// the end that is nearest. Stepping an empty box has to begin SOMEWHERE, and beginning outside the range would only
    /// be clamped back anyway.</summary>
    private double StartingPoint()
    {
        if (Minimum > 0) return Minimum;
        if (Maximum < 0) return Maximum;
        return 0;
    }

    /// <summary>The step for one click of a held button. Repeats arrive one <see cref="RepeatInterval"/> apart, so a
    /// click that follows closely enough is part of a hold: after a few of those the step doubles every so often, up to
    /// a cap, so crossing a big range does not need the button held for a minute - and a single click stays a step.</summary>
    private double HeldStep()
    {
        if (!IsSpeedupEnabled) return SmallChange;

        var now = Environment.TickCount64;
        var continues = now - _lastStepTime <= Math.Max(100, RepeatInterval * 3);
        _heldSteps = continues ? _heldSteps + 1 : 0;
        _lastStepTime = now;

        var doublings = Math.Min(6, Math.Max(0, _heldSteps - 10) / 15);
        return SmallChange * (1 << doublings);
    }

    private void SetValueFromInput(double value)
    {
        // SetCurrentValue, not a plain assignment: a Local write outranks a {Binding} and would freeze a data-bound
        // control for good - the source could never move it again (the same note as on Slider).
        SetCurrentValue(ValueProperty, Snap(value));
    }

    /// <summary>Rounded to a whole number of steps FROM ZERO, so 0.1-steps land on 0.3 rather than on 0.30000000000004,
    /// and so an unbounded Minimum (which the default is) cannot turn the arithmetic into an infinity.</summary>
    private double Snap(double value)
    {
        if (!SnapsToSmallChange || SmallChange <= 0) return value;
        return Math.Round(value / SmallChange, MidpointRounding.AwayFromZero) * SmallChange;
    }

    private void UpdateButtons()
    {
        // A button that cannot move the value any further says so, rather than clicking to no effect. An EMPTY box can
        // always be stepped - that is how a value gets into it - so neither button is off in that state.
        if (_increase != null) _increase.IsEnabled = !IsReadOnly && (!Value.HasValue || Value.Value < Maximum);
        if (_decrease != null) _decrease.IsEnabled = !IsReadOnly && (!Value.HasValue || Value.Value > Minimum);
    }

    // --- Input ------------------------------------------------------------------------------------------------------

    // The TUNNEL, so an arrow key is a step before the text box reads it as caret movement, which it would mark handled.
    // The wheel bubbles up from the text box, so it is taken on the way back.
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (e.Handled || IsReadOnly) return;

        switch (e.Key)
        {
            case Key.UpArrow when AreArrowKeysEnabled:
                Step(+1, SmallChange);
                break;
            case Key.DownArrow when AreArrowKeysEnabled:
                Step(-1, SmallChange);
                break;
            case Key.PageUp when AreArrowKeysEnabled:
                Step(+1, LargeChange);
                break;
            case Key.PageDown when AreArrowKeysEnabled:
                Step(-1, LargeChange);
                break;
            case Key.Escape:
                UpdateText();   // abandon the entry, go back to what the value actually is
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    /// <summary>Refuse a keystroke that could not be part of a number BEFORE the text box inserts it. The preview and
    /// the main event share one args object and a handled one skips the handlers, so marking it here is what stops the
    /// character - there is nothing to undo afterwards.</summary>
    private void OnPreviewTextInput(object sender, TextInputEventArgs e)
    {
        if (e.Handled || string.IsNullOrEmpty(e.Text)) return;

        if (!IsManualEntryEnabled || IsReadOnly)
        {
            e.Handled = true;
            return;
        }

        var numbers = EffectiveCulture.NumberFormat;
        foreach (var c in e.Text)
        {
            if (char.IsDigit(c)) continue;
            var isSeparator = InputMode == NumericInputMode.Decimals &&
                              numbers.NumberDecimalSeparator.Contains(c);
            var isSign = numbers.NegativeSign.Contains(c) || numbers.PositiveSign.Contains(c);
            var isGrouping = numbers.NumberGroupSeparator.Contains(c);
            if (isSeparator || isSign || isGrouping) continue;

            e.Handled = true;
            return;
        }
    }

    // --- Drag to scrub -----------------------------------------------------------------------------------------------

    private const double ScrubThreshold = 4;   // pixels a press may wander before it stops being a click

    private bool _scrubArmed;      // a press landed here; a sideways move may yet turn it into a scrub
    private bool _scrubbing;
    private double _scrubOriginX;  // in the ROOT's space - the one frame that cannot move under the drag
    private double _scrubFrom;
    private int _scrubDirection;   // -1 / +1: which side of the press the drag is on, so the pointer only changes on a real turn

    protected override void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(sender, e);
        // A repeat click is a text-selection gesture (word, then all) - it must not arm a scrub, or the second press of
        // a double-click would fight the selection it just made.
        if (!IsDragScrubEnabled || IsReadOnly || e.ClickCount > 1) return;

        _scrubOriginX = RootX(e);
        _scrubFrom = Value ?? StartingPoint();
        _scrubArmed = true;
    }

    protected override void OnMouseMove(object sender, MouseEventArgs e)
    {
        base.OnMouseMove(sender, e);
        if (!_scrubArmed) return;

        // Measured from the PRESS, never accumulated per event: an accumulated delta drifts, and here it would also
        // fight the editor's own idea of where the drag began.
        var travelled = RootX(e) - _scrubOriginX;
        if (!_scrubbing)
        {
            if (Math.Abs(travelled) < ScrubThreshold) return;   // still a click as far as anyone knows
            BeginScrub();
        }

        ShowScrubCursor(travelled);
        SetValueFromInput(_scrubFrom + travelled / Math.Max(1, ScrubPixelsPerStep) * ScrubStep());
    }

    protected override void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(sender, e);
        if (!_scrubArmed) return;

        _scrubArmed = false;
        if (!_scrubbing) return;   // never crossed the threshold: it was a click, and the editor already handled it

        _scrubbing = false;
        Mouse.OverrideCursor = null;
        _text?.SuppressCaret(false);
        if (IsMouseCaptured) ReleaseMouseCapture();
    }

    private void BeginScrub()
    {
        _scrubbing = true;
        // Take the press off the editor: it started a selection drag on the way here, and leaving that armed would mean
        // a drag with no end - the button-up now comes to US - and the next hover would carry on selecting.
        _text?.CancelMouseSelection();
        _text?.SuppressCaret(true);   // the box is not being typed into for as long as this drag lasts
        CaptureMouse();
        _scrubDirection = 0;
    }

    /// <summary>Shows the pointer for the direction the drag has gone, from the PRESS - not from the last event, which
    /// would flip the shape on every twitch of the hand. Set through the app-wide OVERRIDE rather than Mouse.Cursor: the
    /// pointer keeps entering the parts it drags across (presenter, frame, a button), and each of those sets Mouse.Cursor
    /// to its own on the way in, which wiped ours a frame after we set it.</summary>
    private void ShowScrubCursor(double travelled)
    {
        var direction = Math.Sign(travelled);
        if (direction == 0 || direction == _scrubDirection) return;

        _scrubDirection = direction;
        Mouse.OverrideCursor = (direction < 0 ? ScrubLeftCursor : ScrubRightCursor) ?? Cursors.SizeEWE;
    }

    /// <summary>What one step of the drag is worth. The modifiers are the ones every 3D tool uses: Shift for a tenth of
    /// a step, Ctrl for a whole page of them.</summary>
    private double ScrubStep()
    {
        var modifiers = Keyboard.Modifiers;
        if ((modifiers & (InputModifiers.LeftShift | InputModifiers.RightShift)) != 0) return SmallChange / 10;
        if ((modifiers & (InputModifiers.LeftControl | InputModifiers.RightControl)) != 0) return LargeChange;
        return SmallChange;
    }

    private double RootX(MouseEventArgs e)
    {
        IUIComponent root = this;
        while (root.VisualParent is { } parent) root = parent;
        return root is IInputComponent input ? e.GetPosition(input).X : e.GetPosition(this).X;
    }

    private void OnMouseWheelStep(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || IsReadOnly || !IsMouseWheelEnabled || e.IsHorizontal || e.Delta == 0) return;
        // Focus is the normal permission to take the wheel; hovering is enough only when asked for, because a control
        // that swallows the wheel it was not given stops the surface under it from scrolling.
        if (_text?.IsFocused != true && !(TracksMouseWheelWhenOver && IsMouseOver)) return;

        Step(Math.Sign(e.Delta), SmallChange);
        e.Handled = true;
    }
}
