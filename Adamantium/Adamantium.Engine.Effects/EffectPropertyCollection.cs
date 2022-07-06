using System;
using System.Collections.Generic;
using Adamantium.Core;

namespace Adamantium.Engine.Effects;

public class EffectPropertyCollection : Dictionary<string, object>
{
    public void SetProperty<T>(PropertyKey<T> key, T value)
    {
        if (Utilities.IsEnum<T>(value))
        {
            var intValue = Convert.ToInt32(value);
            Add(key.Name, intValue);
        }
        else
        {
            Add(key.Name, value);
        }
    }

    public EffectPropertyCollection Clone()
    {
        return (EffectPropertyCollection) MemberwiseClone();
    }
}