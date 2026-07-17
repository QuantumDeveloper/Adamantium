using System;
using Adamantium.Core;
using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.ECS;
using Adamantium.ECS.Components;
using Adamantium.ECS.Components.Extensions;
using Adamantium.Mathematics;
using Adamantium.Win32;

namespace Adamantium.Engine.EntityServices;

public class TransformService : EntityService
{
    private ToolsManager tools;
    private LightManager lightManager;
    private CameraManager cameraManager;

    public Boolean IsPaused { get; set; }

    public TransformService(EntityWorld world)
        : base(world)
    {
            
    }

    public override bool IsUpdateService => true;
    public override bool IsRenderingService => false;
    public override EntityServiceType ServiceType => EntityServiceType.Update;

    public override void Initialize()
    {
        tools = EntityWorld.DependencyResolver.Resolve<ToolsManager>();
        lightManager = EntityWorld.DependencyResolver.Resolve<LightManager>();
        cameraManager = EntityWorld.DependencyResolver.Resolve<CameraManager>();
    }

    public override void Update(AppTime gameTime)
    {
        var entities = Entities;
        try
        {
            foreach (var entity in entities)
            {
                Transform(entity, gameTime);
            }
            tools.Update(entities, cameraManager, lightManager);
            lightManager.Update();
        }
        catch (Exception e)
        {
            MessageBox.Show(e.Message + e.StackTrace);
        }
    }
        
    private void Transform(Entity entity, AppTime gameTime)
    {
        var generalCenter = entity.GetLocalCenter();
        entity.TraverseInDepth(current =>
        {
            var colliders = current.GetComponents<Collider>();
            foreach (var camera in cameraManager.ActiveCameras)
            {
                if (camera.Owner == current)
                {
                    continue;
                }

                // The owner's world for THIS camera, computed already (the walk is top-down); identity for a root. This is
                // what makes the transform hierarchy real - the child composes onto the parent instead of standing alone.
                var parentWorld = current.Owner?.Transform != null
                    ? current.Owner.Transform.GetMetadata(camera).AbsoluteWorld
                    : Matrix4x4F.Identity;
                current.Transform.CalculateFinalTransform(camera, generalCenter, parentWorld);

                for (int i = 0; i < colliders.Length; ++i)
                {
                    colliders[i].ClearData();
                    for (int j = 0; j < colliders.Length; ++j)
                    {
                        colliders[j].UpdateForCamera(camera);
                    }
                }
            }

            var animation = current.GetComponent<AnimationComponent>();
            var controller = current.GetComponent<AnimationController>();
            animation?.Update(gameTime);
            controller?.Update(gameTime);
        });
    }
}