using System;
using System.Collections.Generic;
using Adamantium.Core.DependencyInjection;
using Adamantium.Engine.Templates.Tools;
using Adamantium.Engine.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.Core;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Managers
{
    public class ToolsManager : GameManagerBase
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

        private EntityWorld entityWorld;
        private IDependencyResolver dependencyResolver;

        private GameInputManager inputManager;
        private float limitDistance = 0.06f;

        private ToolBase currentTool = null;

        bool _lightProcessingResult;
        private bool _isDraggingEnabled;
        private bool _isMoveToolEnabled;
        private bool _isRotationToolEnabled;
        private bool _isPivotToolEnabled;
        private bool _isScaleToolEnabled;
        private bool _localTransformEnabled;

        public ToolsManager(IGame game) : base(game)
        {
            DependencyResolver.RegisterInstance<ToolsManager>(this);
            CameraDragTool = new CameraDragTool(nameof(CameraDragTool));
            MoveTool = new MoveTool(false, 1.0f, new Vector3F(2));
            RotationTool = new RotationTool(false, 2.0f, new Vector3F(2));
            ScaleTool = new ScaleTool(false, 1.0f, new Vector3F(2));
            PivotTool = new PivotTool(false, 1.0f, new Vector3F(2));
            OrientationTool = new OrientationTool(100, new Vector3F(1), QuaternionF.RotationAxis(Vector3F.Right, MathHelper.DegreesToRadians(180)));
            currentTool = MoveTool;

            PlaneGridTool = new PlaneGridToolTemplate(20, 20, new Vector3F(1), 20).BuildEntity(null, "PlaneGrid");

            inputManager = DependencyResolver.Resolve<GameInputManager>();
            EntityWorld.AddToGroup(MoveTool.Tool, "Tools");
            EntityWorld.AddToGroup(PivotTool.Tool, "Tools");
            EntityWorld.AddToGroup(RotationTool.Tool, "Tools");
            EntityWorld.AddToGroup(ScaleTool.Tool, "Tools");
            EntityWorld.AddToGroup(OrientationTool.Tool, "HUD");
            EntityWorld.AddToGroup(PlaneGridTool, "Common");
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
                    currentTool = CameraDragTool;
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
                    currentTool = MoveTool;
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
                    currentTool = RotationTool;
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
                    currentTool = PivotTool;
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
                    currentTool = ScaleTool;
                }
            }
        }

        public Boolean LocalTransformEnabled
        {
            get => _localTransformEnabled;
            set => _localTransformEnabled = value;
        }

        private CollisionResult CheckEntityIntersection(IEnumerable<Entity> entities, Camera camera, Vector2F cursorPosition, CollisionMode collisionMode)
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

        public void Update(IEnumerable<Entity> entities, CameraManager cameraManager, LightManager lightManager)
        {
            CollisionMode collisionMode = CollisionMode.IgnoreNonGeometryParts;
            var camera = cameraManager.UserControlledCamera;
            if (SelectedEntity != null && !SelectedEntity.IsEnabled)
            {
                currentTool.SetStandby();
            }

            if (!currentTool.IsLocked && !_lightProcessingResult && camera != null)
            {
                result = CheckEntityIntersection(entities, camera, inputManager.RelativePosition, collisionMode);
                var lightResult = lightManager.Intersects(camera, inputManager.RelativePosition, collisionMode);
                var cameraResult = cameraManager.Intersects(camera, inputManager.RelativePosition, collisionMode);

                result.ValidateAgainst(lightResult);
                result.ValidateAgainst(cameraResult);

            }

            OrientationTool.Process(SelectedEntity, cameraManager, inputManager);

            if (currentTool.Enabled && !_lightProcessingResult)
            {
                currentTool.LocalTransformEnabled = LocalTransformEnabled;
                currentTool.Process(SelectedEntity, cameraManager, inputManager);
            }

            if (!currentTool.IsLocked)
            {
                _lightProcessingResult = lightManager.ProcessLight(SelectedEntity, cameraManager, inputManager);
            }

            if (result.Intersects && inputManager.IsMouseButtonPressed(MouseButton.Left) && !currentTool.IsLocked && !_lightProcessingResult)
            {
                if (SelectedEntity != null && SelectedEntity != result.Entity)
                {
                    SelectedEntity.IsSelected = false;
                }
                SelectedEntity = result.Entity;
                SelectedEntity.IsSelected = true;
            }
            else if (!result.Intersects && inputManager.IsMouseButtonPressed(MouseButton.Left) && !currentTool.IsLocked && !_lightProcessingResult)
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
                    foreach (var activeCamera in cameraManager.ActiveCameras)
                    {
                        current.Transform.CalculateFinalTransform(activeCamera, Vector3F.Zero);
                    }
                });

            Text = "Current selected entity: " + SelectedEntity + "\n";
        }
    }
}
