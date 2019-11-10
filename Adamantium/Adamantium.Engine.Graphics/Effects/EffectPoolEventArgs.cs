using System;

namespace Adamantium.Engine.Effects
{
   public class EffectPoolEventArgs:EventArgs
   {
      public Effect Effect { get; set; }

      public EffectPoolEventArgs(Effect effect)
      {
         Effect = effect;
      }
   }
}
