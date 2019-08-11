using System;
using Adamantium.Engine.Core;

namespace Adamantium.Engine.Compiler.Converter
{
   [Flags]
   public enum Modules
   {
      [Order(-1)]
      None = 0,
      [Order(0)]
      Geometry = 2,
      [Order(1)]
      Animation = 4,
      [Order(2)]
      Controllers = 8,
      [Order(3)]
      Light = 16,
      [Order(4)]
      Images = 32,
      [Order(5)]
      Materials = 64,
      [Order(6)]
      Effects = 128,
      [Order(7)]
      Cameras = 256,
      [Order(8)]
      VisualScenes = 512
   }
}
