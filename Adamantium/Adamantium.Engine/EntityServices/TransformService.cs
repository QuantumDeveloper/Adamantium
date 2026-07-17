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
            var transform = current.Transform;
            var dirty = transform.IsWorldDirty;
            Collider[] colliders = null;

            foreach (var camera in cameraManager.ActiveCameras)
            {
                if (camera.Owner == current)
                {
                    continue;
                }

                var metadata = transform.GetMetadata(camera);
                // Recompute this (node, camera) ONLY when an input to its world matrix changed: the node's own transform
                // (dirty - also set below when its PARENT moved), the CAMERA position (the world is camera-relative; note a
                // rotating camera does NOT move, so mouse-look costs nothing), the shared pivot, or a first-ever compute.
                // A static scene therefore skips the whole matrix + collider pass instead of rebuilding it every frame.
                if (!dirty && metadata.Computed
                    && metadata.LastCameraPosition == camera.Owner.Transform.Position
                    && metadata.LastPivotCorrection == generalCenter)
                {
                    continue;
                }

                // The owner's world for THIS camera, computed already (the walk is top-down); identity for a root.
                var parentWorld = current.Owner?.Transform != null
                    ? current.Owner.Transform.GetMetadata(camera).AbsoluteWorld
                    : Matrix4x4F.Identity;
                transform.CalculateFinalTransform(camera, generalCenter, parentWorld);

                // Collider bounds ride the same world matrix, so refresh them exactly when it was recomputed (fetch the
                // list lazily so a fully-static node allocates nothing).
                colliders ??= current.GetComponents<Collider>();
                for (int i = 0; i < colliders.Length; ++i)
                {
                    colliders[i].UpdateForCamera(camera);
                }
            }

            transform.IsWorldDirty = false;
            // A moved node changes its children's parent-world, so dirty them for this same top-down pass (a camera move
            // needs no propagation - every node detects that independently above).
            if (dirty)
            {
                for (int i = 0; i < current.Dependencies.Count; ++i)
                {
                    var childTransform = current.Dependencies[i].Transform;
                    if (childTransform != null)
                    {
                        childTransform.IsWorldDirty = true;
                    }
                }
            }

            current.GetComponent<AnimationComponent>()?.Update(gameTime);
            current.GetComponent<AnimationController>()?.Update(gameTime);
        });
    }
}