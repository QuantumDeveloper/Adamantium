using Adamantium.Mathematics;

namespace Adamantium.Engine.Compiler.Converter.ConversionUtils
{
   internal class MatrixParams
   {
      public MatrixParams()
      {
         Scale = Vector3F.One;
         Rotation = QuaternionF.Identity;
         Translation = Vector3F.Zero;
      }

      public static MatrixParams Default;

      static MatrixParams()
      {
         Default = new MatrixParams();
      }

      public Vector3F Translation;
      public Vector3F Scale;
      public QuaternionF Rotation;
   }
}
