using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class MoveTool: ToolBase
    {
        private Vector3F toolDelta;

        private const string RightAxisManipulatorName = "RightAxisManipulator";
        private const string UpAxisManipulatorName = "UpAxisManipulator";
        private const string ForwardAxisManipulatorName = "ForwardAxisManipulator";
        private const string RightForwardManipulatorName = "RightForwardManipulator";
        private const string UpForwardManipulatorName = "UpForwardManipulator";
        private const string RightUpManipulatorName = "RightUpManipulator";
        private const string CentralManipulatorName = "CentralManipulator";

        private readonly Entity rightManipulator;
        private readonly Entity upManipulator;
        private readonly Entity forwardManipulator;
        private readonly Entity rightUpManipulator;
        private readonly Entity rightForwardManipulator;
        private readonly Entity upForwardManipulator;
        private readonly Entity centralManipulator;

        public MoveTool(bool initialState, float axisLength, Vector3F baseScale, int tessellation = 20) :base(nameof(MoveTool))
        {
            var moveTemplate = new MoveToolTemplate(axisLength, baseScale, tessellation);
            Tool = moveTemplate.BuildEntity(null, Name);
            Enabled = initialState;

            rightManipulator = Tool.Get(RightAxisManipulatorName);
            upManipulator = Tool.Get(UpAxisManipulatorName);
            forwardManipulator = Tool.Get(ForwardAxisManipulatorName);

            rightUpManipulator = Tool.Get(RightUpManipulatorName);
            rightForwardManipulator = Tool.Get(RightForwardManipulatorName);
            upForwardManipulator = Tool.Get(UpForwardManipulatorName);

            centralManipulator = Tool.Get(CentralManipulatorName);
        }

        public override void Process(Entity targetEntity, CameraManager cameraManager, GameInputManager inputManager)
        {
            if (!CheckTargetEntity(targetEntity))
                return;

            HighlightSelectedTool(false);
            var camera = cameraManager.UserControlledCamera;

            if (inputManager.IsMouseButtonReleased(MouseButton.Left))
            {
                IsLocked = false;
                ResetVisibility(true);
            }

            if (!IsLocked)
            {
                Tool.IsEnabled = true;
                UpdateToolTransform(targetEntity, cameraManager, LocalTransformEnabled, true, true);
                var collisionMode = CollisionMode.Mixed;

                toolIntersectionResult = Tool.Intersects(
                    camera,
                    inputManager.VirtualPosition,
                    collisionMode,
                    CompareOrder.Less, 
                    0.05f);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    previousCoordinates = toolIntersectionResult.IntersectionPoint;
                    HighlightSelectedTool(true);
                    toolDelta = selectedTool.GetRelativePosition(camera) - toolIntersectionResult.IntersectionPoint;
                }

                IsLocked = CheckIsLocked(inputManager);

                if (IsLocked)
                {
                    SetToolVisibility();
                }
                else
                {
                    ShouldStayVisible(inputManager);
                }
            }

            if (IsLocked && Enabled)
            {
                HighlightSelectedTool(true);
                var intersects = GetRayPlaneIntersectionPoint(camera, inputManager, out var interPoint);
                if (intersects)
                {
                    TransformEntityPosition(targetEntity, camera, interPoint);
                }
            }

            Tool.TraverseByLayer(
                current =>
                {
                    UpdateAxisVisibility(current, camera);
                },
                true);
            Transform(Tool, cameraManager);
        }

        private void TransformEntityPosition(Entity entityToTransform, Camera camera, Vector3F rayPlaneInterPoint)
        {
            var absolute = camera.Owner.Transform.Position + rayPlaneInterPoint + toolDelta;
            var distance = absolute - selectedTool.Transform.Position;
            if (LocalTransformEnabled)
            {
                if (selectedTool == rightManipulator)
                {
                    entityToTransform.Transform.TranslateRight(distance.X);
                }
                else if (selectedTool == upManipulator)
                {
                    entityToTransform.Transform.TranslateUp(distance.Y);
                }
                else if (selectedTool == forwardManipulator)
                {
                    entityToTransform.Transform.TranslateForward(-distance.Z);
                }
                else if (selectedTool == rightForwardManipulator)
                {
                    var matr = entityToTransform.Transform.GetRotationMatrix();
                    var res = Vector3.Normalize(matr.Right + matr.Forward);
                    distance.Y = 0;
                    distance.Z = -distance.Z;
                    entityToTransform.Transform.Translate(res, distance);
                }
                else if (selectedTool == upForwardManipulator)
                {
                    var matr = entityToTransform.Transform.GetRotationMatrix();
                    var res = Vector3.Normalize(matr.Forward + matr.Up);
                    distance.X = 0;
                    distance.Z = -distance.Z;
                    entityToTransform.Transform.Translate(res, distance);
                }
                else if (selectedTool == rightUpManipulator)
                {
                    var matr = entityToTransform.Transform.GetRotationMatrix();
                    var res = Vector3.Normalize(matr.Right + matr.Up);
                    distance.Z = 0;
                    entityToTransform.Transform.Translate(res, distance);
                }
                else if (selectedTool == centralManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.Normalize(Vector3.One), distance);
                }
            }
            else
            {
                if (selectedTool == rightManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.Right, distance.X);
                }
                else if (selectedTool == upManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.Up, distance.Y);
                }
                else if (selectedTool == forwardManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.ForwardLH, distance.Z);
                }
                else if (selectedTool == rightForwardManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.ForwardLH, distance.Z);
                    entityToTransform.Transform.Translate(Vector3.Right, distance.X);
                }
                else if (selectedTool == upForwardManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.ForwardLH, distance.Z);
                    entityToTransform.Transform.Translate(Vector3.Up, distance.Y);
                }
                else if (selectedTool == rightUpManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.Right, distance.X);
                    entityToTransform.Transform.Translate(Vector3.Up, distance.Y);
                }
                else if (selectedTool == centralManipulator)
                {
                    entityToTransform.Transform.Translate(Vector3.Normalize(Vector3.One), distance);
                }
            }
            var center = entityToTransform.GetCenterAbsolute();
            Tool.Transform.SetPosition(center);

            previousCoordinates = rayPlaneInterPoint;
        }

        protected override void HighlightSelectedTool(bool value)
        {
            if (selectedTool == null)
            {
                return;
            }

            selectedTool.TraverseByLayer(
                current =>
                {
                    current.IsSelected = value;
                }, true);

            if (selectedTool == rightForwardManipulator)
            {
                rightManipulator.Owner.TraverseByLayer(current=> { current.IsSelected = value; }, true);
                forwardManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
            }
            else if (selectedTool == rightUpManipulator)
            {
                rightManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
                upManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
            }
            else if (selectedTool == upForwardManipulator)
            {
                upManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
                forwardManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
            }

            else if (selectedTool == rightManipulator)
            {
                selectedTool.Owner.IsSelected = value;
            }

            else if (selectedTool == upManipulator)
            {
                selectedTool.Owner.IsSelected = value;
            }

            else if (selectedTool == forwardManipulator)
            {
                selectedTool.Owner.IsSelected = value;
            }
        }

        protected override void SetToolVisibility()
        {
            bool value = true;

            if (selectedTool == null)
            {
                return;
            }

            ResetVisibility(selectedTool == centralManipulator);

            if (selectedTool == rightForwardManipulator || selectedTool == rightUpManipulator || selectedTool == upForwardManipulator)
            {
                selectedTool.Visible = value;
                rightManipulator.Owner.TraverseByLayer(current => { current.Visible = value; }, true);
                upManipulator.Owner.TraverseByLayer(current => { current.Visible = value; }, true);
                forwardManipulator.Owner.TraverseByLayer(current => { current.Visible = value; }, true);
            }

            else if (selectedTool == rightManipulator || selectedTool == rightManipulator.Owner)
            {
                rightManipulator.Owner.TraverseInDepth(current=> { current.Visible = value; }, true);
            }

            else if (selectedTool == upManipulator || selectedTool == upManipulator.Owner)
            {
                upManipulator.Owner.TraverseInDepth(current => { current.Visible = value; }, true);
            }

            else if (selectedTool == forwardManipulator || selectedTool == forwardManipulator.Owner)
            {
                forwardManipulator.Owner.TraverseInDepth(current => { current.Visible = value; }, true);
            }

            centralManipulator.Visible = true;
        }
    }
}
