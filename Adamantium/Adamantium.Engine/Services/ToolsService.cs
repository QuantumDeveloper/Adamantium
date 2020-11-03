using System;
using Adamantium.Engine.Templates.Tools;
using Adamantium.Engine.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Game.GameInput;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Services
{
    public class ToolsService
    {
        public CameraDragTool CameraDragTool { get; private set; }

        public RotationTool RotationTool { get; private set; }

        public MoveTool MoveTool { get; private set; }

        public ScaleTool ScaleTool { get; private set; }

        public PivotTool PivotTool { get; private set; }

        public OrientationTool OrientationTool { get; private set; }

        public Entity SelectedEntity { get; set; }

        public Entity PlaneGridTool { get; set; }

        private CollisionResult result = new CollisionResult();

        public String Text { get; private set; }

        private InputService InputService;
        private float limitDistance = 0.06f;

        private TransformTool CurrentTool = null;

        private LightService lightService;
        private Game _game;

        bool _lightProcessingResult;
        private bool _isDraggingEnabled;
        private bool _isMoveToolEnabled;
        private bool _isRotationToolEnabled;
        private bool _isPivotToolEnabled;
        private bool _isScaleToolEnabled;
        private bool _localTransformEnabled;

        public ToolsService(Game game)
        {
            _game = game;
            game.Services.Add(this);
            lightService = game.LightService;
            CameraDragTool = new CameraDragTool(nameof(CameraDragTool));
            MoveTool = new MoveTool(false, 1.0f, new Vector3F(2));
            RotationTool = new RotationTool(false, 2.0f, new Vector3F(2));
            ScaleTool = new ScaleTool(false, 1.0f, new Vector3F(2));
            PivotTool = new PivotTool(false, 1.0f, new Vector3F(2));
            OrientationTool = new OrientationTool(100, new Vector3F(1), QuaternionF.RotationAxis(Vector3F.Right, MathHelper.DegreesToRadians(180)));
            CurrentTool = MoveTool;

            PlaneGridTool = new PlaneGridToolTemplate(20, 20, new Vector3F(1), 20).BuildEntity(null, "PlaneGrid");

            InputService = game.Services.Get<InputService>();
            game.EntityWorld.AddToGroup(MoveTool.Tool, "Tools");
            game.EntityWorld.AddToGroup(PivotTool.Tool, "Tools");
            game.EntityWorld.AddToGroup(RotationTool.Tool, "Tools");
            game.EntityWorld.AddToGroup(ScaleTool.Tool, "Tools");
            game.EntityWorld.AddToGroup(OrientationTool.Tool, "HUD");
            game.EntityWorld.AddToGroup(PlaneGridTool, "Common");
        }


        public Boolean IsDraggingEnabled
        {
            get => _isDraggingEnabled;
            set
            {
                _isDraggingEnabled = value;
                CameraDragTool.Enabled = value;
                if (value)
                {
                    CurrentTool = CameraDragTool;
                }
            }
        }

        public Boolean IsMoveToolEnabled
        {
            get => _isMoveToolEnabled;
            set
            {
                _isMoveToolEnabled = value;
                MoveTool.Enabled = value;
                if (value)
                {
                    CurrentTool = MoveTool;
                }
            }
        }

        public Boolean IsRotationToolEnabled
        {
            get => _isRotationToolEnabled;
            set
            {
                _isRotationToolEnabled = value;
                RotationTool.Enabled = value;
                if (value)
                {
                    CurrentTool = RotationTool;
                }
            }
        }

        public Boolean IsPivotToolEnabled
        {
            get => _isPivotToolEnabled;
            set
            {
                _isPivotToolEnabled = value;
                PivotTool.Enabled = value;
                if (value)
                {
                    CurrentTool = PivotTool;
                }
            }
        }

        public Boolean IsScaleToolEnabled
        {
            get => _isScaleToolEnabled;
            set
            {
                _isScaleToolEnabled = value;
                ScaleTool.Enabled = value;
                if (value)
                {
                    CurrentTool = ScaleTool;
                }
            }
        }

        public Boolean LocalTransformEnabled
        {
            get => _localTransformEnabled;
            set => _localTransformEnabled = value;
        }

        private CollisionResult CheckEntityIntersection(Entity[] entities, Camera camera, Vector2F cursorPosition, CollisionMode collisionMode)
        {
            CollisionResult collisionResult = new CollisionResult();
            foreach (Entity entity in entities)
            {
                if (!entity.IsEnabled)
                {
                    continue;
                }
                result = entity.Intersects(camera, cursorPosition, collisionMode, CompareOrder.Less, limitDistance);
                if (result.Intersects)
                {
                    collisionResult.ValidateAndSetValues(result.Entity, result.IntersectionPoint, true);
                }
            }
            return collisionResult;
        }

        public void Update(Entity[] entities, CameraService cameraService)
        {
            if (lightService == null)
            {
                lightService = _game.LightService;
            }

            CollisionMode collisionMode = CollisionMode.IgnoreNonGeometryParts;
            var camera = cameraService.UserControlledCamera;
            if (SelectedEntity != null && !SelectedEntity.IsEnabled)
            {
                CurrentTool.SetStandby();
            }

            if (!CurrentTool.IsLocked && !_lightProcessingResult)
            {
                result = CheckEntityIntersection(entities, camera, InputService.RelativePosition, collisionMode);
                var lightResult = lightService.Intersects(camera, InputService.RelativePosition, collisionMode);
                var cameraResult = cameraService.Intersects(camera, InputService.RelativePosition, collisionMode);

                result.ValidateAgainst(lightResult);
                result.ValidateAgainst(cameraResult);

            }

            OrientationTool.Process(SelectedEntity, cameraService, InputService);

            if (CurrentTool.Enabled && !_lightProcessingResult)
            {
                CurrentTool.LocalTransformEnabled = LocalTransformEnabled;
                CurrentTool.Process(SelectedEntity, cameraService, InputService);
            }

            if (!CurrentTool.IsLocked)
            {
                _lightProcessingResult = lightService.ProcessLight(SelectedEntity, cameraService, InputService);
            }

            if (result.Intersects && InputService.IsMouseButtonPressed(MouseButton.Left) && !CurrentTool.IsLocked && !_lightProcessingResult)
            {
                if (SelectedEntity != null && SelectedEntity != result.Entity)
                {
                    SelectedEntity.IsSelected = false;
                }
                SelectedEntity = result.Entity;
                SelectedEntity.IsSelected = true;
            }
            else if (!result.Intersects && InputService.IsMouseButtonPressed(MouseButton.Left) && !CurrentTool.IsLocked && !_lightProcessingResult)
            {
                if (SelectedEntity != null)
                {
                    SelectedEntity.IsSelected = false;
                    SelectedEntity = null;
                }
            }

            PlaneGridTool.TraverseInDepth(
                current =>
                {
                    foreach (var activeCamera in cameraService.ActiveCameras)
                    {
                        current.Transform.CalculateFinalTransform(activeCamera, Vector3F.Zero);
                    }
                });

            Text = "Current selected entity: " + SelectedEntity + "\n";
        }
    }
}
