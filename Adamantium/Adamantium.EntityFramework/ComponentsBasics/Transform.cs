using System;
using System.Collections.Generic;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public sealed class Transform : ActivatableComponent
    {
        public Transform()
        {
            InitialPosition = Vector3D.Zero;
            Position = Vector3D.Zero;
            Rotation = QuaternionF.Identity;
            PivotRotation = QuaternionF.Identity;
            baseScale = Vector3F.One;
            scaleFactor = Vector3F.One;
            TransformData = new Dictionary<Camera, TransformMetaData>();
        }

        private Vector3D initialPosition;
        private Vector3D position;
        private QuaternionF rotation;
        private Vector3F baseScale;
        private Vector3F scaleFactor;
        private Vector3D pivot;
        private QuaternionF pivotRotation;

        private readonly Dictionary<Camera, TransformMetaData> TransformData;

        public Vector3F GetRelativePosition(Vector3D offset)
        {
            return Position - offset;
        }

        public void RemoveMetadata(Camera camera)
        {
            if (TransformData.ContainsKey(camera))
            {
                TransformData.Remove(camera);
            }
        }

        public TransformMetaData GetMetadata(Camera camera)
        {
            TransformMetaData metaData;
            TransformData.TryGetValue(camera, out metaData);
            if (metaData == null)
            {
                metaData = TransformMetaData.New();
                metaData.Camera = camera;
                TransformData.Add(camera, metaData);
            }
            return metaData;
        }

        public void SetMetadata(Camera camera, TransformMetaData metadata)
        {
            //Make sure metadata contains correct camera instance
            metadata.Camera = camera;
            if (TransformData.ContainsKey(camera))
            {
                TransformData[camera] = metadata;
            }
            else
            {
                TransformData.Add(camera, metadata);
            }
        }

        public void SetEnableForCamera(Camera camera, bool enabled)
        {
            GetMetadata(camera).Enabled = enabled;
        }

        public Vector3D Pivot
        {
            get => pivot + Position;
            set
            {
                if (SetProperty(ref pivot, value))
                {
                    pivot = value - Position;
                }
            }
        }

        public QuaternionF PivotRotation
        {
            get => pivotRotation;
            set => SetProperty(ref pivotRotation, value);
        }

        public Vector3D InitialPosition
        {
            get => initialPosition;
            set => SetProperty(ref initialPosition, value);
        }

        public Vector3D Position
        {
            get => position;
            set => SetProperty(ref position, value);
        }

        public QuaternionF Rotation
        {
            get => rotation;
            set => SetProperty(ref rotation, value);
        }

        public Vector3F BaseScale
        {
            get => baseScale;
            set
            {
                if (SetProperty(ref baseScale, value))
                {
                    RaisePropertyChanged(nameof(Scale));
                }
            }
        }

        public Vector3F ScaleFactor
        {
            get => scaleFactor;
            set
            {
                if (SetProperty(ref scaleFactor, value))
                {
                    RaisePropertyChanged(nameof(Scale));
                }
            }
        }

        public Vector3F Scale => baseScale * scaleFactor;

        public void Move(Vector3F direction, Double distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(direction, distance);
                if (Owner == null && IsEnabled)
                {
                    Position = distanceVector;
                    return;
                }

                Traverse(entity => Move(entity, distanceVector));
            }
        }

        public void Move(Vector3F direction, Vector3D distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(direction, distance);
                if (Owner == null && IsEnabled)
                {
                    Position = distanceVector;
                    return;
                }

                Traverse(entity => Move(entity, distanceVector));
            }
        }

        public void Translate(Vector3F direction, Double distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(direction, distance);
                if (Owner == null && IsEnabled)
                {
                    Position += distanceVector;
                    return;
                }

                Traverse(entity => Translate(entity, distanceVector));
            }
        }

        public void TranslateRight(Double distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(GetRotationMatrix().Right, distance);
                if (Owner == null && IsEnabled)
                {
                    Position += distanceVector;
                    return;
                }

                Traverse(entity => Translate(entity, distanceVector));
            }
        }

        public void TranslateUp(Double distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(GetRotationMatrix().Up, distance);
                if (Owner == null && IsEnabled)
                {
                    Position += distanceVector;
                    return;
                }

                Traverse(entity => Translate(entity, distanceVector));
            }
        }

        public void TranslateForward(Double distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(GetRotationMatrix().Forward, distance);
                if (Owner == null && IsEnabled)
                {
                    Position += distanceVector;
                    return;
                }

                Traverse(entity => Translate(entity, distanceVector));
            }
        }

        public void SetScalingRotation(QuaternionF rotation)
        {
            lock (this)
            {
                if (Owner == null && IsEnabled)
                {
                    PivotRotation = rotation;
                    return;
                }

                Traverse(entity => SetScalingRotation(entity, rotation));
            }
        }

        public void Translate(Vector3F direction, Vector3F distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(direction, distance);
                if (Owner == null && IsEnabled)
                {
                    Position += distanceVector;
                    return;
                }

                Traverse(entity => Translate(entity, distanceVector));
            }
        }

        public void TranslatePivot(Vector3F direction, Double distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(direction, distance);
                if (Owner == null && IsEnabled)
                {
                    Pivot += distanceVector;
                    return;
                }

                Traverse(entity => TranslatePivot(entity, distanceVector));
            }
        }

        public void TranslatePivot(Vector3F direction, Vector3F distance)
        {
            lock (this)
            {
                var distanceVector = Vector3D.Multiply(direction, distance);
                if (Owner == null && IsEnabled)
                {
                    Pivot += distanceVector;
                    return;
                }

                Traverse(entity => TranslatePivot(entity, distanceVector));
            }
        }

        public void SetPosition(Vector3D newPosition)
        {
            lock (this)
            {
                if (Owner == null && IsEnabled)
                {
                    Position = newPosition;
                    return;
                }

                Traverse(entity => SetPosition(entity, newPosition));
            }
        }

        public void SetPivot(Vector3F pivotPosition)
        {
            lock (this)
            {
                if (Owner == null && IsEnabled)
                {
                    Pivot = pivotPosition;
                    return;
                }

                Traverse(entity => SetPivot(entity, pivotPosition));
            }
        }

        private float EnsureAngle(float angle, RotationUnits units)
        {
            if (units == RotationUnits.Degrees)
            {
                angle = MathHelper.DegreesToRadians(angle);
            }
            return angle;
        }

        public Matrix4x4F GetRotationMatrix()
        {
            return Matrix4x4F.RotationQuaternion(Rotation);
        }

        public Matrix4x4F GetPivotRotationMatrix()
        {
            return Matrix4x4F.RotationQuaternion(PivotRotation);
        }

        public void ResetPosition()
        {
            if (Owner == null && IsEnabled)
            {
                Position = InitialPosition;
                return;
            }

            Traverse(entity => SetPosition(entity, InitialPosition));
        }

        public void ResetScale()
        {
            if (Owner == null && IsEnabled)
            {
                ScaleFactor = Vector3F.One;
                return;
            }

            Traverse(entity => SetScaleFactor(entity, Vector3F.One));
        }

        public void ResetRotation()
        {
            if (Owner == null && IsEnabled)
            {
                Rotation = QuaternionF.Identity;
                return;
            }

            Traverse(entity => SetRotation(entity, QuaternionF.Identity));
        }

        public void ResetPivotPosition()
        {
            if (Owner == null && IsEnabled)
            {
                Pivot = Position;
                return;
            }

            Traverse(entity => SetPivot(entity, entity.Transform.Position));
        }

        public void ResetPivotRotation()
        {
            if (Owner == null && IsEnabled)
            {
                PivotRotation = QuaternionF.Identity;
                return;
            }

            Traverse(entity => SetPivotRotation(entity, QuaternionF.Identity));
        }

        public void Rotate(Vector3F axis, float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(axis, angle), Rotation);
                return;
            }

            Traverse(entity => Rotate(entity, axis, angle));
        }

        public void RotateRight(float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(GetRotationMatrix().Right, angle), Rotation);
                return;
            }

            Traverse(entity => Rotate(entity, Owner.Transform.GetRotationMatrix().Right, angle));
        }

        public void RotateUp(float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(GetRotationMatrix().Up, angle), Rotation);
                return;
            }

            Traverse(entity => Rotate(entity, Owner.Transform.GetRotationMatrix().Up, angle));
        }

        public void RotateForward(float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(GetRotationMatrix().Forward, angle), Rotation);
                return;
            }

            Traverse(entity => Rotate(entity, Owner.Transform.GetRotationMatrix().Forward, angle));
        }

        public void RotatePivot(Vector3F axis, float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                PivotRotation = QuaternionF.Multiply(QuaternionF.RotationAxis(axis, angle), PivotRotation);
                return;
            }

            Traverse(entity => RotatePivot(entity, axis, angle));
        }

        public void RotatePivotRight(float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                PivotRotation = QuaternionF.Multiply(QuaternionF.RotationAxis(GetPivotRotationMatrix().Right, angle), PivotRotation);
                return;
            }

            Traverse(entity => RotatePivot(entity, Owner.Transform.GetPivotRotationMatrix().Right, angle));
        }

        public void RotatePivotUp(float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                PivotRotation = QuaternionF.Multiply(QuaternionF.RotationAxis(GetPivotRotationMatrix().Up, angle), PivotRotation);
                return;
            }

            Traverse(entity => RotatePivot(entity, Owner.Transform.GetPivotRotationMatrix().Up, angle));
        }

        public void RotatePivotForward(float angle, RotationUnits units = RotationUnits.Radians)
        {
            angle = EnsureAngle(angle, units);
            if (Owner == null && IsEnabled)
            {
                PivotRotation = QuaternionF.Multiply(QuaternionF.RotationAxis(GetPivotRotationMatrix().Forward, angle), PivotRotation);
                return;
            }

            Traverse(entity => RotatePivot(entity, Owner.Transform.GetPivotRotationMatrix().Forward, angle));
        }

        ///<summary>
        ///Sync entity orientation with camera forward axis for left handed coordinate system
        ///</summary>
        public void SyncOrientationWithCameraForwardLH(Camera camera)
        {
            var rotMatr = camera.GetCameraRotationMatrix();
            var quat = QuaternionF.RotationLookAtLH(rotMatr.Forward, rotMatr.Up);
            Owner.Transform.SetRotation(quat);
        }

        ///<summary>
        ///Sync entity orientation with camera backward axis for left handed coordinate system
        ///</summary>
        public void SyncOrientationWithCameraBackwardLH(Camera camera)
        {
            var rotMatr = camera.GetCameraRotationMatrix();
            var quat = QuaternionF.RotationLookAtLH(rotMatr.Backward, rotMatr.Up);
            Owner.Transform.SetRotation(quat);
        }

        public void SetRotation(QuaternionF rotation)
        {
            if (Owner == null && IsEnabled)
            {
                Rotation = rotation;
                return;
            }

            Traverse(entity => SetRotation(entity, rotation));
        }

        public void SetPivotRotation(QuaternionF rotation)
        {
            if (Owner == null && IsEnabled)
            {
                PivotRotation = rotation;
                return;
            }

            Traverse(entity => SetPivotRotation(entity, rotation));
        }

        public void SetScaleFactor(float factor)
        {
            SetScaleFactor(new Vector3F(factor));
        }

        public void SetScaleFactor(Vector3F factor)
        {
            if (Owner == null && IsEnabled)
            {
                ScaleFactor = factor;
                return;
            }

            Traverse(entity => SetScaleFactor(entity, factor));
        }

        public void SetBaseScale(Vector3F scale)
        {
            if (Owner == null && IsEnabled)
            {
                BaseScale = scale;
                return;
            }

            Traverse(entity => SetBaseScale(entity, scale));
        }

        public void SetBaseScale(float scale)
        {
            SetBaseScale(new Vector3F(scale));
        }

        public void DivideScale(float scale)
        {
            DivideScale(new Vector3F(scale));
        }


        public void DivideScale(Vector3F scale)
        {
            if (Owner == null && IsEnabled)
            {
                ScaleFactor /= scale;
                return;
            }

            Traverse(entity => DivideScale(entity, scale));
        }

        public void MultiplyScale(float scale)
        {
            MultiplyScale(new Vector3F(scale));
        }

        public void MultiplyScale(Vector3F scale)
        {
            if (Owner == null && IsEnabled)
            {
                ScaleFactor *= scale;
                return;
            }

            Traverse(entity => MultiplyScale(entity, scale));
        }

        private void MultiplyScale(Entity entity, Vector3F scale)
        {
            entity.Transform.ScaleFactor *= scale;
        }

        private void DivideScale(Entity entity, Vector3F scale)
        {
            entity.Transform.ScaleFactor /= scale;
        }

        private void SetBaseScale(Entity entity, Vector3F scale)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.BaseScale = scale;
            }
        }

        private void SetScaleFactor(Entity entity, Vector3F scale)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.ScaleFactor = scale;
            }
        }

        private void SetRotation(Entity entity, QuaternionF rotation)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.Rotation = rotation;
            }
        }

        private void Rotate(Entity entity, Vector3F axis, float angle)
        {
            if (entity.Transform.IsEnabled)
            {
                var transform = entity.Transform;
                transform.Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(axis, angle), transform.Rotation);
            }
        }

        private void RotatePivot(Entity entity, Vector3F axis, float angle)
        {
            if (entity.Transform.IsEnabled)
            {
                var transform = entity.Transform;
                transform.PivotRotation = QuaternionF.Multiply(QuaternionF.RotationAxis(axis, angle), transform.PivotRotation);
            }
        }

        private void Move(Entity entity, Vector3D distance)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.Position = distance;
            }
        }

        private void Translate(Entity entity, Vector3D distance)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.Position += distance;
            }
        }

        private void SetScalingRotation(Entity entity, QuaternionF rotation)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.PivotRotation = rotation;
            }
        }

        private void TranslatePivot(Entity entity, Vector3D distance)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.Pivot += distance;
            }
        }

        private void SetPivot(Entity entity, Vector3D position)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.Pivot = position;
            }
        }

        private void SetPosition(Entity entity, Vector3D newPosition)
        {
            if (entity.Transform != null && entity.Transform.IsEnabled)
            {
                entity.Transform.Position = newPosition;
            }
        }

        private void SetPivotRotation(Entity entity, QuaternionF rotation)
        {
            if (entity.Transform.IsEnabled)
            {
                entity.Transform.PivotRotation = rotation;
            }
        }

        public Matrix4x4F CalculateFinalTransform(Camera camera, Vector3F pivotCorrection)
        {
            Matrix4x4F matrix;
            var scaling = Scale;
            var relativePosition = GetRelativePosition(camera.GetOwnerPosition());
            var finalPivot = (Vector3F)pivot;
            var scalingCenter = finalPivot;
            
            Matrix4x4F.Transformation(ref scalingCenter, ref pivotRotation, ref scaling, ref finalPivot, ref rotation, ref relativePosition, out matrix);

            var metada = GetMetadata(camera);
            metada.RelativePosition = relativePosition;
            metada.Pivot = finalPivot;
            metada.WorldMatrix = matrix;
            metada.Rotation = Rotation;
            metada.Scale = Scale;
            return matrix;
        }

        public override void CloneValues(IComponent component)
        {
            var transform = component as Transform;
            if (transform != null)
            {
                transform.Rotation = Rotation;
                transform.BaseScale = BaseScale;
                transform.ScaleFactor = ScaleFactor;
                transform.InitialPosition = InitialPosition;
                transform.Position = Position;
                transform.Pivot = pivot;
                transform.PivotRotation = PivotRotation;
            }
        }
    }
}
