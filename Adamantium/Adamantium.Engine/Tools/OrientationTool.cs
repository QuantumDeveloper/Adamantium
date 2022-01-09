using System;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.GameInput;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class OrientationTool: TransformTool
    {
        private Entity rightAxisManipulator;
        private Entity leftAxisManipulator;
        private Entity upAxisManipulator;
        private Entity downAxisManipulator;
        private Entity forwardAxisManipulator;
        private Entity backwardAxisManipulator;
        private Entity centralManipulator;

        public OrientationTool(float size, Vector3F baseScale, QuaternionF initialRotation) : base("OrientationTool")
        {
            var orientationTemplate = new OrientationToolTemplate(size, baseScale, initialRotation);
            Tool = orientationTemplate.BuildEntity(null, Name);

            rightAxisManipulator = Tool.Get("RightAxisManipulator");
            leftAxisManipulator = Tool.Get("LeftAxisManipulator");
            upAxisManipulator = Tool.Get("UpAxisManipulator");
            downAxisManipulator = Tool.Get("DownAxisManipulator");
            forwardAxisManipulator = Tool.Get("ForwardAxisManipulator");
            backwardAxisManipulator = Tool.Get("BackwardAxisManipulator");
            centralManipulator = Tool.Get("CentralManipulator");
        }

        public override void Process(Entity targetEntity, CameraService cameraService, InputService inputService)
        {
            Tool.TraverseByLayer((current) =>
            {
                var colliders = current.GetComponents<Collider>();
                foreach (var camera in cameraService.ActiveCameras)
                {
                    TransformOrientationTool(current, camera);
                    for (int i = 0; i < colliders.Length; ++i)
                    {
                        colliders[i].ClearData();
                        for (int j = 0; j < colliders.Length; ++j)
                        {
                            colliders[j].UpdateForCamera(camera);
                        }
                    }

                    UpdateOrientationToolAxisVisibility(current, camera);

                    current.Transform.GetMetadata(camera).IsSelected = false;

                    if (camera == cameraService.UserControlledCamera)
                    {
                        toolIntersectionResult = Tool.Intersects(camera, inputService.RelativePosition, false, camera.UiProjection, CollisionMode.IgnoreNonGeometryParts, CompareOrder.Greater, 0, false);

                        if (toolIntersectionResult.Intersects)
                        {
                            toolIntersectionResult.Entity.Transform.GetMetadata(camera).IsSelected = true;
                        }
                    }
                }
            }, true);

            if (inputService.IsMouseButtonPressed(MouseButton.Left) && toolIntersectionResult.Intersects)
            {
                selectedTool = toolIntersectionResult.Entity;
                HandleMouseClick(cameraService.UserControlledCamera);
            }
        }

        private void TransformOrientationTool(Entity current, Camera camera)
        {
            Matrix4x4F world;
            var scaling = current.Transform.Scale;
            var relativePosition = new Vector3F(camera.Width - 60, 60, 150f);
            var finalPivot = Vector3F.Zero;
            var orientation = QuaternionF.Conjugate(camera.Rotation);
            orientation.X = -orientation.X;

            QuaternionF rot = QuaternionF.Identity;
            Vector3F zero = Vector3F.Zero;
            Matrix4x4F.Transformation(
                ref zero,
                ref rot,
                ref scaling,
                ref finalPivot,
                ref orientation,
                ref relativePosition,
                out world);
            var metadata = current.Transform.GetMetadata(camera);
            metadata.WorldMatrixF = world;
            metadata.RelativePosition = relativePosition;
        }

        private void HandleMouseClick(Camera camera)
        {
            var rotation = QuaternionF.Identity;
            if (selectedTool == rightAxisManipulator)
            {
                rotation = QuaternionF.RotationLookAtLH(Vector3F.Left, Vector3F.Up);
            }
            else if (selectedTool == leftAxisManipulator)
            {
                rotation = QuaternionF.RotationLookAtLH(Vector3F.Right, Vector3F.Up);
            }
            else if (selectedTool == forwardAxisManipulator)
            {
                rotation = QuaternionF.RotationLookAtLH(Vector3F.BackwardLH, Vector3F.Up);
            }
            else if (selectedTool == backwardAxisManipulator)
            {
                rotation = QuaternionF.RotationLookAtLH(Vector3F.ForwardLH, Vector3F.Up);
            }
            else if (selectedTool == upAxisManipulator)
            {
                rotation = QuaternionF.RotationLookAtLH(Vector3F.Down, Vector3F.ForwardLH);
            }
            else if (selectedTool == downAxisManipulator)
            {
                rotation = QuaternionF.RotationLookAtLH(Vector3F.Up, Vector3F.BackwardLH);
            }
            camera.RotateAroundSelectedObject(rotation, 500);
        }

        private void UpdateOrientationToolAxisVisibility(Entity current, Camera camera)
        {
            var transformData = current.Transform.GetMetadata(camera);
            float dotProduct = 0;
            if (current == rightAxisManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transformData.WorldMatrixF.Right), Vector3F.ForwardLH));
                transformData.Enabled = !(dotProduct >= 0.85);
            }

            else if (current == leftAxisManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transformData.WorldMatrixF.Left), Vector3F.ForwardLH));
                transformData.Enabled = !(dotProduct >= 0.85);
            }

            else if (current == upAxisManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transformData.WorldMatrixF.Up), Vector3F.ForwardLH));
                transformData.Enabled = !(dotProduct >= 0.85);
            }
            else if (current == downAxisManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transformData.WorldMatrixF.Down), Vector3F.ForwardLH));
                transformData.Enabled = !(dotProduct >= 0.85);
            }
            else if (current == forwardAxisManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transformData.WorldMatrixF.Backward), Vector3F.ForwardLH));
                transformData.Enabled = !(dotProduct >= 0.85);
            }
            else if (current == backwardAxisManipulator)
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transformData.WorldMatrixF.Forward), Vector3F.ForwardLH));
                transformData.Enabled = !(dotProduct >= 0.85);
            }
        }
    }
}
