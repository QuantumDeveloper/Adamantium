using Adamantium.Engine.GameInput;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class RotationTool: TransformTool
    {
        private float limitDistance = 0.06f;
        private Vector3F lastCoordinates;
        private readonly float coefficient = 20.0f;

        private Entity rightOrbit;
        private Entity upOrbit;
        private Entity forwardOrbit;
        private Entity currentViewManipulator;
        private Entity currentViewCircle;
        private Entity centralManipulator;


        public RotationTool(bool initialState, float diameter, Vector3F baseScale, int tesselation = 40) : base("RotationTool")
        {
            var rotationTemplate = new RotationToolTemplate(diameter, baseScale, tesselation);
            Tool = rotationTemplate.BuildEntity(null, Name);
            Enabled = initialState;

            rightOrbit = Tool.Get("RightAxisOrbit");
            upOrbit = Tool.Get("UpAxisOrbit");
            forwardOrbit = Tool.Get("ForwardAxisOrbit");
            currentViewManipulator = Tool.Get("CurrentViewManipulator");
            currentViewCircle = Tool.Get("CurrentViewCircle");
            centralManipulator = Tool.Get("CentralManipulator");
        }

        public override void Process(Entity targetEntity, CameraService cameraService, InputService inputService)
        {
            if (!CheckTargetEntity(targetEntity))
                return;

            HighlightSelectedTool(false);
            var camera = cameraService.UserControlledCamera;

            SetIsLocked(inputService);

            if (!IsLocked)
            {
                Tool.IsEnabled = true;
                UpdateToolTransform(targetEntity, cameraService, LocalTransformEnabled, false, true);

                Tool.TraverseByLayer(
                    (current) =>
                    {
                        TransformRotationTool(current, camera);
                    },
                    true);
                Transform(Tool, cameraService);

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
                }
                IsLocked = CheckIsLocked(inputService);
                ShouldStayVisible(inputService);
            }

            if (IsLocked)
            {
                HighlightSelectedTool(true);
                var intersects = GetRayPlaneIntersectionPoint(camera, inputService, out var interPoint);
                if (intersects)
                {
                    TransformEntityByRotationTool(targetEntity, camera, inputService);
                    Tool.TraverseByLayer(
                        (current) =>
                        {
                            TransformRotationTool(current, camera);
                        },
                        true);
                    Transform(Tool, cameraService);
                }
            }
        }

        private void TransformEntityByRotationTool(Entity targetEntity, Camera camera, InputService inputService)
        {
            Vector3F start = lastCoordinates;
            Vector3F end = new Vector3F(inputService.VirtualPosition);
            float radians = MathHelper.AngleBetween2D(Vector3F.Normalize(start), Vector3F.Normalize(end));
            radians /= coefficient;

            if (!float.IsNaN(radians))
            {
                if (LocalTransformEnabled)
                {
                    if (selectedTool == rightOrbit)
                    {
                        rightOrbit.Transform.RotateRight(radians);
                        upOrbit.Transform.RotateRight(radians);
                        forwardOrbit.Transform.RotateRight(radians);

                        targetEntity.Transform.RotateRight(radians);
                    }
                    else if (selectedTool == upOrbit)
                    {
                        rightOrbit.Transform.RotateUp(radians);
                        upOrbit.Transform.RotateUp(radians);
                        forwardOrbit.Transform.RotateUp(radians);

                        targetEntity.Transform.RotateUp(radians);
                    }
                    else if (selectedTool == forwardOrbit)
                    {
                        rightOrbit.Transform.RotateForward(-radians);
                        upOrbit.Transform.RotateForward(-radians);
                        forwardOrbit.Transform.RotateForward(-radians);

                        targetEntity.Transform.RotateForward(-radians);
                    }
                }
                else
                {
                    if (selectedTool == rightOrbit)
                    {
                        rightOrbit.Transform.Rotate(Vector3F.Right, radians);
                        upOrbit.Transform.Rotate(Vector3F.Right, radians);
                        forwardOrbit.Transform.Rotate(Vector3F.Right, radians);

                        targetEntity.Transform.Rotate(Vector3F.Right, radians);
                    }
                    else if (selectedTool == upOrbit)
                    {
                        rightOrbit.Transform.Rotate(Vector3F.Up, radians);
                        upOrbit.Transform.Rotate(Vector3F.Up, radians);
                        forwardOrbit.Transform.Rotate(Vector3F.Up, radians);

                        targetEntity.Transform.Rotate(Vector3F.Up, radians);
                    }
                    else if (selectedTool == forwardOrbit)
                    {
                        rightOrbit.Transform.Rotate(Vector3F.ForwardLH, -radians);
                        upOrbit.Transform.Rotate(Vector3F.ForwardLH, -radians);
                        forwardOrbit.Transform.Rotate(Vector3F.ForwardLH, -radians);

                        targetEntity.Transform.Rotate(Vector3F.ForwardLH, -radians);
                    }
                }
            }
            if (selectedTool == currentViewManipulator)
            {
                rightOrbit.Transform.Rotate(camera.Forward, - radians);
                upOrbit.Transform.Rotate(camera.Forward, -radians);
                forwardOrbit.Transform.Rotate(camera.Forward, -radians);
                targetEntity.Transform.Rotate(camera.Forward, -radians);
            }
            else if (selectedTool == centralManipulator)
            {
                var res = Vector3F.Abs(end - start);

                if (res.Y != 0)
                {
                    rightOrbit.Transform.Rotate(Vector3F.Right, -radians);
                    upOrbit.Transform.Rotate(Vector3F.Right, -radians);
                    forwardOrbit.Transform.Rotate(Vector3F.Right, -radians);
                    targetEntity.Transform.Rotate(Vector3F.Right, -radians);
                }
                else
                {
                    rightOrbit.Transform.Rotate(Vector3F.Up, radians);
                    upOrbit.Transform.Rotate(Vector3F.Up, radians);
                    forwardOrbit.Transform.Rotate(Vector3F.Up, radians);
                    targetEntity.Transform.Rotate(Vector3F.Up, radians);
                }
            }
            lastCoordinates = new Vector3F(inputService.VirtualPosition);
        }

        private void TransformRotationTool(Entity current, Camera camera)
        {
            if (current == currentViewManipulator || current == currentViewCircle) //Done
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

        protected override void HighlightSelectedTool(bool value)
        {
            if (value)
            {
                if (selectedTool != null && selectedTool != currentViewCircle)
                {
                    if (selectedTool == centralManipulator)
                    {
                        currentViewCircle.IsSelected = true;
                    }
                    else
                    {
                        selectedTool.IsSelected = true;
                    }
                }
            }
            else
            {
                if (selectedTool != null)
                {
                    selectedTool.IsSelected = false;
                    if (selectedTool == centralManipulator)
                    {
                        currentViewCircle.IsSelected = false;
                    }
                }
            }
        }
    }
}
