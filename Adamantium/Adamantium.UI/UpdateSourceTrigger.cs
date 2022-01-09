namespace Adamantium.UI;

/// <summary>
/// Value indicates when binded value must be updated
/// </summary>
public enum UpdateSourceTrigger
{
   /// <summary>
   /// For most <see cref="AdamantiumProperty"/>s default value is PropertyChanged
   /// </summary>
   Default,

   /// <summary>
   /// Update source value when <see cref="AdamantiumProperty"/> changed
   /// </summary>
   PropertyChanged,

   /// <summary>
   /// Update source value when Control has lost focus
   /// </summary>
   LostFocus,

   /// <summary>
   /// Update source value on BindingExpression.UpdateSource()
   /// </summary>
   Explicit
}