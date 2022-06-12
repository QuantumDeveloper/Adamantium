﻿using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class CameraDragTool: ToolBase
    {
        private Vector3F dragStart;

        public CameraDragTool(string name) : base(name)
        {
        }

        public override void Process(Entity targetEntity, CameraManager cameraManager, GameInputManager inputManager)
        {
            var camera = cameraManager.UserControlledCamera;

            SetIsLocked(inputManager);

            if (!IsLocked)
            {
                var nearPoint = Vector3F.Unproject(new Vector3F(inputManager.RelativePosition.X, inputManager.RelativePosition.Y, 1), 0, 0, camera.Width, camera.Height, 0, 1, camera.ViewMatrix * camera.ProjectionMatrix);
                var farPoint = Vector3F.Unproject(new Vector3F(inputManager.RelativePosition.X, inputManager.RelativePosition.Y, 0), 0, 0, camera.Width, camera.Height, 0, 1, camera.ViewMatrix * camera.ProjectionMatrix);
                var direction = farPoint - nearPoint;
                direction.Normalize();
                var ray = new Ray(nearPoint, direction);
                var planePosition = camera.Forward * 100;
                var plane = new Plane(planePosition, camera.Forward);
                toolIntersectionResult.Intersects = ray.Intersects(ref plane, out Vector3F interPoint);
                toolIntersectionResult.IntersectionPoint = (Vector3)interPoint;
                if (toolIntersectionResult.Intersects)
                {
                    dragStart = interPoint;
                }

                IsLocked = CheckIsLocked(inputManager);
            }

            if (IsLocked && Enabled)
            {
                var intersects = GetRayPlaneIntersectionPoint(camera, inputManager, out var interPoint);
                if (intersects)
                {
                    MoveCamera(camera, interPoint);
                }
            }
        }

        private void MoveCamera(Camera camera, Vector3F endPoint)
        {
            if (dragStart == endPoint)
            {
                return;
            }
            var diff = endPoint - dragStart;
            var matr = Matrix4x4F.RotationQuaternion(camera.Rotation);

            diff = Vector3F.TransformCoordinate(diff, matr);
            diff.X = -diff.X;
            diff.Y = -diff.Y;
            camera.TranslateRight(diff.X);
            camera.TranslateUp(diff.Y);
            dragStart = endPoint;
        }
    }
}
