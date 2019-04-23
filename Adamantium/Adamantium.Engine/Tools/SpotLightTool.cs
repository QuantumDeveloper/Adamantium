using System;
using Adamantium.Engine.GameInput;
using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Lights;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class SpotLightTool : LightTool
    {
        private Vector3F _scale;
        private Entity anchorCenter;
        private Entity anchorRight;
        private Entity anchorForward;
        private Entity anchorLeft;
        private Entity anchorBackward;

        private Vector3F _previousScaleFactor;

        private float MinimumSpotAngle = MathHelper.DegreesToRadians(1f);
        private float MaximumSpotAngle = MathHelper.DegreesToRadians(89.5f);

        public SpotLightTool(string name) : base(name)
        {
            Tool = new SpotLightVisualTemplate().BuildEntity(null, "Spot");
            anchorCenter = Tool.Get("AnchorPointCenter");
            anchorRight = Tool.Get("AnchorPointRight");
            anchorForward = Tool.Get("AnchorPointForward");
            anchorLeft = Tool.Get("AnchorPointLeft");
            anchorBackward = Tool.Get("AnchorPointBackward");
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
                    inputService.RelativePosition,
                    collisionMode,
                    CompareOrder.Less,
                    0.05f);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    previousCoordinates = toolIntersectionResult.IntersectionPoint;
                    var spotRadius = CurrentLight.SpotRadius;
                    _previousScaleFactor = new Vector3F(spotRadius, CurrentLight.Range, spotRadius);
                    HighlightSelectedTool(true);
                }

                IsLocked = CheckIsLocked(inputService);

                if (IsLocked)
                {
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
                    ScaleSpotLight(targetEntity, camera, interPoint);
                }
            }

            Transform(Tool, cameraService);
        }

        private void ScaleSpotLight(Entity lightToTransform, Camera camera, Vector3F rayPlaneInterPoint)
        {
            _scale = rayPlaneInterPoint / toolIntersectionResult.IntersectionPoint;
            if (!float.IsInfinity(_scale.X) && !float.IsInfinity(_scale.Z))
            {
                var avg = (_scale.X + _scale.Z) / 2;
                _scale.X = _scale.Z = avg;
            }
            else
            {
                _scale.X = _scale.Z = Math.Min(_scale.X, _scale.Z);
            }

            if (selectedTool == anchorCenter)
            {
                if (!float.IsNaN(_scale.Y) && !MathHelper.IsZero(_scale.Y) && !float.IsInfinity(_scale.Y))
                {
                    if (_scale.Y < MinimumAllowedRange)
                    {
                        _scale.Y = MinimumAllowedRange;
                    }

                    CurrentLight.Range = _previousScaleFactor.Y * _scale.Y;
                    selectedTool.Owner.Transform.ScaleFactor = new Vector3F(_previousScaleFactor.X, CurrentLight.Range, _previousScaleFactor.Z);
                }
            }
            else
            {
                if (!float.IsNaN(_scale.X) && !MathHelper.IsZero(_scale.X) && !float.IsInfinity(_scale.Y))
                {
                    var radius = _previousScaleFactor.X * _scale.X;
                    CurrentLight.OuterSpotAngle = (float) Math.Atan(radius / CurrentLight.Range);

                    if (CurrentLight.OuterSpotAngle < MinimumSpotAngle)
                    {
                        CurrentLight.OuterSpotAngle = MinimumSpotAngle;
                    }
                    else if (CurrentLight.OuterSpotAngle > MaximumSpotAngle)
                    {
                        CurrentLight.OuterSpotAngle = MaximumSpotAngle;
                    }

                    var spotRadius = CurrentLight.SpotRadius;
                    selectedTool.Owner.Transform.ScaleFactor = new Vector3F(spotRadius, CurrentLight.Range, spotRadius);
                }
            }

            KeepScaleForAnchors();
            PositionAnchors(lightToTransform);
        }

        private void KeepScaleForAnchors()
        {
            anchorCenter.Transform.SetScaleFactor(toolScale);
            anchorRight.Transform.SetScaleFactor(toolScale);
            anchorForward.Transform.SetScaleFactor(toolScale);
            anchorLeft.Transform.SetScaleFactor(toolScale);
            anchorBackward.Transform.SetScaleFactor(toolScale);
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
                }, true);

            selectedTool.IsSelected = value;
        }

        protected override void UpdateToolTransform(Entity target, CameraService cameraService, bool isLocalAxis, bool useTargetCenter, bool calculateTransform)
        {
            var camera = cameraService.UserControlledCamera;
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

            Tool.TraverseByLayer(current =>
            {
                current.Transform.Rotation = target.Transform.Rotation;
                var spotRadius = CurrentLight.SpotRadius;
                current.Transform.ScaleFactor = new Vector3F(spotRadius, CurrentLight.Range, spotRadius);
            }, true);

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
            var scale = new Vector3F(CurrentLight.SpotRadius, CurrentLight.Range, CurrentLight.SpotRadius);
            var position = targetEntity.Transform.Position;
            var rot = targetEntity.Transform.GetRotationMatrix();

            var newPos2 = position + ((scale.Y - 1* toolScale) * rot.Down);
            var rightPos = newPos2 + ((scale.X / 2 - 0.5f*toolScale) * rot.Right);
            var forwardPos = newPos2 + ((scale.X / 2 - 0.5f * toolScale) * rot.Forward);
            var leftPos = newPos2 + ((scale.X / 2 - 0.5f * toolScale) * rot.Left);
            var backwardPos = newPos2 + ((scale.X / 2 - 0.5f * toolScale) * rot.Backward);

            anchorCenter.Transform.SetPosition(newPos2);
            anchorRight.Transform.SetPosition(rightPos);
            anchorForward.Transform.SetPosition(forwardPos);
            anchorLeft.Transform.SetPosition(leftPos);
            anchorBackward.Transform.SetPosition(backwardPos);
        }
    }
}
