using System;
using System.Diagnostics;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Lights;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class PointLightTool : LightTool
    {
        private Vector3F _scale;
        private Entity anchorRight;
        private Entity anchorForward;
        private Entity anchorLeft;
        private Entity anchorBackward;
        private Entity anchorUp;
        private Entity anchorDown;
        private float _minRadius = 0.01f;

        private float _previousScaleFactor;
        private Vector2F startPosition;

        public PointLightTool(string name) : base(name)
        {
            Tool = new PointLightVisualTemplate().BuildEntity(null, "Point");
            anchorRight = Tool.Get("AnchorPointRight");
            anchorForward = Tool.Get("AnchorPointForward");
            anchorLeft = Tool.Get("AnchorPointLeft");
            anchorBackward = Tool.Get("AnchorPointBackward");
            anchorUp = Tool.Get("AnchorPointUp");
            anchorDown = Tool.Get("AnchorPointDown");
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
                UpdateToolTransform(targetEntity, cameraService, false, true, true);

                var collisionMode = CollisionMode.CollidersOnly;

                toolIntersectionResult = Tool.Intersects(
                    camera,
                    inputService.VirtualPosition,
                    collisionMode,
                    CompareOrder.Less,
                    0.05f);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    previousCoordinates = toolIntersectionResult.IntersectionPoint;
                    _previousScaleFactor = CurrentLight.Range;
                    HighlightSelectedTool(true);
                }

                IsLocked = CheckIsLocked(inputService);

                if (IsLocked)
                {
                    startPosition = inputService.VirtualPosition;
                    HighlightSelectedTool(true);
                }
                else
                {
                    ShouldStayVisible(inputService);
                }
            }

            if (IsLocked && Enabled)
            {
                HighlightSelectedTool(true);
                var intersects = GetRayPlaneIntersectionPoint(camera, inputService, out var interPoint);
                if (intersects)
                {
                    ScalePointLight(targetEntity, inputService, interPoint);
                }
            }

            Transform(Tool, cameraService);
        }

        private void ScalePointLight(Entity lightToTransform, InputService input, Vector3F rayPlaneInterPoint)
        {
            var res = input.VirtualPosition / startPosition;
            
            if (!float.IsInfinity(_scale.X) && !float.IsInfinity(_scale.Y) && !float.IsInfinity(_scale.Z))
            {
                //var avg = Vector2F.Max(res);
                var avg = Vector2F.Average(res);

                _scale.X = _scale.Y = _scale.Z = avg;
            }


            if (!float.IsNaN(_scale.X) && !float.IsInfinity(_scale.X) && !MathHelper.IsZero(_scale.X))
            {
                if (_scale.X < MinimumAllowedRange)
                {
                    _scale.X = MinimumAllowedRange;
                }

                CurrentLight.Range = _previousScaleFactor * _scale.X;
                selectedTool.Owner.Transform.ScaleFactor = new Vector3F(CurrentLight.Range);
            }

            KeepScaleForAnchors();
            PositionAnchors(lightToTransform);
        }

        private void KeepScaleForAnchors()
        {
            anchorRight.Transform.SetScaleFactor(toolScale);
            anchorForward.Transform.SetScaleFactor(toolScale);
            anchorLeft.Transform.SetScaleFactor(toolScale);
            anchorBackward.Transform.SetScaleFactor(toolScale);
            anchorUp.Transform.SetScaleFactor(toolScale);
            anchorDown.Transform.SetScaleFactor(toolScale);
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
                    current.IsSelected = false;
                },
                true);

            selectedTool.IsSelected = value;
        }

        protected override void UpdateToolTransform(Entity target, CameraService cameraService, bool isLocalAxis, bool useTargetCenter, bool calculateTransform)
        {
            var camera = cameraService.UserControlledCamera;
            // Set tool to the local coordinates center of the target Entity (not geometrical center)
            if (useTargetCenter)
            {
                Tool.TraverseByLayer(
                    current =>
                    {
                        current.Transform.Position = target.GetCenterAbsolute();
                    },
                    true);
            }
            else
            {
                Tool.TraverseByLayer(
                    current =>
                    {
                        current.Transform.Position = target.Transform.Pivot;
                    },
                    true);
            }

            Tool.TraverseByLayer(
                current =>
                {
                    current.Transform.Rotation = target.Transform.Rotation;
                    current.Transform.ScaleFactor = new Vector3F(CurrentLight.Range);
                },
                true);
            toolScale = target.GetRelativePosition(camera).Length() * MathHelper.DegreesToRadians(camera.Fov) * camera.AspectRatio * 0.1f;
            KeepScaleForAnchors();
            PositionAnchors(target);

            if (calculateTransform)
            {
                Tool.TraverseByLayer(
                    current =>
                    {
                        current.Transform.CalculateFinalTransform(camera, Vector3F.Zero);
                    });
            }
        }

        private void PositionAnchors(Entity targetEntity)
        {
            var scale = new Vector3F(CurrentLight.Range);
            var position = targetEntity.Transform.Position;
            var rot = targetEntity.Transform.GetRotationMatrix();

            var rightPos = position + (((scale.X) / 2 - 0.5f * toolScale) * rot.Right);
            var forwardPos = position + ((scale.X / 2 - 0.5f * toolScale) * rot.Forward);
            var leftPos = position + ((scale.X / 2 - 0.5f * toolScale) * rot.Left);
            var backwardPos = position + ((scale.X / 2 - 0.5f * toolScale) * rot.Backward);
            var upPos = position + ((scale.X / 2 - 0.5f * toolScale) * rot.Up);
            var downPos = position + ((scale.X / 2 - 0.5f * toolScale) * rot.Down);

            anchorRight.Transform.SetPosition(rightPos);
            anchorForward.Transform.SetPosition(forwardPos);
            anchorLeft.Transform.SetPosition(leftPos);
            anchorBackward.Transform.SetPosition(backwardPos);
            anchorUp.Transform.SetPosition(upPos);
            anchorDown.Transform.SetPosition(downPos);
        }
    }
}
