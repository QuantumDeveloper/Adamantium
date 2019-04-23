using System;
using System.ComponentModel;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Material
      {
         public Material()
         {
         }

         public String ID { get; set; }

         public String MeshID { get; set; }

         public Vector4F Emission { get; set; }

         public Vector4F DiffuseColor { get; set; }

         public Vector4F AmbientColor { get; set; }

         public Vector4F SpecularColor { get; set; }

         [DefaultValue(1.0f)]
         public Single Shininess { get; set; }

         public Vector4F Reflective { get; set; }

         public Vector4F Transparent { get; set; }

         public Single Reflectivity { get; set; }

         public Single Transparency { get; set; }

         public Single RefractionIndex { get; set; }


         public String AmbientMap { get; set; }

         public String DiffuseMap { get; set; }

         public String SpecularColorMap { get; set; }

         public String SpecularHighlightMap { get; set; }

         public String AlphaTextureMap { get; set; }

         public String DisplacementMap { get; set; }

         public String BumpMap { get; set; }

         public String ReflectionMap { get; set; }

      }
   }


}
