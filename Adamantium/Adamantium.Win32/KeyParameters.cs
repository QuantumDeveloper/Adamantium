namespace Adamantium.Win32
{
   public struct KeyParameters
   {
      public bool IsRepeated => (PreviousState == KeyStates.Down && CurrentState == KeyStates.Down);

      public uint PressTime { get; set; }

      public int RepeatCount { get; set; }
      public int ScanCode { get; set; }
      public bool IsExtendedKey { get; set; }
      public int ContextCode { get; set; }
      public KeyStates PreviousState { get; set; }
      public KeyStates CurrentState { get; set; }
   }
}
