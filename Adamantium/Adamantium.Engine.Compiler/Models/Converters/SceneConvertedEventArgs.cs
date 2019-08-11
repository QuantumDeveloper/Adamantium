using System;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.Converters
{
   public class SceneConvertedEventArgs: EventArgs
   {
      public SceneData SceneData { get; set; }

      public SceneConvertedEventArgs(SceneData sceneData)
      {
         SceneData = sceneData;
      }
   }
}
