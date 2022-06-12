namespace Adamantium.Game.Core.Input
{
   /// <summary>
   /// Supported Keys enum for Adamantium engine
   /// </summary>
   /// <remarks>
   /// IME - input method editor is an operating system component or program that allows any data, such as keyboard strokes or mouse movements, to be received as input.
   /// In this way users can enter characters and symbols not found on their input devices. Using an input method is obligatory for any language that has more graphemes 
   /// than there are keys on the keyboard.
   /// OEM - original equipment manufacturer is a company that makes a part or subsystem that is used in another company's end product
   /// </remarks>
   public enum Keys:byte
   {
      /// <summary>
      /// Null button
      /// </summary>
      /// <remarks>Virtual key code: 0</remarks>
      None = 0,

      /// <summary>
      /// Backspace key
      /// </summary>
      /// <remarks>Virtual key code: 8</remarks>
      Back = 8,

      /// <summary>
      /// Tab key
      /// </summary>
      /// <remarks>Virtual key code: 9</remarks>
      Tab = 9,

      /// <summary>
      /// Enter key
      /// </summary>
      /// <remarks>Virtual key code: 13</remarks>
      Enter = 13,

      /// <summary>
      /// Shift key
      /// </summary>
      /// <remarks>Virtual key code: 16</remarks>
      Shift = 16,

      /// <summary>
      /// Control (Ctrl) key 
      /// </summary>
      /// <remarks>Virtual key code: 17</remarks>
      Control = 17,

      /// <summary>
      /// Alt key
      /// </summary>
      /// <remarks>Virtual key code: 18</remarks>
      Alt = 18,

      /// <summary>
      /// Pause key
      /// </summary>
      /// <remarks>Virtual key code: 19</remarks>
      Pause = 19,

      /// <summary>
      /// Caps Lock key
      /// </summary>
      /// <remarks>Virtual key code: 20</remarks>
      CapsLock = 20,

      /// <summary>
      /// IME Kana mode
      /// </summary>
      /// <remarks>Virtual key code: 21</remarks>
      Kana = 21,

      /// <summary>
      /// IME Junja mode
      /// </summary>
      /// <remarks>Virtual key code: 23</remarks>
      Junja = 23,

      /// <summary>
      /// IME Kanji mode
      /// </summary>
      /// <remarks>Virtual key code: 25</remarks>
      Kanji = 25,

      /// <summary>
      /// Escape key
      /// </summary>
      /// <remarks>Virtual key code: 27</remarks>
      Escape = 27,

      /// <summary>
      /// IME convert
      /// </summary>
      /// <remarks>Virtual key code: 28</remarks>
      ImeConvert = 28,

      /// <summary>
      /// IME nonconvert
      /// </summary>
      /// <remarks>Virtual key code: 29</remarks>
      ImeNonConvert = 29,

      /// <summary>
      /// IME accept
      /// </summary>
      /// <remarks>Virtual key code: 30</remarks>
      ImeAccept = 30,

      /// <summary>
      /// IME mode change request
      /// </summary>
      /// <remarks>Virtual key code: 31</remarks>
      ImeModeChange = 31,

      /// <summary>
      /// Space key
      /// </summary>
      /// <remarks>Virtual key code: 32</remarks>
      Space = 32,

      /// <summary>
      /// Page up key
      /// </summary>
      /// <remarks>Virtual key code: 33</remarks>
      PageUp = 33,

      /// <summary>
      /// Page down key
      /// </summary>
      /// <remarks>Virtual key code: 34</remarks>
      PageDown = 34,

      /// <summary>
      /// End key
      /// </summary>
      /// <remarks>Virtual key code: 35</remarks>
      End = 35,

      /// <summary>
      /// Home key
      /// </summary>
      /// <remarks>Virtual key code: 36</remarks>
      Home = 36,

      /// <summary>
      /// Left arrow key
      /// </summary>
      /// <remarks>Virtual key code: 37</remarks>
      LeftArrow = 37,

      /// <summary>
      /// Up arrow key
      /// </summary>
      /// <remarks>Virtual key code: 38</remarks>
      UpArrow = 38,

      /// <summary>
      /// Right arrow key
      /// </summary>
      /// <remarks>Virtual key code: 39</remarks>
      RightArrow = 39,

      /// <summary>
      /// Down arrow key
      /// </summary>
      /// <remarks>Virtual key code: 40</remarks>
      DownArrow = 40,

      /// <summary>
      /// Select key
      /// </summary>
      /// <remarks>Virtual key code: 41</remarks>
      Select = 41,

      /// <summary>
      /// Print key
      /// </summary>
      /// <remarks>Virtual key code: 42</remarks>
      Print = 42,

      /// <summary>
      /// Execute key
      /// </summary>
      /// <remarks>Virtual key code: 43</remarks>
      Execute = 43,

      /// <summary>
      /// Print screen key
      /// </summary>
      /// <remarks>Virtual key code: 44</remarks>
      PrintScreen = 44,

      /// <summary>
      /// Insert key
      /// </summary>
      /// <remarks>Virtual key code: 45</remarks>
      Insert = 45,

      /// <summary>
      /// Delete key
      /// </summary>
      /// <remarks>Virtual key code: 0</remarks>
      Delete = 46,

      /// <summary>
      /// Help key
      /// </summary>
      /// <remarks>Virtual key code: 47</remarks>
      Help = 47,

      /// <summary>
      /// Digit 0 key
      /// </summary>
      /// <remarks>Virtual key code: 48</remarks>
      Digit0 = 48,

      /// <summary>
      /// Digit 1 key
      /// </summary>
      /// <remarks>Virtual key code: 49</remarks>
      Digit1 = 49,

      /// <summary>
      /// Digit 2 key
      /// </summary>
      /// <remarks>Virtual key code: 50</remarks>
      Digit2 = 50,

      /// <summary>
      /// Digit 3 key
      /// </summary>
      /// <remarks>Virtual key code: 51</remarks>
      Digit3 = 51,

      /// <summary>
      /// Digit 4 key
      /// </summary>
      /// <remarks>Virtual key code: 52</remarks>
      Digit4 = 52,

      /// <summary>
      /// Digit 5 key
      /// </summary>
      /// <remarks>Virtual key code: 53</remarks>
      Digit5 = 53,

      /// <summary>
      /// Digit 6 key
      /// </summary>
      /// <remarks>Virtual key code: 54</remarks>
      Digit6 = 54,

      /// <summary>
      /// Digit 7 key
      /// </summary>
      /// <remarks>Virtual key code: 55</remarks>
      Digit7 = 55,

      /// <summary>
      /// Digit 8 key
      /// </summary>
      /// <remarks>Virtual key code: 56</remarks>
      Digit8 = 56,

      /// <summary>
      /// Digit 9 key
      /// </summary>
      /// <remarks>Virtual key code: 57</remarks>
      Digit9 = 57,

      /// <summary>
      /// A key
      /// </summary>
      /// <remarks>Virtual key code: 65</remarks>
      A = 65,

      /// <summary>
      /// B key
      /// </summary>
      /// <remarks>Virtual key code: 66</remarks>
      B = 66,

      /// <summary>
      /// C key
      /// </summary>
      /// <remarks>Virtual key code: 67</remarks>
      C = 67,

      /// <summary>
      /// D key
      /// </summary>
      /// <remarks>Virtual key code: 68</remarks>
      D = 68,

      /// <summary>
      /// E key
      /// </summary>
      /// <remarks>Virtual key code: 69</remarks>
      E = 69,

      /// <summary>
      /// F key
      /// </summary>
      /// <remarks>Virtual key code: 70</remarks>
      F = 70,

      /// <summary>
      /// G key
      /// </summary>
      /// <remarks>Virtual key code: 71</remarks>
      G = 71,

      /// <summary>
      /// H key
      /// </summary>
      /// <remarks>Virtual key code: 72</remarks>
      H = 72,

      /// <summary>
      /// I key
      /// </summary>
      /// <remarks>Virtual key code: 73</remarks>
      I = 73,

      /// <summary>
      /// J key
      /// </summary>
      /// <remarks>Virtual key code: 74</remarks>
      J = 74,

      /// <summary>
      /// K key
      /// </summary>
      /// <remarks>Virtual key code: 75</remarks>
      K = 75,

      /// <summary>
      /// L key
      /// </summary>
      /// <remarks>Virtual key code: 76</remarks>
      L = 76,

      /// <summary>
      /// M key
      /// </summary>
      /// <remarks>Virtual key code: 77</remarks>
      M = 77,

      /// <summary>
      /// N key
      /// </summary>
      /// <remarks>Virtual key code: 78</remarks>
      N = 78,

      /// <summary>
      /// O key
      /// </summary>
      /// <remarks>Virtual key code: 79</remarks>
      O = 79,

      /// <summary>
      /// P key
      /// </summary>
      /// <remarks>Virtual key code: 80</remarks>
      P = 80,

      /// <summary>
      /// Q key
      /// </summary>
      /// <remarks>Virtual key code: 81</remarks>
      Q = 81,

      /// <summary>
      /// R key
      /// </summary>
      /// <remarks>Virtual key code: 82</remarks>
      R = 82,

      /// <summary>
      /// S key
      /// </summary>
      /// <remarks>Virtual key code: 83</remarks>
      S = 83,

      /// <summary>
      /// T key
      /// </summary>
      /// <remarks>Virtual key code: 84</remarks>
      T = 84,

      /// <summary>
      /// U key
      /// </summary>
      /// <remarks>Virtual key code: 85</remarks>
      U = 85,

      /// <summary>
      /// V key
      /// </summary>
      /// <remarks>Virtual key code: 86</remarks>
      V = 86,

      /// <summary>
      /// W key
      /// </summary>
      /// <remarks>Virtual key code: 87</remarks>
      W = 87,

      /// <summary>
      /// X key
      /// </summary>
      /// <remarks>Virtual key code: 88</remarks>
      X = 88,

      /// <summary>
      /// Y key
      /// </summary>
      /// <remarks>Virtual key code: 89</remarks>
      Y = 89,

      /// <summary>
      /// Z key
      /// </summary>
      /// <remarks>Virtual key code: 90</remarks>
      Z = 90,

      /// <summary>
      /// Left window key (Natural keyboard)
      /// </summary>
      /// <remarks>Virtual key code: 91</remarks>
      LeftWindows = 91,

      /// <summary>
      /// Right window key (Natural keyboard)
      /// </summary>
      /// <remarks>Virtual key code: 92</remarks>
      RightWindows = 92,

      /// <summary>
      /// Applications key (Natural keyboard)
      /// </summary>
      /// <remarks>Virtual key code: 93</remarks>
      Apps = 93,

      /// <summary>
      /// Sleep key
      /// </summary>
      /// <remarks>Virtual key code: 95</remarks>
      Sleep = 95,

      /// <summary>
      /// Numeric keypad 0 key
      /// </summary>
      /// <remarks>Virtual key code: 96</remarks>
      NumPad0 = 96,

      /// <summary>
      /// Numeric keypad 1 key
      /// </summary>
      /// <remarks>Virtual key code: 97</remarks>
      NumPad1 = 97,

      /// <summary>
      /// Numeric keypad 2 key
      /// </summary>
      /// <remarks>Virtual key code: 98</remarks>
      NumPad2 = 98,

      /// <summary>
      /// Numeric keypad 3 key
      /// </summary>
      /// <remarks>Virtual key code: 99</remarks>
      NumPad3 = 99,

      /// <summary>
      /// Numeric keypad 4 key
      /// </summary>
      /// <remarks>Virtual key code: 100</remarks>
      NumPad4 = 100,

      /// <summary>
      /// Numeric keypad 5 key
      /// </summary>
      /// <remarks>Virtual key code: 101</remarks>
      NumPad5 = 101,

      /// <summary>
      /// Numeric keypad 6 key
      /// </summary>
      /// <remarks>Virtual key code: 102</remarks>
      NumPad6 = 102,

      /// <summary>
      /// Numeric keypad 7 key
      /// </summary>
      /// <remarks>Virtual key code: 103</remarks>
      NumPad7 = 103,

      /// <summary>
      /// Numeric keypad 8 key
      /// </summary>
      /// <remarks>Virtual key code: 104</remarks>
      NumPad8 = 104,

      /// <summary>
      /// Numeric keypad 9 key
      /// </summary>
      /// <remarks>Virtual key code: 105</remarks>
      NumPad9 = 105,

      /// <summary>
      /// Numeric multiply key
      /// </summary>
      /// <remarks>Virtual key code: 106</remarks>
      Multiply = 106,

      /// <summary>
      /// Numeric add key
      /// </summary>
      /// <remarks>Virtual key code: 107</remarks>
      Add = 107,

      /// <summary>
      /// Separator key
      /// </summary>
      /// <remarks>Virtual key code: 108</remarks>
      Separator = 108,

      /// <summary>
      /// Numeric subtract key
      /// </summary>
      /// <remarks>Virtual key code: 109</remarks>
      Subtract = 109,

      /// <summary>
      /// Numeric decimal key
      /// </summary>
      /// <remarks>Virtual key code: 110</remarks>
      Decimal = 110,

      /// <summary>
      /// Numeric divide key
      /// </summary>
      /// <remarks>Virtual key code: 111</remarks>
      Divide = 111,

      /// <summary>
      /// F1 key
      /// </summary>
      /// <remarks>Virtual key code: 112</remarks>
      F1 = 112,

      /// <summary>
      /// F2 key
      /// </summary>
      /// <remarks>Virtual key code: 113</remarks>
      F2 = 113,

      /// <summary>
      /// F3 key
      /// </summary>
      /// <remarks>Virtual key code: 114</remarks>
      F3 = 114,

      /// <summary>
      /// F4 key
      /// </summary>
      /// <remarks>Virtual key code: 115</remarks>
      F4 = 115,

      /// <summary>
      /// F5 key
      /// </summary>
      /// <remarks>Virtual key code: 116</remarks>
      F5 = 116,

      /// <summary>
      /// F6 key
      /// </summary>
      /// <remarks>Virtual key code: 117</remarks>
      F6 = 117,

      /// <summary>
      /// F7 key
      /// </summary>
      /// <remarks>Virtual key code: 118</remarks>
      F7 = 118,

      /// <summary>
      /// F8 key
      /// </summary>
      /// <remarks>Virtual key code: 119</remarks>
      F8 = 119,

      /// <summary>
      /// F9 key
      /// </summary>
      /// <remarks>Virtual key code: 120</remarks>
      F9 = 120,

      /// <summary>
      /// F10 key
      /// </summary>
      /// <remarks>Virtual key code: 121</remarks>
      F10 = 121,

      /// <summary>
      /// F11 key
      /// </summary>
      /// <remarks>Virtual key code: 122</remarks>
      F11 = 122,

      /// <summary>
      /// F12 key
      /// </summary>
      /// <remarks>Virtual key code: 123</remarks>
      F12 = 123,

      /// <summary>
      /// F13 key
      /// </summary>
      /// <remarks>Virtual key code: 124</remarks>
      F13 = 124,

      /// <summary>
      /// F14 key
      /// </summary>
      /// <remarks>Virtual key code: 125</remarks>
      F14 = 125,

      /// <summary>
      /// F15 key
      /// </summary>
      /// <remarks>Virtual key code: 126</remarks>
      F15 = 126,

      /// <summary>
      /// F16 key
      /// </summary>
      /// <remarks>Virtual key code: 127</remarks>
      F16 = 127,

      /// <summary>
      /// F17 key
      /// </summary>
      /// <remarks>Virtual key code: 128</remarks>
      F17 = 128,

      /// <summary>
      /// F18 key
      /// </summary>
      /// <remarks>Virtual key code: 129</remarks>
      F18 = 129,

      /// <summary>
      /// F19 key
      /// </summary>
      /// <remarks>Virtual key code: 130</remarks>
      F19 = 130,

      /// <summary>
      /// F20 key
      /// </summary>
      /// <remarks>Virtual key code: 131</remarks>
      F20 = 131,

      /// <summary>
      /// F21 key
      /// </summary>
      /// <remarks>Virtual key code: 132</remarks>
      F21 = 132,

      /// <summary>
      /// F22 key
      /// </summary>
      /// <remarks>Virtual key code: 133</remarks>
      F22 = 133,

      /// <summary>
      /// F23 key
      /// </summary>
      /// <remarks>Virtual key code: 134</remarks>
      F23 = 134,

      /// <summary>
      /// F24 key
      /// </summary>
      /// <remarks>Virtual key code: 135</remarks>
      F24 = 135,

      /// <summary>
      /// Numeric lock key
      /// </summary>
      /// <remarks>Virtual key code: 144</remarks>
      NumLock = 144,

      /// <summary>
      /// Scroll lock key
      /// </summary>
      /// <remarks>Virtual key code: 145</remarks>
      ScrollLock = 145,

      /// <summary>
      /// Left shift key
      /// </summary>
      /// <remarks>Virtual key code: 160</remarks>
      LeftShift = 160,

      /// <summary>
      /// Right shift key
      /// </summary>
      /// <remarks>Virtual key code: 161</remarks>
      RightShift = 161,

      /// <summary>
      /// Left control key
      /// </summary>
      /// <remarks>Virtual key code: 162</remarks>
      LeftControl = 162,

      /// <summary>
      /// Right control key
      /// </summary>
      /// <remarks>Virtual key code: 163</remarks>
      RightControl = 163,

      /// <summary>
      /// Left alt key
      /// </summary>
      /// <remarks>Virtual key code: 164</remarks>
      LeftAlt = 164,

      /// <summary>
      /// Right alt key
      /// </summary>
      /// <remarks>Virtual key code: 165</remarks>
      RightAlt = 165,

      /// <summary>
      /// Browser backward key
      /// </summary>
      /// <remarks>Virtual key code: 166</remarks>
      BrowserBack = 166,

      /// <summary>
      /// Browser forward key
      /// </summary>
      /// <remarks>Virtual key code: 167</remarks>
      BrowserForward = 167,

      /// <summary>
      /// Browser refresh key
      /// </summary>
      /// <remarks>Virtual key code: 168</remarks>
      BrowserRefresh = 168,

      /// <summary>
      /// Browser setup key
      /// </summary>
      /// <remarks>Virtual key code: 169</remarks>
      BrowserStop = 169,

      /// <summary>
      /// Browser search key
      /// </summary>
      /// <remarks>Virtual key code: 170</remarks>
      BrowserSearch = 170,

      /// <summary>
      /// Browser favorites key
      /// </summary>
      /// <remarks>Virtual key code: 171</remarks>
      BrowserFavorites = 171,

      /// <summary>
      /// Browser home key
      /// </summary>
      /// <remarks>Virtual key code: 172</remarks>
      BrowserHome = 172,

      /// <summary>
      /// Mute volume key
      /// </summary>
      /// <remarks>Virtual key code: 173</remarks>
      VolumeMute = 173,

      /// <summary>
      /// Volume down key
      /// </summary>
      /// <remarks>Virtual key code: 174</remarks>
      VolumeDown = 174,

      /// <summary>
      /// Volume up key
      /// </summary>
      /// <remarks>Virtual key code: 175</remarks>
      VolumeUp = 175,

      /// <summary>
      /// Next media track key
      /// </summary>
      /// <remarks>Virtual key code: 176</remarks>
      MediaNextTrack = 176,

      /// <summary>
      /// Previous media trek key
      /// </summary>
      /// <remarks>Virtual key code: 177</remarks>
      MediaPreviousTrack = 177,

      /// <summary>
      /// Media stop key
      /// </summary>
      /// <remarks>Virtual key code: 178</remarks>
      MediaStop = 178,

      /// <summary>
      /// Media play/pause key
      /// </summary>
      /// <remarks>Virtual key code: 179</remarks>
      MediaPlayPause = 179,

      /// <summary>
      /// Launch email key
      /// </summary>
      /// <remarks>Virtual key code: 180</remarks>
      LaunchMail = 180,

      /// <summary>
      /// Select media key
      /// </summary>
      /// <remarks>Virtual key code: 181</remarks>
      SelectMedia = 181,

      /// <summary>
      /// Launch application 1 key
      /// </summary>
      /// <remarks>Virtual key code: 182</remarks>
      LaunchApplication1 = 182,

      /// <summary>
      /// Launch application 2 key
      /// </summary>
      /// <remarks>Virtual key code: 183</remarks>
      LaunchApplication2 = 183,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the ';:' key
      /// </summary>
      /// <remarks>Virtual key code: 186</remarks>
      OemSemicolon = 186,

      /// <summary>
      /// For any country/region, the '+' key
      /// </summary>
      /// <remarks>Virtual key code: 187</remarks>
      OemPlus = 187,

      /// <summary>
      /// For any country/region, the ',' key
      /// </summary>
      /// <remarks>Virtual key code: 188</remarks>
      OemComma = 188,

      /// <summary>
      /// For any country/region, the '-' key
      /// </summary>
      /// <remarks>Virtual key code: 189</remarks>
      OemMinus = 189,

      /// <summary>
      /// For any country/region, the '.' key
      /// </summary>
      /// <remarks>Virtual key code: 190</remarks>
      OemPeriod = 190,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the '/?' key
      /// </summary>
      /// <remarks>Virtual key code: 191</remarks>
      OemQuestion = 191,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the '`~' key
      /// </summary>
      /// <remarks>Virtual key code: 192</remarks>
      OemTilde = 192,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the '[{' key
      /// </summary>
      /// <remarks>Virtual key code: 219</remarks>
      OemOpenBrackets = 219,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the '\|' key
      /// </summary>
      /// <remarks>Virtual key code: 220</remarks>
      OemPipe = 220,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the ']}' key
      /// </summary>
      /// <remarks>Virtual key code: 221</remarks>
      OemCloseBrackets = 221,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// For the US standard keyboard, the 'single-quote/double-quote' key
      /// </summary>
      /// <remarks>Virtual key code: 222</remarks>
      OemQuotes = 222,

      /// <summary>
      /// Used for miscellaneous characters; it can vary by keyboard.
      /// </summary>
      /// <remarks>Virtual key code: 223</remarks>
      Oem8 = 223,

      /// <summary>
      /// Either the angle bracket key or the backslash key on the RT 102-key keyboard
      /// </summary>
      /// <remarks>Virtual key code: 226</remarks>
      OemBackslash = 226,

      /// <summary>
      /// IME PROCESS key
      /// </summary>
      /// <remarks>Virtual key code: 229</remarks>
      ProcessKey = 229,

      /// <summary>
      /// The OEM ATTN key.
      /// </summary>
      /// <remarks>Virtual key code: 240</remarks>
      OemAttn = 240,

      /// <summary>
      /// The OEM FINISH key.
      /// </summary>
      /// <remarks>Virtual key code: 241</remarks>
      OemFinish = 241,

      /// <summary>
      /// The OEM COPY key
      /// </summary>
      /// <remarks>Virtual key code: 242</remarks>
      OemCopy = 242,

      /// <summary>
      /// The OEM AUTO key.
      /// </summary>
      /// <remarks>Virtual key code: 243</remarks>
      OemAuto = 243,

      /// <summary>
      /// The OEM ENLW key.
      /// </summary>
      /// <remarks>Virtual key code: 244</remarks>
      OemEnlW = 244,

      /// <summary>
      /// BackTab key
      /// </summary>
      /// <remarks>Virtual key code: 245</remarks>
      OemBackTab = 245,

      /// <summary>
      /// Attn key
      /// </summary>
      /// <remarks>Virtual key code: 246</remarks>
      Attn = 246,

      /// <summary>
      /// CrSel key
      /// </summary>
      /// <remarks>Virtual key code: 247</remarks>
      Crsel = 247,

      /// <summary>
      /// ExSel key
      /// </summary>
      /// <remarks>Virtual key code: 248</remarks>
      Exsel = 248,

      /// <summary>
      /// Erase EOF key
      /// </summary>
      /// <remarks>Virtual key code: 249</remarks>
      EraseEof = 249,

      /// <summary>
      /// Play key
      /// </summary>
      /// <remarks>Virtual key code: 250</remarks>
      Play = 250,

      /// <summary>
      /// Zoom key
      /// </summary>
      /// <remarks>Virtual key code: 251</remarks>
      Zoom = 251,
      
      /// <summary>
      /// PA 1 key 
      /// </summary>
      /// <remarks>Virtual key code: 253</remarks>
      Pa1 = 253,

      /// <summary>
      /// Clear key
      /// </summary>
      /// <remarks>Virtual key code: 254</remarks>
      OemClear = 254
   }
}
