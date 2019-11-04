using ProtoBuf;

namespace Adamantium.Engine.Core.Effects
{
   public sealed partial class EffectData
   {
      [ProtoContract]
      public sealed class Pass
      {
         private CommonData.PropertyCollection properties;

         /// <summary>
         /// Name of this pass.
         /// </summary>
         [ProtoMember(1)]
         public string Name;

         /// <summary>
         /// True if this pass is the sub-pass of a root pass.
         /// </summary>
         [ProtoMember(2)]
         public bool IsSubPass;

         /// <summary>
         /// List of <see cref="EffectData.Properties"/>.
         /// </summary>
         [ProtoMember(3)]
         public CommonData.PropertyCollection Properties
         {
            get { return properties ?? (properties = new CommonData.PropertyCollection()); }
            set { properties = value; }
         }

         /// <summary>
         /// Description of the shader stage <see cref="Pipeline"/>.
         /// </summary>
         [ProtoMember(4)]
         public Pipeline Pipeline;


         /// <summary>
         /// Clones this instance.
         /// </summary>
         /// <returns>Pass.</returns>
         public Pass Clone()
         {
            var pass = (Pass)MemberwiseClone();
            if (pass.Properties != null)
            {
               pass.Properties = pass.Properties.Clone();
            }
            if (pass.Pipeline != null)
            {
               pass.Pipeline = pass.Pipeline.Clone();
            }

            return pass;
         }

         /// <summary>
         /// Returns a string that represents the current object.
         /// </summary>
         /// <returns>
         /// A string that represents the current object.
         /// </returns>
         /// <filterpriority>2</filterpriority>
         public override string ToString()
         {
            return $"Pass: [{Name}], SubPass: {IsSubPass}, Attributes({Properties.Count})";
         }
      }
   }
}
