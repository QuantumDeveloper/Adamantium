using Adamantium.Engine.Compiler.Converter.ConversionUtils;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Compiler.Converter.Containers
{
   public class Autodesk3DsDataContainer:DataContainer
   {
      public Autodesk3DsDataContainer(string filePath) : base(filePath)
      {
            Data = new SceneData();
      }

     public SceneData Data { get; set; }

      public override FileType Type => FileType.AutoDesk3DS;
   }
}
