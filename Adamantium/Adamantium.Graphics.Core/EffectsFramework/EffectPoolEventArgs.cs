using System;

namespace Adamantium.Graphics.Core.EffectsFramework;

public class EffectPoolEventArgs : EventArgs
{
   public Effect Effect { get; set; }

   public EffectPoolEventArgs(Effect effect)
   {
      Effect = effect;
   }
}