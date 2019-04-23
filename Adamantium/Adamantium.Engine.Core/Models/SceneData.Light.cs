using System;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Core.Models
{
   public partial class SceneData
   {
      public class Light
      {
         public Light()
         {
            Scale = Vector3F.One;
            Rotation = QuaternionF.Identity;
         }

         public Light(Light copy)
         {
            ID = copy.ID;
            Name = copy.Name;
            Position = copy.Position;
            Color = new Vector3F(copy.Color.ToArray());
            LightType = copy.LightType;
         }

         public String Name { get; set; }

         public String ID { get; set; }

         public LightType LightType { get; set; }

         public Vector3F Color { get; set; }

         public Vector3F Position { get; set; }

         public Vector3F Scale { get; set; }

         public QuaternionF Rotation { get; set; }

         //Константное затухание
         public Single ConstantAttenuation { get; set; }

         //Линейное затухание
         public Single LinearAttenuation { get; set; }

         //Квадратичное затухание
         public Single QuadraticAttenuation { get; set; }

         public Single FalloffAngle { get; set; }

         public Single FalloffExponent { get; set; }
      }
   }
}
