using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Graphics;
using Adamantium.Engine.Templates.Lights;
using Adamantium.Engine.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Services
{
    public class LightService
    {
        private List<Light> lights;
        //private Effect depthWriter;
        private Game _game;

        private object _syncObj = new object();

        private ReadOnlyCollection<Light> _lights;
        public ReadOnlyCollection<Light> Lights => _lights;

        public List<Light> SpotLights { get; private set; }
        public List<Light> PointLights { get; private set; }
        public List<Light> DirectionalLights { get; private set; }

        private Entity DirectionalLightIcon;
        private Entity PointLightIcon;
        private Entity SpotLightIcon;
        private Entity DirectionalLightVisual;
        private Entity PointLightVisual;
        private Entity SpotLightVisual;

        private Collider directionalCollider;
        private Collider pointCollider;
        private Collider spotCollider;
        
        private EntityGroup _lightsGroup;

        private Entity SpotLightMesh;
        private Entity PointLightMesh;

        private MeshRenderer spotLightRenderer;
        private MeshRenderer pointLightRenderer;
        private LightTool currentTool;

        private DirectionalLightTool DirectionalLightTool { get; set; }

        private SpotLightTool SpotLightTool { get; set; }

        private PointLightTool PointLightTool { get; set; }

        public LightService(Game game)
        {
            _game = game;
            _game.Services.Add(this);
            //depthWriter = game.Content.Load<Effect>("Effects/DeferredShading/DepthWriter");
            lights = new List<Light>();
            _lights = new ReadOnlyCollection<Light>(lights);
            _lightsGroup = new EntityGroup("Lights");
            game.EntityWorld.AddGroup(_lightsGroup);

            SpotLightMesh = new SpotLightMeshTemplate().BuildEntity();
            PointLightMesh = new PointLightMeshTemplate().BuildEntity();

            spotLightRenderer = SpotLightMesh.GetComponent<MeshRenderer>();
            pointLightRenderer = PointLightMesh.GetComponent<MeshRenderer>();

            DirectionalLights = new List<Light>();
            SpotLights = new List<Light>();
            PointLights = new List<Light>();

            SpotLightTool = new SpotLightTool(nameof(Tools.SpotLightTool));
            PointLightTool = new PointLightTool(nameof(Tools.PointLightTool));
            DirectionalLightTool = new DirectionalLightTool(nameof(Tools.DirectionalLightTool));
            SpotLightTool.Enabled = true;
            PointLightTool.Enabled = true;
            DirectionalLightTool.Enabled = true;

            game.EntityWorld.AddToGroup(SpotLightTool.Tool, "Lights");
            game.EntityWorld.AddToGroup(PointLightTool.Tool, "Lights");
            game.EntityWorld.AddToGroup(DirectionalLightTool.Tool, "Lights");

            Task.Run(() => CreateLightsIcons());
            Task.Run(() => CreateLightsVisual());
        }

        public void Update()
        {
            DirectionalLights.Clear();
            DirectionalLights = _lights.Where(x => x.Type == LightType.Directional).ToList();

            SpotLights.Clear();
            SpotLights = _lights.Where(x => x.Type == LightType.Spot).ToList();

            PointLights.Clear();
            PointLights = _lights.Where(x => x.Type == LightType.Point).ToList();
        }

        private void CreateLightsIcons()
        {
            DirectionalLightIcon = new DirectionalLightIconTemplate().BuildEntity(null, "Directional Light Icon");
            PointLightIcon = new PointLightIconTemplate().BuildEntity(null, "Point light icon");
            SpotLightIcon = new SpotLightIconTemplate().BuildEntity(null, "Spot light icon");

            directionalCollider = DirectionalLightIcon.GetComponent<Collider>();
            pointCollider = PointLightIcon.GetComponent<Collider>(); 
            spotCollider = SpotLightIcon.GetComponent<Collider>();
        }

        private void CreateLightsVisual()
        {
            DirectionalLightVisual = new DirectionalLightVisualTemplate().BuildEntity(null, "Directional Light Visual");
            PointLightVisual = new PointLightVisualTemplate().BuildEntity(null, "Point light visual");
            SpotLightVisual = new SpotLightVisualTemplate().BuildEntity(null, "Spot light visual");
        }

        public Entity CreateLight(LightType type, string name)
        {
            Entity light = null;
            if (string.IsNullOrEmpty(name))
            {
                name = type.ToString();
            }
            light = new LightTemplate().BuildEntity(null, name, type);
            double diameter = 0;
            switch (type)
            {
                case LightType.Directional:
                    diameter = DirectionalLightIcon.GetDiameter();
                    break;
                case LightType.Point:
                    diameter = PointLightIcon.GetDiameter();
                    break;
                case LightType.Spot:
                    diameter = SpotLightIcon.GetDiameter();
                    break;
            }
            
            light.Transform.SetPosition(light.GetPositionForNewObject(_game.CameraService.UserControlledCamera, diameter));
            AddLight(light);
            return light;
        }

        public void AddLight(Entity light)
        {
            var lightComponent = light.GetComponent<Light>();
            if (lightComponent == null)
                return;

            _lightsGroup.Add(light);

            switch (lightComponent.Type)
            {
                case LightType.Directional:
                    DirectionalLights.Add(lightComponent);
                    break;
                case LightType.Point:
                    PointLights.Add(lightComponent);
                    break;
                case LightType.Spot:
                    SpotLights.Add(lightComponent);
                    break;
            }

            lock (_syncObj)
            {
                lights.Add(lightComponent);
            }
            
            _game.EntityWorld.AddEntity(light);
        }

        public void RemoveLight(Entity light)
        {
            _lightsGroup.Remove(light);

            var lightComponent = light.GetComponent<Light>();
            if (lightComponent == null)
                return;

            lights.Remove(lightComponent);
        }

        public bool Contains(Entity light)
        {
            return _lightsGroup.Contains(light);
        }

        public CollisionResult Intersects(Camera camera, Vector2F cursorPosition, CollisionMode collisionMode)
        {
            lock (_syncObj)
            {
                CollisionResult collisionResult = new CollisionResult(CompareOrder.Less);
                var projectionMatrix = camera.ProjectionMatrix;
                foreach (var light in lights)
                {
                    var transform = light.Owner.Transform.GetMetadata(camera);
                    Vector3F point;
                    var billboard = Matrix4x4F.BillboardLH(transform.RelativePosition, Vector3F.Zero, transform.WorldMatrix.Up, camera.Forward);
                    var rotation = MathHelper.GetRotationFromMatrix(billboard);
                    var world = Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(transform.RelativePosition);
                    var ray = Collisions.CalculateRay(cursorPosition, camera, world, projectionMatrix, true);

                    var collision = GetColliderForLightType(light);
                    if (collision != null)
                    {
                        var intersects = collision.Intersects(ref ray, out point);
                        if (intersects)
                        {
                            collisionResult.ValidateAndSetValues(light.Owner, point, true);
                        }
                    }
                }
                return collisionResult;
            }
        }

        private Collider GetColliderForLightType(Light light)
        {
            switch (light.Type)
            {
                case LightType.Directional:
                    return directionalCollider;
                case LightType.Point:
                    return pointCollider;
                case LightType.Spot:
                    return spotCollider;
            }
            return null;
        }

//        public void DrawIcons(Effect effect, Camera camera, GraphicsDevice drawingContext, IGameTime gametime)
//        {
//            var directionRender = DirectionalLightIcon.GetComponent<RenderableComponent>();
//            var pointRender = PointLightIcon.GetComponent<RenderableComponent>();
//            var spotRender = SpotLightIcon.GetComponent<RenderableComponent>();
//            lock (_syncObj)
//            {
//                var view = camera.ViewMatrix;
//                var proj = camera.ProjectionMatrix;
//                effect.Parameters["viewMatrix"].SetValue(view);
//                effect.Parameters["projectionMatrix"].SetValue(proj);
//
//                foreach (var light in lights)
//                {
//                    if (!light.Owner.IsEnabled)
//                    {
//                        continue;
//                    }
//
//                    var transform = light.Owner.Transform.GetMetadata(camera);
//
//                    if (transform.RelativePosition.Length() < PointLightIcon.GetDiameter())
//                    {
//                        continue;
//                    }
//
//                    var billboard = Matrix4x4F.BillboardRH(transform.RelativePosition, Vector3F.Zero, camera.Up, camera.Forward);
//                    var rotation = MathHelper.GetRotationFromMatrix(billboard);
//                    var world = Matrix4x4F.RotationQuaternion(rotation) * Matrix4x4F.Translation(transform.RelativePosition);
//
//                    var transparency = 1 - (1 / transform.RelativePosition.Length());
//                    effect.Parameters["transparency"].SetValue(transparency);
//                    effect.Parameters["worldMatrix"].SetValue(world);
//                    effect.Parameters["wvp"].SetValue(world * view * proj);
//                    effect.Parameters["meshColor"].SetValue(light.Color);
//                    effect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
//                    if (light.Type == LightType.Directional)
//                    {
//                        directionRender.Draw(drawingContext, gametime);
//                    }
//                    else if (light.Type == LightType.Point)
//                    {
//                        pointRender.Draw(drawingContext, gametime);
//                    }
//                    else if (light.Type == LightType.Spot)
//                    {
//                        spotRender.Draw(drawingContext, gametime);
//                    }
//                }
//            }
//
//            effect.Techniques["MeshVertex"].Passes["NoLight"].UnApply();
//        }
//
//        public void DrawDebugLight(Entity lightEntity, Effect effect, CameraService cameraService, Camera activeCamera, GraphicsDevice drawingContext, IGameTime gameTime)
//        {
//            if (lightEntity == null || !lightEntity.IsEnabled)
//                return;
//
//            if (!Contains(lightEntity))
//            {
//                return;
//            }
//
//            var light = lightEntity.GetComponent<Light>();
//            LightTool tool = null;
//            switch (light.Type)
//            {
//                case LightType.Directional:
//                    tool = DirectionalLightTool;
//                    break;
//                case LightType.Point:
//                    tool = PointLightTool;
//                    break;
//                case LightType.Spot:
//                    tool = SpotLightTool;
//                    break;
//            }
//            tool.TransformTool(lightEntity, light, cameraService, activeCamera);
//            tool.Tool.TraverseByLayer(current => ProcessLight(current, effect, cameraService.UserControlledCamera, drawingContext, gameTime), true);
//            effect.Techniques["MeshVertex"].Passes["NoLight"].UnApply(true);
//        }
//
//        private void ProcessLight(Entity current, Effect effect, Camera camera, GraphicsDevice drawingContext, IGameTime gameTime)
//        {
//            var transformation = current.Transform.GetMetadata(camera);
//            if (!transformation.Enabled || !current.Visible)
//            {
//                return;
//            }
//
//            var geometries = current.GetComponents<MeshRendererBase>();
//            foreach (var component in geometries)
//            {
//                var world = transformation.WorldMatrix;
//                var wvp = world * camera.ViewMatrix * camera.ProjectionMatrix;
//                effect.Parameters["wvp"].SetValue(wvp);
//
//                if (!current.IsSelected)
//                {
//                    effect.Parameters["meshColor"].SetValue(Colors.Yellow.ToVector3());
//                }
//                else
//                {
//                    effect.Parameters["meshColor"].SetValue(Colors.Red.ToVector3());
//                }
//
//                effect.Techniques["MeshVertex"].Passes["NoLight"].Apply();
//                component.Draw(drawingContext, gameTime);
//            }
//        }

        public void DrawPointLightMesh(GraphicsDevice device, IGameTime gameTime)
        {
            pointLightRenderer.Draw(device, gameTime);
        }

        public void DrawSpotLightMesh(GraphicsDevice device, IGameTime gameTime)
        {
            spotLightRenderer.Draw(device, gameTime);
        }

        public bool ProcessLight(Entity lightEntity, CameraService cameraService, InputService inputService)
        {
            if (!Contains(lightEntity))
                return false;

            var light = lightEntity.GetComponent<Light>();

            if (light == null)
            {
                return false;
            }
            

            switch (light.Type)
            {
                case LightType.Directional:
                    currentTool = DirectionalLightTool;
                    break;
                case LightType.Point:
                    currentTool = PointLightTool;
                    break;
                case LightType.Spot:
                    currentTool = SpotLightTool;
                    break;
            }

            return currentTool.Process(lightEntity, light, cameraService, inputService);
        }
    }
}
