using System;
using Adamantium.Engine.Managers;
using Adamantium.Engine.Services;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.Core.Input;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public abstract class ToolBase
    {
        public string Name { get; }

        public bool IsLocked { get; protected set; }

        protected CollisionResult toolIntersectionResult;
        protected float toolScale;
        protected Entity selectedTool;
        protected Vector3F? previousCoordinates = Vector3F.Zero;
        private bool enabled;

        public Entity Tool { get; protected set; }

        public bool LocalTransformEnabled { get; set; }

        protected ToolBase(string name)
        {
            Name = name;
            toolIntersectionResult = new CollisionResult();
        }

        public bool Enabled
        {
            get => enabled;
            set
            {
                enabled = value;
                if (!value && Tool != null)
                {
                    Tool.IsEnabled = false;
                }
                OnEnabledChanged(value);
            }
        }

        public void SetStandby()
        {
            Tool.IsEnabled = false;
        }

        protected virtual bool CheckTargetEntity(Entity targetEntity)
        {
            if (targetEntity == null)
            {
                Tool.IsEnabled = false;
                return false;
            }
            return true;
        }

        protected virtual void OnEnabledChanged(bool newState)
        {
            if (!newState)
            {
                IsLocked = false;
                HighlightSelectedTool(false);
            }
        }

        protected virtual void HighlightSelectedTool(bool value)
        {
            if (selectedTool != null)
            {
                selectedTool.IsSelected = value;
            }
        }

        protected virtual void SetToolVisibility() { }

        protected void ResetVisibility(bool value)
        {
            Tool.TraverseByLayer(
                current =>
                {
                    current.Visible = value;
                }, true);

            Tool.Visible = true;
        }

        protected bool CheckIsLocked(GameInputManager inputManager)
        {
            return inputManager.IsMouseButtonPressed(MouseButton.Left) && toolIntersectionResult.Intersects;
        }

        protected void ShouldStayVisible(GameInputManager inputManager)
        {
            if (inputManager.IsMouseButtonPressed(MouseButton.Left) && !toolIntersectionResult.Intersects)
            {
                Tool.IsEnabled = false;
                IsLocked = false;
            }
        }

        protected void SetIsLocked(GameInputManager inputManager)
        {
            if (inputManager.IsMouseButtonReleased(MouseButton.Left))
            {
                IsLocked = false;
            }
        }

        public abstract void Process(Entity targetEntity, CameraManager cameraManager, GameInputManager inputManager);

        protected bool GetRayPlaneIntersectionPoint(Camera camera, GameInputManager inputManager, out Vector3 intersectionPoint)
        {
            var p = new Plane(toolIntersectionResult.IntersectionPoint, camera.Forward);
            var ray = Collisions.CalculateRay(inputManager.VirtualPosition, camera, Matrix4x4F.Identity);
            var intersects = ray.Intersects(ref p, out Vector3F interPoint);
            intersectionPoint = (Vector3)interPoint;
            if (previousCoordinates == intersectionPoint)
            {
                return false;
            }
            previousCoordinates = intersectionPoint;

            return intersects;
        }

        protected void Transform(Entity entity, CameraManager cameraManager)
        {
            entity.TraverseInDepth(current =>
            {
                foreach (var activeCamera in cameraManager.ActiveCameras)
                {
                    current.Transform.CalculateFinalTransform(activeCamera, Vector3F.Zero);
                }
            }, true);
        }

        protected virtual void UpdateAxisVisibility(Entity current, Camera camera)
        {
            float dotProduct = 0;
            var transform = current.GetActualMatrixF(camera);
            if (current.Name == "RightAxisManipulator")
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Right), camera.Forward));
                current.IsEnabled = !(dotProduct >= 0.97);
                current.Owner.IsEnabled = current.IsEnabled;
            }
            else if (current.Name == "UpAxisManipulator")
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Up), camera.Forward));
                current.IsEnabled = !(dotProduct >= 0.97);
                current.Owner.IsEnabled = current.IsEnabled;
            }
            else if (current.Name == "ForwardAxisManipulator")
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Forward), camera.Forward));
                current.IsEnabled = !(dotProduct >= 0.97);
                current.Owner.IsEnabled = current.IsEnabled;
            }
            else if (current.Name.StartsWith("RightForwardManipulator"))
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Up), camera.Forward));
                current.IsEnabled = dotProduct >= 0.05;
            }
            else if (current.Name.StartsWith("UpForwardManipulator"))
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Right), camera.Forward));
                current.IsEnabled = dotProduct >= 0.05;
            }
            else if (current.Name.StartsWith("RightUpManipulator"))
            {
                dotProduct = Math.Abs(Vector3F.Dot(Vector3F.Normalize(transform.Forward), camera.Forward));
                current.IsEnabled = dotProduct >= 0.05;
            }
        }

        protected virtual void UpdateToolTransform(Entity target, CameraManager cameraManager, bool isLocalAxis, bool useTargetCenter, bool calculateTransform)
        {
            var camera = cameraManager.UserControlledCamera;
            // Set tool to the local coordinates center of the target Entity (not geometrical center)
            if (useTargetCenter)
            {
                Tool.TraverseByLayer(current =>
                {
                    current.Transform.Position = target.GetCenterAbsolute();
                }, true);
            }
            else
            {
                Tool.TraverseByLayer(current =>
                {
                    current.Transform.Position = target.Transform.Pivot;
                }, true);
            }
            
            if (isLocalAxis)
            {
                Tool.TraverseByLayer(current =>
                {
                    current.Transform.Rotation = target.Transform.Rotation;
                }, true);
            }
            else
            {
                Tool.TraverseByLayer(current =>
                {
                    current.Transform.Rotation = QuaternionF.Identity;
                }, true);
            }

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
