using System;
using System.ComponentModel;
using Adamantium.Engine.Graphics;
using Adamantium.Mathematics;
using Component = Adamantium.EntityFramework.ComponentsBasics.Component;
using TextureDimension = Adamantium.Engine.Core.Models.TextureDimension;

namespace Adamantium.EntityFramework.Components
{
    public class Material : Component
   {
      public Texture Texture { get; set; }

      public TextureDimension TextureDimension { get; set; }

      public String TexturePath { get; set; }

      //public SamplerState Sampler { get; set; }

      public Vector4F Emission { get; set; }

      public Vector4F DiffuseColor { get; set; }

      public Vector4F AmbientColor { get; set; }

      public Vector4F SpecularColor { get; set; }

      [DefaultValue(1.0f)]
      public Single Shininess { get; set; }

      public Vector4F Reflective { get; set; }

      public Vector4F Transparent { get; set; }

      public Single Reflectivity { get; set; }

      [DefaultValue(1.0f)]
      public Single Transparency { get; set; }

      public Single RefractionIndex { get; set; }

      public Vector4F HighlightColor { get; set; }

      public Vector3F MeshColor { get; set; }

      public Vector4F BoundingBoxColor { get; set; } = Colors.White.ToVector4();

      public Vector4F LightColor { get; set; }

   }
}
