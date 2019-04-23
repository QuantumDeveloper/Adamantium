using System;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class PivotTool: TransformTool
    {
        private Vector3F toolDelta;
        private float limitDistance = 0.06f;
        private Vector3F lastCoordinates;

        private const string MoveRightAxisName = "MoveRight";
        private const string MoveUpAxisName = "MoveUp";
        private const string MoveForwardAxisName = "MoveForward";
        private const string MoveRightManipulatorName = "MoveRightManipulator";
        private const string MoveUpManipulatorName = "MoveUpManipulator";
        private const string MoveForwardManipulatorName = "MoveForwardManipulator";
        private const string RotateRightManipulatorName = "RightOrbit";
        private const string RotateUpManipulatorName = "UpOrbit";
        private const string RotateForwardManipulatorName = "ForwardOrbit";
        private const string CentralManipulatorName = "CentralManipulator";
        private const string PivotPointName = "PivotPoint";

        private readonly Entity moveRight;
        private readonly Entity moveUp;
        private readonly Entity moveForward;
        private readonly Entity moveRightManipulator;
        private readonly Entity moveUpManipulator;
        private readonly Entity moveForwardManipulator;
        private readonly Entity rotateRightOrbit;
        private readonly Entity rotateUpOrbit;
        private readonly Entity rotateForwardOrbit;
        private readonly Entity centralManipulator;
        private readonly Entity pivotPoint;

        public PivotTool(bool initialState, float axisLength, Vector3F baseScale, int tessellation = 40) : base("PivotTool")
        {
            var moveTemplate = new PivotToolTemplate(axisLength, baseScale, tessellation);
            Tool = moveTemplate.BuildEntity(null, Name);
            Enabled = initialState;

            moveRight = Tool.Get(MoveRightAxisName);
            moveUp = Tool.Get(MoveUpAxisName);
            moveForward = Tool.Get(MoveForwardAxisName);

            moveRightManipulator = Tool.Get(MoveRightManipulatorName);
            moveUpManipulator = Tool.Get(MoveUpManipulatorName);
            moveForwardManipulator = Tool.Get(MoveForwardManipulatorName);

            rotateRightOrbit = Tool.Get(RotateRightManipulatorName);
            rotateUpOrbit = Tool.Get(RotateUpManipulatorName);
            rotateForwardOrbit = Tool.Get(RotateForwardManipulatorName);

            centralManipulator = Tool.Get(CentralManipulatorName);

            pivotPoint = Tool.Get(PivotPointName);
        }

        public override void Process(Entity targetEntity, CameraService cameraService, InputService inputService)
        {
            if (!CheckTargetEntity(targetEntity))
                return;

            HighlightSelectedTool(false);
            var camera = cameraService.UserControlledCamera;

            if (inputService.IsMouseButtonReleased(MouseButton.Left))
            {
                IsLocked = false;
                ResetVisibility(true);
            }

            if (!IsLocked)
            {
                Tool.IsEnabled = true;
                UpdateToolTransform(targetEntity, cameraService, false, true, false);
                Tool.Transform.SetRotation(targetEntity.Transform.PivotRotation);
                Tool.TraverseByLayer(
                    current =>
                    {
                        TransformCentralToolPart(current, camera);
                        UpdateAxisVisibility(current, camera);
                    },
                    true);

                var collisionMode = CollisionMode.Mixed;

                toolIntersectionResult = Tool.Intersects(
                    camera,
                    inputService.RelativePosition,
                    collisionMode,
                    CompareOrder.Less,
                    limitDistance);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    lastCoordinates = toolIntersectionResult.IntersectionPoint;
                    HighlightSelectedTool(true);
                    toolDelta = selectedTool.GetRelativePosition(camera) - toolIntersectionResult.IntersectionPoint;
                }

                IsLocked = CheckIsLocked(inputService);

                if (IsLocked)
                {
                    SetToolVisibility();
                }
                else
                {
                    ShouldStayVisible(inputService);
                }
            }

            if (IsLocked && Enabled)
            {
                HighlightSelectedTool(true);
                Vector3F interPoint;
                var intersects = GetRayPlaneIntersectionPoint(camera, inputService, out interPoint);
                if (intersects)
                {
                    Tool.TraverseByLayer(
                        current =>
                        {
                            TransformCentralToolPart(current, camera);
                        },
                        true);
                    TransformEntityPivot(targetEntity, camera, interPoint);
                }
            }

            Transform(Tool, cameraService);
        }

        protected override void UpdateAxisVisibility(Entity current, Camera camera)
        {
            float dotProduct = 0;
            var transform = current.GetActualMatrix(camera);
            if (current == moveRightManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Right), camera.Forward));
                current.Visible = !(dotProduct >= 0.97);
                current.Owner.Visible = current.Visible;
            }
            else if (current == moveUpManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Up), camera.Forward));
                current.Visible = !(dotProduct >= 0.97);
                current.Owner.Visible = current.Visible;
            }
            else if (current == moveForwardManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Forward), camera.Forward));
                current.Visible = !(dotProduct >= 0.97);
                current.Owner.Visible = current.Visible;
            }
        }

        private void TransformCentralToolPart(Entity current, Camera camera)
        {
            if (current== pivotPoint || current == centralManipulator)
            {
                var billboardMatrix = Matrix4x4F.BillboardLH(
                    current.GetRelativePosition(camera),
                    Vector3F.Zero,
                    camera.Up,
                    camera.Forward);
                var rot = MathHelper.GetRotationFromMatrix(billboardMatrix);
                current.Transform.SetRotation(rot);
            }
        }

        private void TransformEntityPivot(Entity entityToTransform, Camera camera, Vector3F rayPlaneInterPoint)
        {
            var center = Tool.GetRelativePosition(camera);
            Vector3F start = lastCoordinates - center;
            Vector3F end = rayPlaneInterPoint - center;

            float radians = MathHelper.AngleBetween(Vector3F.Normalize(start), Vector3F.Normalize(end), camera.Forward);

            var absolute = camera.GetOwnerPosition() + rayPlaneInterPoint + toolDelta;
            var distance = absolute - selectedTool.Transform.Position;
            var world = Matrix4x4F.RotationQuaternion(entityToTransform.Transform.PivotRotation);
            if (selectedTool == moveRight || selectedTool == moveRightManipulator)
            {
                entityToTransform.Transform.TranslatePivot(world.Right, distance.X);
            }
            else if (selectedTool == moveUp || selectedTool == moveUpManipulator)
            {
                entityToTransform.Transform.TranslatePivot(world.Up, distance.Y);
            }
            else if (selectedTool == moveForward || selectedTool == moveForwardManipulator)
            {
                entityToTransform.Transform.TranslatePivot(world.Backward, distance.Z);
            }
            else if (selectedTool == centralManipulator)
            {
                entityToTransform.Transform.TranslatePivot(Vector3F.Normalize(Vector3F.One), distance);
            }

            if (selectedTool == rotateRightOrbit)
            {
                entityToTransform.Transform.RotatePivotRight(radians);
                Tool.Transform.RotateRight(radians);
            }
            else if (selectedTool == rotateUpOrbit)
            {
                entityToTransform.Transform.RotatePivotUp(radians);
                Tool.Transform.RotateUp(radians);
            }
            else if (selectedTool == rotateForwardOrbit)
            {
                entityToTransform.Transform.RotatePivotForward(-radians);
                Tool.Transform.RotateForward(-radians);
            }

            Tool.TraverseByLayer(
                current =>
                {
                    TransformCentralToolPart(current, camera);
                },
                true);

            center = entityToTransform.GetCenterAbsolute();
            Tool.Transform.SetPosition(center);

            lastCoordinates = rayPlaneInterPoint;
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

            if (selectedTool == moveRightManipulator)
            {
                moveRightManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
            }
            else if (selectedTool == moveUpManipulator)
            {
                moveUpManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
            }
            else if (selectedTool == moveForwardManipulator)
            {
                moveForwardManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
            }
        }

        protected override void SetToolVisibility()
        {
            if (selectedTool == null)
            {
                return;
            }

            var value = true;

            ResetVisibility(true);

            if (selectedTool == moveRightManipulator || selectedTool == moveRight)
            {
                moveRight.TraverseInDepth(current => { current.Visible = value; }, true);
                moveUpManipulator.Owner.TraverseByLayer(current => { current.Visible = false; }, true);
                moveForwardManipulator.Owner.TraverseByLayer(current => { current.Visible = false; }, true);
            }
            else if (selectedTool == moveUpManipulator || selectedTool == moveUp)
            {
                moveUp.TraverseInDepth(current => { current.Visible = value; }, true);
                moveRightManipulator.Owner.TraverseByLayer(current => { current.Visible = false; }, true);
                moveForwardManipulator.Owner.TraverseByLayer(current => { current.Visible = false; }, true);
            }
            else if (selectedTool == moveForwardManipulator || selectedTool == moveForward)
            {
                moveForward.TraverseInDepth(current => { current.Visible = value; }, true);
                moveRightManipulator.Owner.TraverseByLayer(current => { current.Visible = false; }, true);
                moveUpManipulator.Owner.TraverseByLayer(current => { current.Visible = false; }, true);
            }
        }

        protected virtual void UpdateToolTransform(Entity target, CameraService cameraService, bool isLocalAxis, bool useTargetCenter, bool calculateTransform)
        {
            var camera = cameraService.UserControlledCamera;
            // Set tool to the local coordinates center of the target Entity (not geometrical center)
            Tool.TraverseByLayer(current =>
            {
                current.Transform.Position = target.GetCenterAbsolute();
            }, true);

            Tool.TraverseByLayer(current =>
            {
                current.Transform.Rotation = target.Transform.PivotRotation;
            }, true);

            var value = target.GetRelativePosition(camera).Length() * MathHelper.DegreesToRadians(camera.Fov) * camera.AspectRatio * 0.01f;
            if (!MathHelper.IsZero(value))
            {
                Tool.Transform.SetScaleFactor(value);
            }

            if (calculateTransform)
            {
                Tool.TraverseInDepth(
                    current =>
                    {
                        current.Transform.CalculateFinalTransform(camera, Vector3F.Zero);
                    });
            }
        }
    }
}
