namespace Adamantium.UI.Data;

/// <summary>
/// Defines possible binding modes.
/// </summary>
public enum BindingMode
{
   /// <summary>
   /// Default binding mode.
   /// </summary>
   /// <remarks>If value set to default, Binding mode will get from <see cref="AdamantiumProperty "/> metadata</remarks>
   Default,

   /// <summary>
   /// Updates the target when the application starts or when the data context changes.
   /// </summary>
   OneTime,

   /// <summary>
   /// Binds one way from source to target.
   /// </summary>
   OneWay,

   /// <summary>
   /// Binds two-way with the initial value coming from the target.
   /// </summary>
   TwoWay,

   /// <summary>
   /// Binds one way from target to source.
   /// </summary>
   OneWayToSource
}