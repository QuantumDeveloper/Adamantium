namespace Adamantium.Game
{
   /// <summary>
   /// Options used when using <see cref="RawInputDevice.RegisterDevice"/>
   /// </summary>
   public enum FilteringOptions
   {
      /// <summary>
      /// Default register using <see cref="Application.AddMessageFilter"/> for RawInput message filtering
      /// </summary>
      Default = 0,

      /// <summary>
      /// Use custom message filtering instead of <see cref="Application.AddMessageFilter"/>
      /// </summary>
      CustomFiltering = 1,

      /// <summary>
      /// Do not use messaging automatically
      /// </summary>
      NoFiltering = 2
   }
}
