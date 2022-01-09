namespace Adamantium.UI.Input;

/// <summary>
/// Virtual Keyboard key codes
/// </summary>
/// <remarks>
/// IME - input method editor is an operating system component or program that allows any data, such as keyboard strokes or mouse movements, to be received as input.
/// In this way users can enter characters and symbols not found on their input devices. Using an input method is obligatory for any language that has more graphemes 
/// than there are keys on the keyboard.
/// OEM - original equipment manufacturer is a company that makes a part or subsystem that is used in another company's end product
/// </remarks>
public enum Key : uint
{
   /// <summary>
   /// NUll button
   /// </summary>
   /// <remarks>Virtual key code: 0x0</remarks>
   None = 0x0,

   /// <summary>
   /// Left mouse button
   /// </summary>
   /// /// <remarks>Virtual key code: 0x01 (1)</remarks>
   LeftButton = 0x01,

   /// <summary>
   /// Right mouse button
   /// </summary>
   /// /// <remarks>Virtual key code: 0x02 (2)</remarks>
   RightButton = 0x02,

   /// <summary>
   /// Control-break processing
   /// </summary>
   /// <remarks>Virtual key code: 0x03 (3)</remarks>
   Cancel = 0x03,

   /// <summary>
   /// Middle mouse button (three-button mouse)
   /// </summary>
   /// <remarks>Virtual key code: 0x04 (4)</remarks>
   MiddleButton = 0x04,

   /// <summary>
   /// X1 mouse button
   /// </summary>
   /// <remarks>Virtual key code: 0x05 (5)</remarks>
   XButton1 = 0x05,

   /// <summary>
   /// X2 mouse button
   /// </summary>
   /// <remarks>Virtual key code: 0x06 (6)</remarks>
   XButton2 = 0x06,

   /// <summary>
   /// BACKSPACE key
   /// </summary>
   /// <remarks>Virtual key code: 0x08 (8)</remarks>
   BackSpace = 0x08,

   /// <summary>
   /// TAB key
   /// </summary>
   /// <remarks>Virtual key code: 0x09 (9)</remarks>
   Tab = 0x09,

   /// <summary>
   /// CLEAR key
   /// </summary>
   /// <remarks>Virtual key code: 0x0С (12)</remarks>
   Clear = 0x0C,

   /// <summary>
   /// ENTER key
   /// </summary>
   /// <remarks>Virtual key code: 0x0D (13)</remarks>
   Enter = 0x0D,

   /// <summary>
   /// SHIFT key
   /// </summary>
   /// <remarks>Virtual key code: 0x10 (16)</remarks>
   Shift = 0x10,

   /// <summary>
   /// CTRL key
   /// </summary>
   /// <remarks>Virtual key code: 0x11 (17)</remarks>
   Ctrl = 0x11,

   /// <summary>
   /// ALT key
   /// </summary>
   /// <remarks>Virtual key code: 0x12 (18)</remarks>
   Alt = 0x12,

   /// <summary>
   /// PAUSE key
   /// </summary>
   /// <remarks>Virtual key code: 0x13 (19)</remarks>
   Pause = 0x13,

   /// <summary>
   /// CAPS LOCK key
   /// </summary>
   /// <remarks>Virtual key code: 0x14 (20)</remarks>
   CapsLock = 0x14,

   /// <summary>
   /// IME Kana mode
   /// </summary>
   /// <remarks>Virtual key code: 0x15 (21)</remarks>
   IMEKana = 0x15,

   /// <summary>
   /// IME Hanguel mode
   /// </summary>
   /// <remarks>Virtual key code: 0x15 (21)</remarks>
   IMEHanguel = 0x15,

   /// <summary>
   /// IME Junja mode
   /// </summary>
   /// <remarks>Virtual key code: 0x17 (23)</remarks>
   IMEJunja = 0x17,

   /// <summary>
   /// IME Kanji mode
   /// </summary>
   /// <remarks>Virtual key code: 0x19 (25)</remarks>
   IMEKanji = 0x19,

   /// <summary>
   /// Esc key
   /// </summary>
   /// <remarks>Virtual key code: 0x1B (27)</remarks>
   Escape = 0x1B,

   /// <summary>
   /// IME convert
   /// </summary>
   /// <remarks>Virtual key code: 0x1C (28)</remarks>
   IMEConvert = 0x1C,

   /// <summary>
   /// IME nonconvert
   /// </summary>
   /// <remarks>Virtual key code: 0x1D (29)</remarks>
   IMENonconvert = 0x1D,

   /// <summary>
   /// IME accept
   /// </summary>
   /// <remarks>Virtual key code: 0x1E (30)</remarks>
   IMEAccept = 0x1E,

   /// <summary>
   /// IME mode change request
   /// </summary>
   /// <remarks>Virtual key code: 0x1F (31)</remarks>
   IMEModeChange = 0x1F,

   /// <summary>
   /// SPACEBAR
   /// </summary>
   /// <remarks>Virtual key code: 0x20 (32)</remarks>
   Space = 0x20,

   /// <summary>
   /// PAGE UP key
   /// </summary>
   /// <remarks>Virtual key code: 0x21 (33)</remarks>
   PageUp = 0x21,

   /// <summary>
   /// PAGE DOWN key
   /// </summary>
   /// <remarks>Virtual key code: 0x22 (34)</remarks>
   PageDown = 0x22,

   /// <summary>
   /// END key
   /// </summary>
   /// <remarks>Virtual key code: 0x23 (35)</remarks>
   End = 0x23,

   /// <summary>
   /// HOME key
   /// </summary>
   /// <remarks>Virtual key code: 0x24 (36)</remarks>
   Home = 0x24,

   /// <summary>
   /// LEFT ARROW key
   /// </summary>
   /// <remarks>Virtual key code: 0x25 (37)</remarks>
   LeftArrow = 0x25,

   /// <summary>
   /// UP ARROW key
   /// </summary>
   /// <remarks>Virtual key code: 0x26 (38)</remarks>
   UpArrow = 0x26,

   /// <summary>
   /// RIGHT ARROW key
   /// </summary>
   /// <remarks>Virtual key code: 0x27 (39)</remarks>
   RightArrow = 0x27,

   /// <summary>
   /// DOWN ARROW key
   /// </summary>
   /// <remarks>Virtual key code: 0x28 (40)</remarks>
   DownArrow = 0x28,

   /// <summary>
   /// SELECT key
   /// </summary>
   /// <remarks>Virtual key code: 0x29 (41)</remarks>
   Select = 0x29,

   /// <summary>
   /// PRINT key
   /// </summary>
   /// <remarks>Virtual key code: 0x2A (42)</remarks>
   Print = 0x2A,

   /// <summary>
   /// EXECUTE key
   /// </summary>
   /// <remarks>Virtual key code: 0x2B (43)</remarks>
   Execute = 0x2B,

   /// <summary>
   /// PRINT SCREEN key
   /// </summary>
   /// <remarks>Virtual key code: 0x2C (44)</remarks>
   PrintScreen = 0x2C,

   /// <summary>
   /// Insert key
   /// </summary>
   /// <remarks>Virtual key code: 0x2D (45)</remarks>
   Insert = 0x2D,

   /// <summary>
   /// Delete key
   /// </summary>
   /// <remarks>Virtual key code: 0x2E (46)</remarks>
   Delete = 0x2E,

   /// <summary>
   /// HELP key
   /// </summary>
   /// <remarks>Virtual key code: 0x2F (47)</remarks>
   Help = 0x2F,

   /// <summary>
   /// 0 key
   /// </summary>
   /// <remarks>Virtual key code: 0x30 (48)</remarks>
   D0 = 0x30,

   /// <summary>
   /// 1 key
   /// </summary>
   /// <remarks>Virtual key code: 0x31 (49)</remarks>
   D1 = 0x31,

   /// <summary>
   /// 2 key
   /// </summary>
   /// <remarks>Virtual key code: 0x32 (50)</remarks>
   D2 = 0x32,

   /// <summary>
   /// 3 key
   /// </summary>
   /// <remarks>Virtual key code: 0x33 (51)</remarks>
   D3 = 0x33,

   /// <summary>
   /// 4 key
   /// </summary>
   /// <remarks>Virtual key code: 0x34 (52)</remarks>
   D4 = 0x34,

   /// <summary>
   /// 5 key
   /// </summary>
   /// <remarks>Virtual key code: 0x35 (53)</remarks>
   D5 = 0x35,

   /// <summary>
   /// 6 key
   /// </summary>
   /// <remarks>Virtual key code: 0x36 (54)</remarks>
   D6 = 0x36,

   /// <summary>
   /// 7 key
   /// </summary>
   /// <remarks>Virtual key code: 0x37 (55)</remarks>
   D7 = 0x37,

   /// <summary>
   /// 8 key
   /// </summary>
   /// <remarks>Virtual key code: 0x38 (56)</remarks>
   D8 = 0x38,

   /// <summary>
   /// 9 key
   /// </summary>
   /// <remarks>Virtual key code: 0x39 (57)</remarks>
   D9 = 0x39,

   /// <summary>
   /// A key
   /// </summary>
   /// <remarks>Virtual key code: 0x41 (65)</remarks>
   A = 0x41,

   /// <summary>
   /// B key
   /// </summary>
   /// <remarks>Virtual key code: 0x42 (66)</remarks>
   B = 0x42,

   /// <summary>
   /// C key
   /// </summary>
   /// <remarks>Virtual key code: 0x43 (67)</remarks>
   C = 0x43,

   /// <summary>
   /// D key
   /// </summary>
   /// <remarks>Virtual key code: 0x44 (68)</remarks>
   D = 0x44,

   /// <summary>
   /// E key
   /// </summary>
   /// <remarks>Virtual key code: 0x45 (69)</remarks>
   E = 0x45,

   /// <summary>
   /// F key
   /// </summary>
   /// <remarks>Virtual key code: 0x46 (70)</remarks>
   F = 0x46,

   /// <summary>
   /// G key
   /// </summary>
   /// <remarks>Virtual key code: 0x47 (71)</remarks>
   G = 0x47,

   /// <summary>
   /// H key
   /// </summary>
   /// <remarks>Virtual key code: 0x48 (72)</remarks>
   H = 0x48,

   /// <summary>
   /// I key
   /// </summary>
   /// <remarks>Virtual key code: 0x49 (73)</remarks>
   I = 0x49,

   /// <summary>
   /// J key
   /// </summary>
   /// <remarks>Virtual key code: 0x4A (74)</remarks>
   J = 0x4A,

   /// <summary>
   /// K key
   /// </summary>
   /// <remarks>Virtual key code: 0x4B (75)</remarks>
   K = 0x4B,

   /// <summary>
   /// L key
   /// </summary>
   /// <remarks>Virtual key code: 0x4C (76)</remarks>
   L = 0x4C,

   /// <summary>
   /// M key
   /// </summary>
   /// <remarks>Virtual key code: 0x4D (77)</remarks>
   M = 0x4D,

   /// <summary>
   /// N key
   /// </summary>
   /// <remarks>Virtual key code: 0x4E (78)</remarks>
   N = 0x4E,

   /// <summary>
   /// O key
   /// </summary>
   /// <remarks>Virtual key code: 0x4F (79)</remarks>
   O = 0x4F,

   /// <summary>
   /// P key
   /// </summary>
   /// <remarks>Virtual key code: 0x50 (80)</remarks>
   P = 0x50,

   /// <summary>
   /// Q key
   /// </summary>
   /// <remarks>Virtual key code: 0x51 (81)</remarks>
   Q = 0x51,

   /// <summary>
   /// R key
   /// </summary>
   /// <remarks>Virtual key code: 0x52 (82)</remarks>
   R = 0x52,

   /// <summary>
   /// S key
   /// </summary>
   /// <remarks>Virtual key code: 0x53 (83)</remarks>
   S = 0x53,

   /// <summary>
   /// T key
   /// </summary>
   /// <remarks>Virtual key code: 0x54 (84)</remarks>
   T = 0x54,

   /// <summary>
   /// U key
   /// </summary>
   /// <remarks>Virtual key code: 0x55 (85)</remarks>
   U = 0x55,

   /// <summary>
   /// V key
   /// </summary>
   /// <remarks>Virtual key code: 0x56 (86)</remarks>
   V = 0x56,

   /// <summary>
   /// W key
   /// </summary>
   /// <remarks>Virtual key code: 0x57 (87)</remarks>
   W = 0x57,

   /// <summary>
   /// X key
   /// </summary>
   /// <remarks>Virtual key code: 0x58 (88)</remarks>
   X = 0x58,

   /// <summary>
   /// Y key
   /// </summary>
   /// <remarks>Virtual key code: 0x59 (89)</remarks>
   Y = 0x59,

   /// <summary>
   /// Z key
   /// </summary>
   /// <remarks>Virtual key code: 0x5A (90)</remarks>
   Z = 0x5A,

   /// <summary>
   /// Left Windows key (Natural keyboard)
   /// </summary>
   /// <remarks>Virtual key code: 0x5B (91)</remarks>
   LeftWin = 0x5B,

   /// <summary>
   /// Right Windows key (Natural keyboard)
   /// </summary>
   /// <remarks>Virtual key code: 0x5C (92)</remarks>
   RightWin = 0x5C,

   /// <summary>
   /// Applications key (Natural keyboard)
   /// </summary>
   /// <remarks>Virtual key code: 0x5D (93)</remarks>
   Apps = 0x5D,

   /// <summary>
   /// Computer Sleep key
   /// </summary>
   /// <remarks>Virtual key code: 0x5F (95)</remarks>
   Sleep = 0x5F,

   /// <summary>
   /// Numeric keypad 0 key
   /// </summary>
   /// <remarks>Virtual key code: 0x60 (96)</remarks>
   NumPad0 = 0x60,

   /// <summary>
   /// Numeric keypad 1 key
   /// </summary>
   /// <remarks>Virtual key code: 0x61 (97)</remarks>
   NumPad1 = 0x61,

   /// <summary>
   /// Numeric keypad 2 key
   /// </summary>
   /// <remarks>Virtual key code: 0x62 (98)</remarks>
   NumPad2 = 0x62,

   /// <summary>
   /// Numeric keypad 3 key
   /// </summary>
   /// <remarks>Virtual key code: 0x63 (99)</remarks>
   NumPad3 = 0x63,

   /// <summary>
   /// Numeric keypad 4 key
   /// </summary>
   /// <remarks>Virtual key code: 0x64 (100)</remarks>
   NumPad4 = 0x64,

   /// <summary>
   /// Numeric keypad 5 key
   /// </summary>
   /// <remarks>Virtual key code: 0x65 (101)</remarks>
   NumPad5 = 0x65,

   /// <summary>
   /// Numeric keypad 6 key
   /// </summary>
   /// <remarks>Virtual key code: 0x66 (102)</remarks>
   NumPad6 = 0x66,

   /// <summary>
   /// Numeric keypad 7 key
   /// </summary>
   /// <remarks>Virtual key code: 0x67 (103)</remarks>
   NumPad7 = 0x67,

   /// <summary>
   /// Numeric keypad 8 key
   /// </summary>
   /// <remarks>Virtual key code: 0x68 (104)</remarks>
   NumPad8 = 0x68,

   /// <summary>
   /// Numeric keypad 9 key
   /// </summary>
   /// <remarks>Virtual key code: 0x69 (105)</remarks>
   NumPad9 = 0x69,

   /// <summary>
   /// Numpad Multiply key
   /// </summary>
   /// <remarks>Virtual key code: 0x6A (106)</remarks>
   NumPadMultiply = 0x6A,

   /// <summary>
   /// Numpad Add key
   /// </summary>
   /// <remarks>Virtual key code: 0x6B (107)</remarks>
   NumPadAdd = 0x6B,

   /// <summary>
   /// Separator key
   /// </summary>
   /// <remarks>Virtual key code: 0x6C (108)</remarks>
   Separator = 0x6C,

   /// <summary>
   /// Numpad Subtract key
   /// </summary>
   /// <remarks>Virtual key code: 0x6D (109)</remarks>
   NumPadSubtract = 0x6D,

   /// <summary>
   /// Numpad Decimal key
   /// </summary>
   /// <remarks>Virtual key code: 0x6E (110)</remarks>
   NumPadDecimal = 0x6E,

   /// <summary>
   /// Numpad Divide key
   /// </summary>
   /// <remarks>Virtual key code: 0x6F (111)</remarks>
   NumPadDivide = 0x6F,

   /// <summary>
   /// F1 key
   /// </summary>
   /// <remarks>Virtual key code: 0x70 (112)</remarks>
   F1 = 0x70,

   /// <summary>
   /// F2 key
   /// </summary>
   /// <remarks>Virtual key code: 0x71 (113)</remarks>
   F2 = 0x71,

   /// <summary>
   /// F3 key
   /// </summary>
   /// <remarks>Virtual key code: 0x72 (114)</remarks>
   F3 = 0x72,

   /// <summary>
   /// F4 key
   /// </summary>
   /// <remarks>Virtual key code: 0x73 (115)</remarks>
   F4 = 0x73,

   /// <summary>
   /// F5 key
   /// </summary>
   /// <remarks>Virtual key code: 0x74 (116)</remarks>
   F5 = 0x74,

   /// <summary>
   /// F6 key
   /// </summary>
   /// <remarks>Virtual key code: 0x75 (117)</remarks>
   F6 = 0x75,

   /// <summary>
   /// F7 key
   /// </summary>
   /// <remarks>Virtual key code: 0x76 (118)</remarks>
   F7 = 0x76,

   /// <summary>
   /// F8 key
   /// </summary>
   /// <remarks>Virtual key code: 0x77 (119)</remarks>
   F8 = 0x77,

   /// <summary>
   /// F9 key
   /// </summary>
   /// <remarks>Virtual key code: 0x78 (120)</remarks>
   F9 = 0x78,

   /// <summary>
   /// F10 key
   /// </summary>
   /// <remarks>Virtual key code: 0x79 (121)</remarks>
   F10 = 0x79,

   /// <summary>
   /// F11 key
   /// </summary>
   /// <remarks>Virtual key code: 0x7A (122)</remarks>
   F11 = 0x7A,

   /// <summary>
   /// F12 key
   /// </summary>
   /// <remarks>Virtual key code: 0x7B (123)</remarks>
   F12 = 0x7B,

   /// <summary>
   /// F13 key
   /// </summary>
   /// <remarks>Virtual key code: 0x7C (124)</remarks>
   F13 = 0x7C,

   /// <summary>
   /// F14 key
   /// </summary>
   /// <remarks>Virtual key code: 0x7D (125)</remarks>
   F14 = 0x7D,

   /// <summary>
   /// F15 key
   /// </summary>
   /// <remarks>Virtual key code: 0x7E (126)</remarks>
   F15 = 0x7E,

   /// <summary>
   /// F16 key
   /// </summary>
   /// <remarks>Virtual key code: 0x7F (127)</remarks>
   F16 = 0x7F,

   /// <summary>
   /// F17 key
   /// </summary>
   /// <remarks>Virtual key code: 0x80 (128)</remarks>
   F17 = 0x80,

   /// <summary>
   /// F18 key
   /// </summary>
   /// <remarks>Virtual key code: 0x81 (129)</remarks>
   F18 = 0x81,

   /// <summary>
   /// F19 key
   /// </summary>
   /// <remarks>Virtual key code: 0x82 (130)</remarks>
   F19 = 0x82,

   /// <summary>
   /// F20 key
   /// </summary>
   /// <remarks>Virtual key code: 0x83 (131)</remarks>
   F20 = 0x83,

   /// <summary>
   /// F21 key
   /// </summary>
   /// <remarks>Virtual key code: 0x84 (132)</remarks>
   F21 = 0x84,

   /// <summary>
   /// F22 key
   /// </summary>
   /// <remarks>Virtual key code: 0x85 (133)</remarks>
   F22 = 0x85,

   /// <summary>
   /// F23 key
   /// </summary>
   /// <remarks>Virtual key code: 0x86 (134)</remarks>
   F23 = 0x86,

   /// <summary>
   /// F24 key
   /// </summary>
   /// <remarks>Virtual key code: 0x87 (135)</remarks>
   F24 = 0x87,

   /// <summary>
   /// NUM LOCK key
   /// </summary>
   /// <remarks>Virtual key code: 0x90 (144)</remarks>
   NumLock = 0x90,

   /// <summary>
   /// SCROLL LOCK key
   /// </summary>
   /// <remarks>Virtual key code: 0x91 (145)</remarks>
   ScrollLock = 0x91,

   /// <summary>
   /// Left SHIFT key
   /// </summary>
   /// <remarks>Virtual key code: 0xA0 (160)</remarks>
   LeftShift = 0xA0,

   /// <summary>
   /// Right SHIFT key
   /// </summary>
   /// <remarks>Virtual key code: 0xA1 (161)</remarks>
   RightShift = 0xA1,

   /// <summary>
   /// Left CONTROL key
   /// </summary>
   /// <remarks>Virtual key code: 0xA2 (162)</remarks>
   LeftCtrl = 0xA2,

   /// <summary>
   /// Right CONTROL key
   /// </summary>
   /// <remarks>Virtual key code: 0xA3 (163)</remarks>
   RightCtrl = 0xA3,

   /// <summary>
   /// Left Alt key
   /// </summary>
   /// <remarks>Virtual key code: 0xA4 (164)</remarks>
   LeftAlt = 0xA4,

   /// <summary>
   /// Right Alt key
   /// </summary>
   /// <remarks>Virtual key code: 0xA5 (165)</remarks>
   RightAlt = 0xA5,

   /// <summary>
   /// Browser Back key
   /// </summary>
   /// <remarks>Virtual key code: 0xA6 (166)</remarks>
   BrowserBackward = 0xA6,

   /// <summary>
   /// Browser Forward key
   /// </summary>
   /// <remarks>Virtual key code: 0xA7 (167)</remarks>
   BrowserForward = 0xA7,

   /// <summary>
   /// Browser Refresh key
   /// </summary>
   /// <remarks>Virtual key code: 0xA8 (168)</remarks>
   BrowserRefresh = 0xA8,

   /// <summary>
   /// Browser Stop key
   /// </summary>
   /// <remarks>Virtual key code: 0xA9 (169)</remarks>
   BrowserStop = 0xA9,

   /// <summary>
   /// Browser Search key
   /// </summary>
   /// <remarks>Virtual key code: 0xAA (170)</remarks>
   BrowserSearch = 0xAA,

   /// <summary>
   /// Browser Favorites key
   /// </summary>
   /// <remarks>Virtual key code: 0xAB (171)</remarks>
   BrowserFavorites = 0xAB,

   /// <summary>
   /// Browser Start and Home key
   /// </summary>
   /// <remarks>Virtual key code: 0xAC (172)</remarks>
   BrowserHome = 0xAC,

   /// <summary>
   /// Volume Mute key
   /// </summary>
   /// <remarks>Virtual key code: 0xAD (173)</remarks>
   VolumeMute = 0xAD,

   /// <summary>
   /// Volume Down key
   /// </summary>
   /// <remarks>Virtual key code: 0xAE (174)</remarks>
   VolumeDown = 0xAE,

   /// <summary>
   /// Volume Up key
   /// </summary>
   /// <remarks>Virtual key code: 0xAF (175)</remarks>
   VolumeUp = 0xAF,

   /// <summary>
   /// Next Track key
   /// </summary>
   /// <remarks>Virtual key code: 0xB0 (176)</remarks>
   NextTrack = 0xB0,

   /// <summary>
   /// Previous Track key
   /// </summary>
   /// <remarks>Virtual key code: 0xB1 (177)</remarks>
   PrevTrack = 0xB1,

   /// <summary>
   /// Stop Media key
   /// </summary>
   /// <remarks>Virtual key code: 0xB2 (178)</remarks>
   StopMedia = 0xB2,

   /// <summary>
   /// Play/Pause Media key
   /// </summary>
   /// <remarks>Virtual key code: 0xB3 (179)</remarks>
   PlayPauseMedia = 0xB3,

   /// <summary>
   /// Start Mail key
   /// </summary>
   /// <remarks>Virtual key code: 0xB4 (180)</remarks>
   LaunchMail = 0xB4,

   /// <summary>
   /// Select Media key
   /// </summary>
   /// <remarks>Virtual key code: 0xB5 (181)</remarks>
   LaunchMediaSelect = 0xB5,

   /// <summary>
   /// Start Application 1 key
   /// </summary>
   /// <remarks>Virtual key code: 0xB6 (182)</remarks>
   LaunchApp1 = 0xB6,

   /// <summary>
   /// Start Application 2 key
   /// </summary>
   /// <remarks>Virtual key code: 0xB7 (183)</remarks>
   LaunchApp2 = 0xB7,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the ';:' key
   /// </summary>
   /// <remarks>Virtual key code: 0xBA (186)</remarks>
   OemSemicolon = 0xBA,

   /// <summary>
   /// For any country/region, the '+' key
   /// </summary>
   /// <remarks>Virtual key code: 0xBB (187)</remarks>
   OemPlus = 0xBB,

   /// <summary>
   /// For any country/region, the ',' key
   /// </summary>
   /// <remarks>Virtual key code: 0xBC (188)</remarks>
   OemComma = 0xBC,

   /// <summary>
   /// For any country/region, the '-' key
   /// </summary>
   /// <remarks>Virtual key code: 0xBD (189)</remarks>
   OemMinus = 0xBD,

   /// <summary>
   /// For any country/region, the '.' key
   /// </summary>
   /// <remarks>Virtual key code: 0xBE (190)</remarks>
   OemPeriod = 0xBE,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the '/?' key
   /// </summary>
   /// <remarks>Virtual key code: 0xBF (191)</remarks>
   OemQuestion = 0xBF,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the '`~' key
   /// </summary>
   /// <remarks>Virtual key code: 0xC0 (192)</remarks>
   OemTilde = 0xC0,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the '[{' key
   /// </summary>
   /// <remarks>Virtual key code: 0xDB (219)</remarks>
   OemOpenBrackets = 0xDB,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the '\|' key
   /// </summary>
   /// <remarks>Virtual key code: 0xDC (220)</remarks>
   OemPipe = 0xDC,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the ']}' key
   /// </summary>
   /// <remarks>Virtual key code: 0xDD (221)</remarks>
   OemCloseBrackets = 0xDD,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// For the US standard keyboard, the 'single-quote/double-quote' key
   /// </summary>
   /// <remarks>Virtual key code: 0xDE (222)</remarks>
   OemQuotes = 0xDE,

   /// <summary>
   /// Used for miscellaneous characters; it can vary by keyboard.
   /// </summary>
   /// <remarks>Virtual key code: 0xDF (223)</remarks>
   Oem8 = 0xDF,

   /// <summary>
   /// Either the angle bracket key or the backslash key on the RT 102-key keyboard
   /// </summary>
   /// <remarks>Virtual key code: 0xE2 (226)</remarks>
   OemBackSlash = 0xE2,

   /// <summary>
   /// IME PROCESS key
   /// </summary>
   /// <remarks>Virtual key code: 0xE5 (229)</remarks>
   ProcessKey = 0xE5,

   /// <summary>
   /// 
   /// </summary>
   /// <remarks>Virtual key code: 0xE6 (230)</remarks>
   ICOClear = 0xE6,

   /// <summary>
   /// Windows 2000: Used to pass Unicode characters as if they were keystrokes. The VK_PACKET key is the low word of a 32-bit Virtual Key value 
   /// used for non-keyboard input methods. For more information, see Remark in KEYBDINPUT, SendInput, WM_KEYDOWN, and WM_KEYUP
   /// </summary>
   /// <remarks>Virtual key code: 0xE7 (231)</remarks>
   Packet = 0xE7,

   /// <summary></summary>
   /// <remarks>Virtual key code: 0xE9 (233)</remarks>
   OemReset = 0xE9,

   /// <summary></summary>
   /// <remarks>Virtual key code: 0xEA (234)</remarks>
   OemJump = 0xEA,

   /// <summary>
   /// OEM PA1 key.
   /// </summary>
   /// <remarks>Virtual key code: 0xEB (235)</remarks>
   OemPA1 = 0xEB,

   /// <summary>
   /// OEM PA2 key.
   /// </summary>
   /// <remarks>Virtual key code: 0xEC (236)</remarks>
   OemPA2 = 0xEC,

   /// <summary>
   /// OEM PA3 key.
   /// </summary>
   /// <remarks>Virtual key code: 0xED (237)</remarks>
   OemPA3 = 0xED,

   /// <summary></summary>
   /// <remarks>Virtual key code: 0xEE (238)</remarks>
   OemWSCtrl = 0xEE,

   /// <summary></summary>
   /// <remarks>Virtual key code: 0xEF (239)</remarks>
   OemCUSel = 0xEF,

   /// <summary>
   /// The OEM ATTN key.
   /// </summary>
   /// <remarks>Virtual key code: 0xF0 (240)</remarks>
   OemAttn = 0xF0,

   /// <summary>
   /// The OEM FINISH key.
   /// </summary>
   /// <remarks>Virtual key code: 0xF1 (241)</remarks>
   OemFinish = 0xF1,

   /// <summary>
   /// The OEM COPY key.
   /// </summary>
   /// <remarks>Virtual key code: 0xF2 (242)</remarks>
   OemCopy = 0xF2,

   /// <summary>
   /// The OEM AUTO key.
   /// </summary>
   /// <remarks>Virtual key code: 0xF3 (243)</remarks>
   OemAuto = 0xF3,

   /// <summary>
   /// The OEM ENLW key.
   /// </summary>
   /// <remarks>Virtual key code: 0xF4 (244)</remarks>
   OemEnlW = 0xF4,

   /// <summary>
   /// BackTab key
   /// </summary>
   /// <remarks>Virtual key code: 0xF5 (245)</remarks>
   OemBackTab = 0xF5,

   /// <summary>
   /// Attn key
   /// </summary>
   /// <remarks>Virtual key code: 0xF6 (246)</remarks>
   Attn = 0xF6,

   /// <summary>
   /// CrSel key
   /// </summary>
   /// <remarks>Virtual key code: 0xF7 (247)</remarks>
   CrSel = 0xF7,

   /// <summary>
   /// ExSel key
   /// </summary>
   /// <remarks>Virtual key code: 0xF8 (248)</remarks>
   ExSel = 0xF8,

   /// <summary>
   /// Erase EOF key
   /// </summary>
   /// <remarks>Virtual key code: 0xF9 (249)</remarks>
   EraseEof = 0xF9,

   /// <summary>
   /// Play key
   /// </summary>
   /// <remarks>Virtual key code: 0xFA (250)</remarks>
   Play = 0xFA,

   /// <summary>
   /// Zoom key
   /// </summary>
   /// <remarks>Virtual key code: 0xFB (251)</remarks>
   Zoom = 0xFB,

   /// <summary>
   /// Reserved key
   /// </summary>
   /// <remarks>Virtual key code: 0xFC (252)</remarks>
   NoName = 0xFC,

   /// <summary>
   /// PA1 key
   /// </summary>
   /// <remarks>Virtual key code: 0xFD (253)</remarks>
   PA1 = 0xFD,

   /// <summary>
   /// Clear key
   /// </summary>
   /// <remarks>Virtual key code: 0xFE (254)</remarks>
   OemClear = 0xFE

}