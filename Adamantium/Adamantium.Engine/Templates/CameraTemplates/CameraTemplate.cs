﻿using Adamantium.EntityFramework;
using Adamantium.Mathematics;


namespace Adamantium.Engine.Templates.Camera
{
    public class CameraTemplate
    {
        public Entity BuildEntity(Entity owner, string name, Vector3D position, Vector3F lookAt, Vector3F up, uint width, uint height, float znear, float zfar)
        {
            Entity root = new Entity(owner, name);
            root.Transform.Position = position;
            var camera = new EntityFramework.Components.Camera(lookAt, up, 45, width, height, znear, zfar);
            root.AddComponent(camera);
            return root;
        }
    }
}
