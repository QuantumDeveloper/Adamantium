namespace Adamantium.Graphics.Core.Models
{
   /// <summary>Whose data a skin source holds. Not flags: a source carries EXACTLY ONE role, and the former
   /// [Flags] with Joint = 0 meant HasFlag(Joint) was always true.</summary>
   public enum ControllerSemantic
   {
      Joint,
      Weight,
      InverseBindMatrix
   }
}
