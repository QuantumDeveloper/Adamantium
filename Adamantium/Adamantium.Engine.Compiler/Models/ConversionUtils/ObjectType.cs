namespace Adamantium.Engine.Compiler.Converter.ConversionUtils
{
   /// <summary>
   /// Describes a concrete type of object present in obj file format.
   /// When string starts from 'o', its object, when it starts from 'g', its group, which contains hierarchy
   /// </summary>
   public enum ObjectType
   {
      /// <summary>
      /// Complete object
      /// </summary>
      Object,

      /// <summary>
      /// Group of objects in hierarchy
      /// </summary>
      Group
   }
}
