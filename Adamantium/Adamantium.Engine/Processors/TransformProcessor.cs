using System;
using Adamantium.Engine.Core;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Win32;

namespace Adamantium.Engine.Processors
{
    public class TransformProcessor : EntityProcessor
    {
        private ToolsService tools;
        private LightService lightService;
        private CameraService cameraService;

        public Boolean IsPaused { get; set; }

        public TransformProcessor(EntityWorld world)
            : base(world)
        {
            
        }

        public override void Initialize()
        {
            tools = EntityWorld.Services.Resolve<ToolsService>();
            lightService = EntityWorld.Services.Resolve<LightService>();
        }

        public override void Update(IGameTime gameTime)
        {
            cameraService = EntityWorld.Services.Resolve<CameraService>();
            var entities = Entities;
            try
            {
                foreach (var entity in entities)
                {
                    Transform(entity, gameTime);
                }
                tools.Update(entities, cameraService);
                lightService.Update();
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message + e.StackTrace);
            }
        }
        
        private void Transform(Entity entity, IGameTime gameTime)
        {
            var generalCenter = entity.GetLocalCenter();
            entity.TraverseInDepth(current =>
            {
                var colliders = current.GetComponents<Collider>();
                foreach (var camera in cameraService.ActiveCameras)
                {
                    if (camera.Owner == current)
                    {
                        //continue;
                    }

                    current.Transform.CalculateFinalTransform(camera, generalCenter);

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
}