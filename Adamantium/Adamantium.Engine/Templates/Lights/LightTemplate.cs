using System;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using LightType = Adamantium.Engine.Core.LightType;

namespace Adamantium.Engine.Templates.Lights
{
    public class LightTemplate
    {
        public Entity BuildEntity(Entity owner, string name, LightType type)
        {
            switch (type)
            {
                case LightType.Directional:
                    return BuildSubEntity(owner, name, new Light(LightType.Directional));
                case LightType.Point:
                    return BuildSubEntity(owner, name, new Light(LightType.Point));
                case LightType.Spot:
                    return BuildSubEntity(owner, name, new Light(LightType.Spot));
                default:
                    throw new NotSupportedException(nameof(type));
            }
        }

        protected Entity BuildSubEntity(Entity owner, String name, Light light)
        {
            var entity = new Entity(owner, name);
            entity.AddComponent(light);
            
            return entity;
        }
    }
}
