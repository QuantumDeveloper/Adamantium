using System;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Image
      {
         public Image()
         {

         }

         public Image(String imageName, String filePath)
         {
            ImageName = imageName;
            FilePath = filePath;
         }

         public String ID { get; set; }
         public String ImageName { get; set; }
         public String FilePath { get; set; }
      }
   }
   
}
