namespace Adamantium.Multiverse.Input;

/// <summary>
/// A key by its place on the keyboard, not by what it types: <see cref="W"/> is the key where W is on a US keyboard, the
/// same key whatever layout is on. Values are USB HID usages (usage page shifted left 16, then the usage id), so they
/// mean the same on every platform. What the key types is <see cref="InputWormhole.Text"/>.
/// </summary>
public enum Keys : uint
{
    None = 0,

    A = 0x0007_0004,
    B = 0x0007_0005,
    C = 0x0007_0006,
    D = 0x0007_0007,
    E = 0x0007_0008,
    F = 0x0007_0009,
    G = 0x0007_000A,
    H = 0x0007_000B,
    I = 0x0007_000C,
    J = 0x0007_000D,
    K = 0x0007_000E,
    L = 0x0007_000F,
    M = 0x0007_0010,
    N = 0x0007_0011,
    O = 0x0007_0012,
    P = 0x0007_0013,
    Q = 0x0007_0014,
    R = 0x0007_0015,
    S = 0x0007_0016,
    T = 0x0007_0017,
    U = 0x0007_0018,
    V = 0x0007_0019,
    W = 0x0007_001A,
    X = 0x0007_001B,
    Y = 0x0007_001C,
    Z = 0x0007_001D,

    Digit1 = 0x0007_001E,
    Digit2 = 0x0007_001F,
    Digit3 = 0x0007_0020,
    Digit4 = 0x0007_0021,
    Digit5 = 0x0007_0022,
    Digit6 = 0x0007_0023,
    Digit7 = 0x0007_0024,
    Digit8 = 0x0007_0025,
    Digit9 = 0x0007_0026,
    Digit0 = 0x0007_0027,

    Enter = 0x0007_0028,
    Escape = 0x0007_0029,

    /// <summary>Backspace.</summary>
    Back = 0x0007_002A,

    Tab = 0x0007_002B,
    Space = 0x0007_002C,
    OemMinus = 0x0007_002D,

    /// <summary>The = + key.</summary>
    OemPlus = 0x0007_002E,

    OemOpenBrackets = 0x0007_002F,
    OemCloseBrackets = 0x0007_0030,

    /// <summary>The \ | key of a US keyboard.</summary>
    OemPipe = 0x0007_0031,

    /// <summary>The # ~ key next to Enter on an ISO keyboard.</summary>
    NonUsHash = 0x0007_0032,

    OemSemicolon = 0x0007_0033,
    OemQuotes = 0x0007_0034,

    /// <summary>The ` ~ key left of 1.</summary>
    OemTilde = 0x0007_0035,

    OemComma = 0x0007_0036,
    OemPeriod = 0x0007_0037,

    /// <summary>The / ? key.</summary>
    OemQuestion = 0x0007_0038,

    CapsLock = 0x0007_0039,

    F1 = 0x0007_003A,
    F2 = 0x0007_003B,
    F3 = 0x0007_003C,
    F4 = 0x0007_003D,
    F5 = 0x0007_003E,
    F6 = 0x0007_003F,
    F7 = 0x0007_0040,
    F8 = 0x0007_0041,
    F9 = 0x0007_0042,
    F10 = 0x0007_0043,
    F11 = 0x0007_0044,
    F12 = 0x0007_0045,

    PrintScreen = 0x0007_0046,
    ScrollLock = 0x0007_0047,
    Pause = 0x0007_0048,
    Insert = 0x0007_0049,
    Home = 0x0007_004A,
    PageUp = 0x0007_004B,
    Delete = 0x0007_004C,
    End = 0x0007_004D,
    PageDown = 0x0007_004E,
    RightArrow = 0x0007_004F,
    LeftArrow = 0x0007_0050,
    DownArrow = 0x0007_0051,
    UpArrow = 0x0007_0052,

    NumLock = 0x0007_0053,
    Divide = 0x0007_0054,
    Multiply = 0x0007_0055,
    Subtract = 0x0007_0056,
    Add = 0x0007_0057,
    NumPadEnter = 0x0007_0058,
    NumPad1 = 0x0007_0059,
    NumPad2 = 0x0007_005A,
    NumPad3 = 0x0007_005B,
    NumPad4 = 0x0007_005C,
    NumPad5 = 0x0007_005D,
    NumPad6 = 0x0007_005E,
    NumPad7 = 0x0007_005F,
    NumPad8 = 0x0007_0060,
    NumPad9 = 0x0007_0061,
    NumPad0 = 0x0007_0062,

    /// <summary>The keypad's decimal point.</summary>
    Decimal = 0x0007_0063,

    /// <summary>The \ | key between left Shift and Z on an ISO keyboard.</summary>
    OemBackslash = 0x0007_0064,

    /// <summary>The context menu key.</summary>
    Apps = 0x0007_0065,

    NumPadEquals = 0x0007_0067,

    F13 = 0x0007_0068,
    F14 = 0x0007_0069,
    F15 = 0x0007_006A,
    F16 = 0x0007_006B,
    F17 = 0x0007_006C,
    F18 = 0x0007_006D,
    F19 = 0x0007_006E,
    F20 = 0x0007_006F,
    F21 = 0x0007_0070,
    F22 = 0x0007_0071,
    F23 = 0x0007_0072,
    F24 = 0x0007_0073,

    Execute = 0x0007_0074,
    Help = 0x0007_0075,
    Select = 0x0007_0077,

    /// <summary>The keypad's comma.</summary>
    Separator = 0x0007_0085,

    /// <summary>Katakana/Hiragana on a Japanese keyboard.</summary>
    Kana = 0x0007_0088,

    /// <summary>Henkan on a Japanese keyboard.</summary>
    ImeConvert = 0x0007_008A,

    /// <summary>Muhenkan on a Japanese keyboard.</summary>
    ImeNonConvert = 0x0007_008B,

    /// <summary>Hanja on a Korean keyboard.</summary>
    Kanji = 0x0007_0091,

    OemClear = 0x0007_009C,
    Crsel = 0x0007_00A3,
    Exsel = 0x0007_00A4,

    LeftControl = 0x0007_00E0,
    LeftShift = 0x0007_00E1,
    LeftAlt = 0x0007_00E2,
    LeftWindows = 0x0007_00E3,
    RightControl = 0x0007_00E4,
    RightShift = 0x0007_00E5,
    RightAlt = 0x0007_00E6,
    RightWindows = 0x0007_00E7,

    Sleep = 0x0001_0082,

    MediaNextTrack = 0x000C_00B5,
    MediaPreviousTrack = 0x000C_00B6,
    MediaStop = 0x000C_00B7,
    MediaPlayPause = 0x000C_00CD,
    VolumeMute = 0x000C_00E2,
    VolumeUp = 0x000C_00E9,
    VolumeDown = 0x000C_00EA,
    SelectMedia = 0x000C_0183,
    LaunchMail = 0x000C_018A,

    /// <summary>The calculator key.</summary>
    LaunchApplication2 = 0x000C_0192,

    /// <summary>The "my computer" key.</summary>
    LaunchApplication1 = 0x000C_0194,

    BrowserSearch = 0x000C_0221,
    BrowserHome = 0x000C_0223,
    BrowserBack = 0x000C_0224,
    BrowserForward = 0x000C_0225,
    BrowserStop = 0x000C_0226,
    BrowserRefresh = 0x000C_0227,
    BrowserFavorites = 0x000C_022A,

    /// <summary>Either Shift: down when the left or the right one is.</summary>
    Shift = 0xFFFF_0001,

    /// <summary>Either Control: down when the left or the right one is.</summary>
    Control = 0xFFFF_0002,

    /// <summary>Either Alt: down when the left or the right one is.</summary>
    Alt = 0xFFFF_0003
}
