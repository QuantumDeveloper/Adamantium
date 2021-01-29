using Adamantium.Engine.Services;
using Adamantium.Engine.Templates.Tools;
using Adamantium.EntityFramework;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.Game.GameInput;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Tools
{
    public class ScaleTool : TransformTool
    {
        private float ScaleToolLength = 1;
        private Vector3F _previousScaleFactor;
        private Vector3F _scale;
        private Vector3F sign = Vector3F.One;
        private const string RightAxisManipulatorName = "RightAxisManipulator";
        private const string UpAxisManipulatorName = "UpAxisManipulator";
        private const string ForwardAxisManipulatorName = "ForwardAxisManipulator";
        private const string RightForwardManipulatorName = "RightForwardManipulator";
        private const string UpForwardManipulatorName = "UpForwardManipulator";
        private const string RightUpManipulatorName = "RightUpManipulator";
        private const string CentralManipulatorName = "CentralManipulator";

        private Vector3D rightPos;
        private Vector3D upPos;
        private Vector3D forwardPos;
        private Vector3D rightUpPos;
        private Vector3D rightForwardPos;
        private Vector3D upForwardPos;

        private readonly Entity rightManipulator;
        private readonly Entity upManipulator;
        private readonly Entity forwardManipulator;
        private readonly Entity rightUpManipulator;
        private readonly Entity rightForwardManipulator;
        private readonly Entity upForwardManipulator;
        private readonly Entity centralManipulator;


        public ScaleTool(bool initialState, float axisLength, Vector3F baseScale) : base("ScaleTool")
        {
            var scaleTemplate = new ScaleToolTemplate(axisLength, baseScale);
            Tool = scaleTemplate.BuildEntity(null, Name);
            Enabled = initialState;

            rightManipulator = Tool.Get(RightAxisManipulatorName);
            upManipulator = Tool.Get(UpAxisManipulatorName);
            forwardManipulator = Tool.Get(ForwardAxisManipulatorName);

            rightUpManipulator = Tool.Get(RightUpManipulatorName);
            rightForwardManipulator = Tool.Get(RightForwardManipulatorName);
            upForwardManipulator = Tool.Get(UpForwardManipulatorName);

            centralManipulator = Tool.Get(CentralManipulatorName);
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
                UpdateToolTransform(targetEntity, cameraService, true, false, false);

                toolScale = targetEntity.GetRelativePosition(camera).Length() *
                            MathHelper.DegreesToRadians(camera.Fov) * camera.AspectRatio * 0.01f;
                var collisionMode = CollisionMode.IgnoreNonGeometryParts;

                PositionManipulators(targetEntity);
                UpdateScaleAndPosition(targetEntity);

                toolIntersectionResult = Tool.Intersects(camera, inputService.VirtualPosition, collisionMode, CompareOrder.Less, 0);

                if (toolIntersectionResult.Intersects)
                {
                    selectedTool = toolIntersectionResult.Entity;
                    _previousScaleFactor = targetEntity.Transform.ScaleFactor;
                    HighlightSelectedTool(true);
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

            if (IsLocked)
            {
                HighlightSelectedTool(true);
                Vector3F interPoint;
                var intersects = GetRayPlaneIntersectionPoint(camera, inputService, out interPoint);
                if (intersects)
                {
                    TransformEntityScale(targetEntity, camera, interPoint);
                }
            }

            Tool.TraverseByLayer(
                current =>
                {
                    UpdateAxisVisibility(current, camera);
                },
                true);
            Transform(Tool, cameraService);
        }

        private void TransformEntityScale(Entity targetEntity, Camera camera, Vector3F rayPlaneInterPoint)
        {
            _scale = rayPlaneInterPoint / toolIntersectionResult.IntersectionPoint;

            var matrix = targetEntity.Transform.GetRotationMatrix();

            if (selectedTool == rightManipulator)
            {
                if (!float.IsNaN(_scale.X) && !float.IsInfinity(_scale.X))
                {
                    var left = Vector3F.Normalize(selectedTool.GetActualMatrix(camera).Left);
                    var angle = Vector3F.Dot(camera.Backward, left);
                    _scale.X = CheckScale(_scale.X, angle);
                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            var factor = current.Transform.ScaleFactor;
                            factor.X = _scale.X;
                            current.Transform.ScaleFactor = new Vector3F(_previousScaleFactor.X * factor.X, factor.Y, factor.Z);
                        });

                    selectedTool.Owner.Transform.ScaleFactor = new Vector3F(toolScale * _scale.X, 1, 1);
                    var scaleDelta =  CalculateManipulatorPosition(_scale.X, sign.X, selectedTool.Transform.BaseScale.X, rightPos, matrix.Right);
                    selectedTool.Transform.SetPosition(scaleDelta);
                }
            }
            else if (selectedTool == upManipulator)
            {
                if (!float.IsNaN(_scale.Y) && !float.IsInfinity(_scale.Y))
                {
                    var up = Vector3F.Normalize(selectedTool.GetActualMatrix(camera).Down);
                    var angle = Vector3F.Dot(camera.Backward, up);
                    _scale.Y = CheckScale(_scale.Y, angle);
                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            var factor = current.Transform.ScaleFactor;
                            factor.Y = _scale.Y;
                            current.Transform.ScaleFactor = new Vector3F(
                                factor.X,
                                _previousScaleFactor.Y * factor.Y,
                                factor.Z);
                        });

                    selectedTool.Owner.Transform.ScaleFactor = new Vector3F(1, toolScale * _scale.Y, 1);
                    var scaleDelta = CalculateManipulatorPosition(_scale.Y, sign.Y, selectedTool.Transform.BaseScale.Y, upPos, matrix.Up);
                    selectedTool.Transform.SetPosition(scaleDelta);
                }
            }
            else if (selectedTool == forwardManipulator)
            {
                if (!float.IsNaN(_scale.Z) && !float.IsInfinity(_scale.Z))
                {
                    var backward = Vector3F.Normalize(selectedTool.GetActualMatrix(camera).Forward);
                    var angle = Vector3F.Dot(camera.Backward, backward);
                    _scale.Z = CheckScale(_scale.Z, angle);
                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            var factor = current.Transform.ScaleFactor;
                            factor.Z = _scale.Z;
                            current.Transform.ScaleFactor = new Vector3F(
                                factor.X,
                                factor.Y,
                                _previousScaleFactor.Z * factor.Z);
                        });
                    selectedTool.Owner.Transform.ScaleFactor = toolScale * new Vector3F(1, 1, _scale.Z);
                    var scaleDelta = CalculateManipulatorPosition(_scale.Z, sign.Z, selectedTool.Transform.BaseScale.Z, forwardPos, matrix.Backward);
                    selectedTool.Transform.SetPosition(scaleDelta);
                }
            }
            else if (selectedTool == rightForwardManipulator)
            {
                if (!float.IsNaN(_scale.X) && !float.IsInfinity(_scale.X))
                {
                    var right = Vector3F.Normalize(selectedTool.GetActualMatrix(camera).Up);
                    var angle = Vector3F.Dot(camera.Backward, right);
                    var averageScale = (_scale.X + _scale.Z) / 2;
                    //averageScale = CheckScale(averageScale, angle);

                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            var factor = current.Transform.ScaleFactor;
                            factor.X = averageScale;
                            factor.Z = averageScale;
                            current.Transform.ScaleFactor = new Vector3F(
                                _previousScaleFactor.X * factor.X,
                                factor.Y,
                                _previousScaleFactor.Z * factor.Z);
                        });

                    rightManipulator.Owner.Transform.ScaleFactor = new Vector3F(toolScale * averageScale, 1, 1);
                    forwardManipulator.Owner.Transform.ScaleFactor = new Vector3F(1, 1, toolScale * averageScale);

                    var scaleDeltaX = CalculateManipulatorPosition(averageScale, sign.X, rightManipulator.Transform.BaseScale.X, rightPos, matrix.Right);
                    var scaleDeltaZ = CalculateManipulatorPosition(averageScale, sign.Z, forwardManipulator.Transform.BaseScale.Z, forwardPos, matrix.Backward);

                    var rightForwardX = CalculateManipulatorPosition(averageScale, sign.X, rightForwardManipulator.Transform.BaseScale.X, matrix.Right);
                    var rightForwardZ = CalculateManipulatorPosition(averageScale, sign.Z, rightForwardManipulator.Transform.BaseScale.Z, matrix.Backward);
                    
                    rightManipulator.Transform.SetPosition(scaleDeltaX);
                    forwardManipulator.Transform.SetPosition(scaleDeltaZ);
                    selectedTool.Transform.SetPosition(rightForwardX + rightForwardZ + rightForwardPos);
                }
            }
            else if (selectedTool == upForwardManipulator)
            {
                if (!float.IsNaN(_scale.X) && !float.IsInfinity(_scale.X))
                {
                    var right = Vector3F.Normalize(selectedTool.GetActualMatrix(camera).Right);
                    var angle = Vector3F.Dot(camera.Backward, right);
                    var averageScale = (_scale.Y + _scale.Z) / 2;
                    //averageScale = CheckScale(averageScale, angle);

                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            var factor = current.Transform.ScaleFactor;
                            factor.Y = averageScale;
                            factor.Z = averageScale;
                            current.Transform.ScaleFactor = new Vector3F(
                                factor.X,
                                _previousScaleFactor.Y * factor.Y,
                                _previousScaleFactor.Z * factor.Z);
                        });

                    upManipulator.Owner.Transform.ScaleFactor = new Vector3F(1, toolScale * averageScale, 1);
                    forwardManipulator.Owner.Transform.ScaleFactor = new Vector3F(1, 1, toolScale * averageScale);

                    var scaleDeltaY = CalculateManipulatorPosition(averageScale, sign.Y, upManipulator.Transform.BaseScale.Y, upPos, matrix.Up);
                    var scaleDeltaZ = CalculateManipulatorPosition(averageScale, sign.Z, forwardManipulator.Transform.BaseScale.Z, forwardPos, matrix.Backward);

                    var upForwardY = CalculateManipulatorPosition(averageScale, sign.Y, upForwardManipulator.Transform.BaseScale.Y, matrix.Up);
                    var upForwardZ = CalculateManipulatorPosition(averageScale, sign.Z, upForwardManipulator.Transform.BaseScale.Z, matrix.Backward);
                    
                    upManipulator.Transform.SetPosition(scaleDeltaY);
                    forwardManipulator.Transform.SetPosition(scaleDeltaZ);
                    selectedTool.Transform.SetPosition(upForwardY + upForwardZ + upForwardPos);
                }
            }
            else if (selectedTool == rightUpManipulator)
            {
                if (!float.IsNaN(_scale.X) && !float.IsInfinity(_scale.X))
                {
                    var right = Vector3F.Normalize(
                        selectedTool.GetActualMatrix(camera).Forward);
                    var angle = Vector3F.Dot(camera.Backward, right);
                    var averageScale = (_scale.X + _scale.Y) / 2;
                    //averageScale = CheckScale(averageScale, angle);

                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            var factor = current.Transform.ScaleFactor;
                            factor.X = averageScale;
                            factor.Y = averageScale;
                            current.Transform.ScaleFactor = new Vector3F(
                                _previousScaleFactor.X * factor.X,
                                _previousScaleFactor.Y * factor.Y,
                                factor.Z);
                        });

                    upManipulator.Owner.Transform.ScaleFactor = new Vector3F(1, toolScale * averageScale, 1);
                    rightManipulator.Owner.Transform.ScaleFactor = new Vector3F(toolScale * averageScale, 1, 1);

                    var scaleDeltaX = CalculateManipulatorPosition(averageScale, sign.X, rightManipulator.Transform.BaseScale.X, rightPos, matrix.Right);
                    var scaleDeltaY = CalculateManipulatorPosition(averageScale, sign.Y, upManipulator.Transform.BaseScale.Y, upPos, matrix.Up);

                    var rightUpX = CalculateManipulatorPosition(averageScale, sign.X, rightUpManipulator.Transform.BaseScale.X, matrix.Right);
                    var rightUpY = CalculateManipulatorPosition(averageScale, sign.Y, rightUpManipulator.Transform.BaseScale.Y, matrix.Up);

                    rightManipulator.Transform.SetPosition(scaleDeltaX);
                    upManipulator.Transform.SetPosition(scaleDeltaY);

                    selectedTool.Transform.SetPosition(rightUpX + rightUpY + rightUpPos);
                }
            }
            else if (selectedTool == centralManipulator)
            {
                var average = Vector3F.Average(_scale);

                var forward = Vector3F.Normalize(selectedTool.GetActualMatrix(camera).Backward);
                var angle = Vector3F.Dot(camera.Forward, forward);
                //average = CheckScale(average, angle);

                if (!float.IsNaN(average) && !float.IsInfinity(average) && !MathHelper.IsZero(average))
                {
                    targetEntity.TraverseInDepth(
                        current =>
                        {
                            current.Transform.ScaleFactor = _previousScaleFactor * average;
                        });

                    rightManipulator.Owner.Transform.ScaleFactor = new Vector3F(toolScale * average, 1, 1);
                    upManipulator.Owner.Transform.ScaleFactor = new Vector3F(1, toolScale * average, 1);
                    forwardManipulator.Owner.Transform.ScaleFactor = new Vector3F(1, 1, toolScale * average);

                    var scaleDeltaX = CalculateManipulatorPosition(average, sign.X, rightManipulator.Transform.BaseScale.X, matrix.Right);
                    var scaleDeltaY = CalculateManipulatorPosition(average, sign.Y, upManipulator.Transform.BaseScale.Y, matrix.Up);
                    var scaleDeltaZ = CalculateManipulatorPosition(average, sign.Z, forwardManipulator.Transform.BaseScale.Z, matrix.Backward);

                    rightManipulator.Transform.Position = scaleDeltaX + rightPos;
                    upManipulator.Transform.Position = scaleDeltaY + upPos;
                    forwardManipulator.Transform.Position = scaleDeltaZ + forwardPos;

                    rightForwardManipulator.Transform.SetPosition(scaleDeltaX + scaleDeltaZ + rightForwardPos);
                    upForwardManipulator.Transform.SetPosition(scaleDeltaY + scaleDeltaZ + upForwardPos);
                    rightUpManipulator.Transform.SetPosition(scaleDeltaX + scaleDeltaY + rightUpPos);
                }
            }
        }

        private Vector3D CalculateManipulatorPosition(double scale, float scaleSign, float baseScale, Vector3D axis)
        {
            return CalculateManipulatorPosition(scale, scaleSign, baseScale, Vector3D.Zero, axis);
        }

        private Vector3D CalculateManipulatorPosition(double scale, float scaleSign, float baseScale, Vector3D initialPosition, Vector3D axis)
        {
            scale *= scaleSign;
            if (scaleSign > 0)
            {
                scale -= ScaleToolLength;
            }
            else
            {
                scale += ScaleToolLength;
            }
            scale *= toolScale * baseScale;
            Vector3D newPos = axis * scale + initialPosition;
            return newPos;
        }

        private Vector3D CalculateManipulatorPositionEx(float scaleSign, float baseScale, Vector3D initialPosition, Vector3D axis)
        {
            double scale = 0;
            if (scaleSign < 0)
            {
                scale = -ScaleToolLength * 2;
            }
            
            scale *= toolScale * baseScale;
            Vector3D newPos = axis * scale + initialPosition;
            return newPos;
        }

        private void PositionManipulators(Entity targetEntity)
        {
            var scale = targetEntity.Transform.Scale;
            var matrix = targetEntity.Transform.GetRotationMatrix();
            var rightDelta = CalculateManipulatorPositionEx(scale.X, rightManipulator.Transform.BaseScale.X, rightManipulator.Transform.Position, matrix.Right);
            var upDelta = CalculateManipulatorPositionEx(scale.Y, upManipulator.Transform.BaseScale.Y, upManipulator.Transform.Position, matrix.Up);
            var forwardDelta = CalculateManipulatorPositionEx(scale.Z, forwardManipulator.Transform.BaseScale.Z, forwardManipulator.Transform.Position, matrix.Backward);

            var rightUpPosition = CalculateManipulatorPositionEx(scale.Y, upManipulator.Transform.BaseScale.Y, rightDelta, matrix.Up);
            var rightForwardPosition = CalculateManipulatorPositionEx(scale.Z, upManipulator.Transform.BaseScale.Y, rightDelta, matrix.Backward);
            var upForwardPosition = CalculateManipulatorPositionEx(scale.Z, upManipulator.Transform.BaseScale.Y, upDelta, matrix.Backward);

            rightManipulator.Transform.SetPosition(rightDelta);
            upManipulator.Transform.SetPosition(upDelta);
            forwardManipulator.Transform.SetPosition(forwardDelta);

            rightUpManipulator.Transform.SetPosition(rightUpPosition);
            rightForwardManipulator.Transform.SetPosition(rightForwardPosition);
            upForwardManipulator.Transform.SetPosition(upForwardPosition);

            if (scale.X < 0)
            {
                if (rightManipulator.Owner.Transform.BaseScale.X > 0)
                {
                    var baseScale = rightManipulator.Owner.Transform.BaseScale;
                    rightManipulator.Owner.Transform.BaseScale = new Vector3F(-baseScale.X, baseScale.Y, baseScale.Z);
                }
            }
            else
            {
                if (rightManipulator.Owner.Transform.BaseScale.X < 0)
                {
                    var baseScale = rightManipulator.Owner.Transform.BaseScale;
                    rightManipulator.Owner.Transform.BaseScale = new Vector3F(-baseScale.X, baseScale.Y, baseScale.Z);
                }
            }

            if (scale.Y < 0)
            {
                if (upManipulator.Owner.Transform.BaseScale.Y > 0)
                {
                    var baseScale = upManipulator.Owner.Transform.BaseScale;
                    upManipulator.Owner.Transform.BaseScale = new Vector3F(baseScale.X, -baseScale.Y, baseScale.Z);
                }
            }
            else
            {
                if (upManipulator.Owner.Transform.BaseScale.Y < 0)
                {
                    var baseScale = upManipulator.Owner.Transform.BaseScale;
                    upManipulator.Owner.Transform.BaseScale = new Vector3F(baseScale.X, -baseScale.Y, baseScale.Z);
                }
            }

            if (scale.Z < 0)
            {
                if (forwardManipulator.Owner.Transform.BaseScale.Z > 0)
                {
                    var baseScale = forwardManipulator.Owner.Transform.BaseScale;
                    forwardManipulator.Owner.Transform.BaseScale = new Vector3F(baseScale.X, baseScale.Y, -baseScale.Z);
                }
            }
            else
            {
                if (forwardManipulator.Owner.Transform.BaseScale.Z < 0)
                {
                    var baseScale = forwardManipulator.Owner.Transform.BaseScale;
                    forwardManipulator.Owner.Transform.BaseScale = new Vector3F(baseScale.X, baseScale.Y, -baseScale.Z);
                }
            }
        }

        private void UpdateScaleAndPosition(Entity targetEntity)
        {
            sign.X = targetEntity.Transform.Scale.X < 0 ? -1 : 1;
            sign.Y = targetEntity.Transform.Scale.Y < 0 ? -1 : 1;
            sign.Z = targetEntity.Transform.Scale.Z < 0 ? -1 : 1;

            rightPos = rightManipulator.Transform.Position;
            upPos = upManipulator.Transform.Position;
            forwardPos = forwardManipulator.Transform.Position;
            rightUpPos = rightUpManipulator.Transform.Position;
            rightForwardPos = rightForwardManipulator.Transform.Position;
            upForwardPos = upForwardManipulator.Transform.Position;
        }

        private float CheckScale(float scaleValue, float angle)
        {
            if (angle < 0.0f)
            {
                scaleValue = 1 - scaleValue + 1;
            }
            return scaleValue;
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
                rightManipulator.Owner.TraverseByLayer(current => { current.IsSelected = value; }, true);
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
                rightManipulator.Owner.TraverseInDepth(current => { current.Visible = value; }, true);
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

