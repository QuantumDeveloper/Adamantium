using System;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.Components.Extensions;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components
{
    /// <summary>
    /// Component represents camera
    /// </summary>
    public class Camera : CameraBase
    {
        private QuaternionF rotationToSync;
        private int rotationTime;
        private bool rotationDone = true;
        private double rotationDuration;
        private QuaternionF startingRotation;
        private Vector3 lookAtRotationPoint;
        private int moveTime;
        private bool moveToObjectDone = true;
        private double moveToDuration = 0;
        private Vector3 startingOffset;
        private Vector3 endingPosition;
        private Vector3 distance;
        private Vector3 diameter;
        private Vector3 center;

        public Single OrthoScaleFactor = 1;

        public Camera()
        {
        }

        /// <summary>
        /// Initializes camera instance from field of view , window width, window height, near and far planes and initial depth state
        /// </summary>
        /// <param name="fov">Field of view</param>
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// <param name="znear">Distance to near plane</param>
        /// <param name="zfar">Distance to far plane</param>
        /// <param name="inverseDepth">Is depth should be inverted</param>
        /// <remarks>If <paramref name="inverseDepth"/> is true, then znear and zfar will be swap when creating projection matrix</remarks>
        public Camera(Single fov, UInt32 width, UInt32 height, Single znear, Single zfar, bool inverseDepth = true)
        {
            IsDepthInversed = !inverseDepth;
            Rotation = QuaternionF.Identity;

            LookAt = Vector3F.ForwardLH;
            Up = -Vector3F.UnitY;
            Velocity = 1;
            CurrentVelocity = 0;
            WheelVelocity = 1;
            DragVelocity = 200;
            Width = width;
            Height = height;

            Fov = fov;
            AspectRatio = (float)width / height;
            ZNear = znear;
            ZFar = zfar;

            RotationSpeed = 35f;
            MouseSensitivity = 0.8f;
            Type = CameraType.Free;

            IsLookingBackwards = false;

            Initialize();
        }

        /// <summary>
        /// Initializes camera instance from position, lookAt vector, Up vector, field of view , window width, window height, near and far planes and initial depth state
        /// </summary>
        /// <param name="lookAt">Look at vector defining forward direction</param>
        /// <param name="up">Up vector defining initial up direction</param>
        /// <param name="fov">Field of view</param>
        /// <param name="width">Window width</param>
        /// <param name="height">Window height</param>
        /// <param name="znear">Distance to near plane</param>
        /// <param name="zfar">Distance to far plane</param>
        /// <param name="inverseDepth">Is depth should be inverted</param>
        /// <remarks>If <paramref name="inverseDepth"/> is true, then znear and zfar will be swap when creating projection matrix</remarks>
        public Camera(Vector3F lookAt, Vector3F up, Single fov, UInt32 width, UInt32 height,
           Single znear, Single zfar, bool inverseDepth = true)
        {
            IsDepthInversed = inverseDepth;
            Rotation = QuaternionF.Identity;
            LookAt = lookAt;
            Up = up;
            Velocity = 1;
            CurrentVelocity = 0;
            WheelVelocity = 1;
            DragVelocity = 200;
            Width = width;
            Height = height;

            Fov = fov;
            AspectRatio = (float)width / height;
            ZNear = znear;
            ZFar = zfar;

            RotationSpeed = 35f;
            MouseSensitivity = 5.0f;
            Type = CameraType.Free;

            IsLookingBackwards = false;
            Initialize();
        }

        /// <summary>
        /// Initialize view/projection matrices for camera
        /// </summary>
        public sealed override void Initialize()
        {
            GetAxisFromViewMatrix();
            BuildPerspectiveFoV();
            BuildOrthoProjection();
            BuildUiProjection();
            BuildIsometricProjection();
            Frustum = new BoundingFrustum(ViewMatrix * ProjectionMatrix);
            base.Initialize();
        }

        /// <summary>
        /// Updates camera`s frustum
        /// </summary>
        public void UpdateFrustum()
        {
            Frustum.Matrix4x4F = ViewMatrix * ProjectionMatrix;
        }

        private Matrix4x4F _viewProjectionMatrix;

        public Matrix4x4F ViewProjectionMatrix => _viewProjectionMatrix;

        ///<summary>
        ///Decides which algorithm will be used for building the field of view.
        ///</summary>
        ///<remarks>
        ///Uses Camera Component as input.
        ///</remarks>
        public void BuildPerspectiveFoV()
        {
            float znear = ZNear;
            float zfar = ZFar;
            // if (IsDepthInversed)
            // {
            //     znear = ZFar;
            //     zfar = ZNear;
            // }

            if (Width > Height)
            {
                BuildPerspectiveFovX(znear, zfar);
            }
            else
            {
                BuildPerspectiveFovY(znear, zfar);
            }
        }

        public void BuildIsometricProjection()
        {
            IsometricProjection = Matrix4x4F.IsometricProjection(0.5f);
        }

        private void BuildOrthoProjection()
        {
            OrthoProjection = Matrix4x4F.OrthoLH(Width / OrthoScaleFactor, Height / OrthoScaleFactor, ZNear, ZFar);
        }
        

        private void BuildUiProjection()
        {
            float znear = ZNear;
            float zfar = ZFar;
            // if (IsDepthInversed)
            // {
            //     znear = ZFar;
            //     zfar = ZNear;
            // }
            
            UiProjection = Matrix4x4F.OrthoOffCenter(0, Width, 0, Height, znear, zfar);
        }

        private void BuildPerspectiveFovX(float zNear, float zFar)
        {
            PerspectiveProjection = Matrix4x4F.PerspectiveFovX(Fov, (float)Width / Height, zNear, zFar);
        }

        private void BuildPerspectiveFovY(float zNear, float zFar)
        {
            PerspectiveProjection = Matrix4x4F.PerspectiveFovY(Fov, (float)Width / Height, zNear, zFar);
        }

        private void GetAxisFromViewMatrix()
        {
            Right = new Vector3F(ViewMatrix.M11, ViewMatrix.M21, ViewMatrix.M31);
            Up = new Vector3F(ViewMatrix.M12, ViewMatrix.M22, ViewMatrix.M32);
            Forward = new Vector3F(ViewMatrix.M13, ViewMatrix.M23, ViewMatrix.M33);
        }

        /// <inheritdoc />
        public void RotateAroundSelectedObject(QuaternionF newRotation, int time)
        {
            rotationDuration = 0;
            rotationToSync = newRotation;
            rotationTime = time;
            rotationDone = false;
            startingRotation = Rotation;
            startingOffset = Owner.Transform.Position;
            if (LookAtObject != null)
            {
                diameter = (Vector3)Forward * LookAtObject.GetDiameter() * Fov;
                center = LookAtObject.GetCenterAbsolute() + diameter + Owner.Transform.Position - LookAtObject.GetLocalCenter();
            }
            Type = CameraType.Special;
        }

        private void ContiniousRotation(IGameTime gameTime)
        {
            if (!rotationDone)
            {
                rotationDuration += gameTime.FrameTime * 1000;
                var weight = (float) rotationDuration / rotationTime;
                Rotation = QuaternionF.Lerp(startingRotation, rotationToSync, weight);
                if (rotationDuration >= rotationTime)
                {
                    Rotation = rotationToSync;
                    rotationDone = true;
                    rotationDuration = 0;
                    SetFreeCamera();
                }
            }
        }

        /// <inheritdoc />
        public override void JumpToObject(Entity lookAtObject, int time)
        {
            LookAtObject = lookAtObject;
            moveTime = time;
            moveToObjectDone = false;
            moveToDuration = 0;
            startingOffset = Owner.Transform.Position;
            diameter = ((Vector3) Forward * lookAtObject.GetDiameter() * Fov);
            endingPosition = lookAtObject.GetCenterAbsolute() - diameter;
            Type = CameraType.Free;
        }

        private void MoveToPoint(IGameTime gameTime)
        {
            if (!moveToObjectDone)
            {
                moveToDuration += gameTime.FrameTime * 1000;
                var weight = (float)moveToDuration / moveTime;
                Owner.Transform.Position = Vector3.Lerp(startingOffset, endingPosition, weight);
                if (moveToDuration >= moveTime)
                {
                    Owner.Transform.Position = endingPosition;
                    moveToObjectDone = true;
                    moveToDuration = 0;
                    SetFreeCamera();
                }
            }
        }

        public override void Update(IGameTime gameTime)
        {
            Rotation.Normalize();
            MoveToPoint(gameTime);
            if (Type == CameraType.Free)
            {
                ViewMatrix = Matrix4x4F.RotationQuaternion(Rotation);
                //ViewMatrix =  Matrix4x4F.LookAtRH(new Vector3F(0, 0, 0), Vector3F.Zero, Vector3F.Up);
                //ViewMatrix.Transpose();
            }
            else if (Type == CameraType.FirstPerson)
            {
                //TODO implement
            }
            else if (Type == CameraType.Special)
            {
                ContiniousRotation(gameTime);
                if (LookAtObject == null)
                {
                    ViewMatrix = Matrix4x4F.RotationQuaternion(Rotation);
                }
                else
                {
                    Matrix4x4F rotMatrix = Matrix4x4F.RotationQuaternion(Rotation);
                    distance = center - Owner.Transform.Position;
                    Owner.Transform.Position = (center) - new Vector3(rotMatrix.M13, rotMatrix.M23, rotMatrix.M33) * distance.Length();
                    ViewMatrix = Matrix4x4F.LookToLH(Vector3F.Zero, -new Vector3F(rotMatrix.M13, rotMatrix.M23, rotMatrix.M33), new Vector3F(rotMatrix.M12, rotMatrix.M22, rotMatrix.M32));
                }
            }
            else
            {
                QuaternionF tmpRotation;

                if (Type == CameraType.ThirdPersonLocked)
                {
                    tmpRotation = SyncRotationWithEntityForwardLH();
                    tmpRotation = QuaternionF.Multiply(Rotation, tmpRotation);
                }
                else
                {
                    tmpRotation = Rotation;
                }

                Matrix4x4F rotMatrix = Matrix4x4F.RotationQuaternion(tmpRotation);
                var center1 = Owner.Owner.GetCenterAbsolute();
                Owner.Transform.Position = center1 - Vector3.Multiply(new Vector3(rotMatrix.M13, rotMatrix.M23, rotMatrix.M33), Radius);

                if (LookAt != null)
                {
                    ViewMatrix = Matrix4x4F.LookAtRH(
                        Vector3F.Zero,
                        (Vector3) LookAt - Owner.Transform.Position,
                        new Vector3F(rotMatrix.M12, rotMatrix.M22, rotMatrix.M32));
                }
                else
                {
                    ViewMatrix = Matrix4x4F.LookAtRH(
                        Vector3F.Zero,
                        center1 - Owner.Transform.Position,
                        new Vector3F(rotMatrix.M12, rotMatrix.M22, rotMatrix.M32));
                }

            }

            GetAxisFromViewMatrix();
            UpdateFrustum();
            _viewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        }

        /// <inheritdoc />
        public override void SetAbsoluteRotation(float angleX, float angleY, float angleZ)
        {
            if (Type != CameraType.ThirdPersonLocked)
            {
                Rotation = QuaternionF.RotationYawPitchRoll(angleY, angleX, angleZ);
            }
            else
            {
                Rotation = QuaternionF.RotationYawPitchRoll(0, angleX, 0);
            }
        }

        /// <inheritdoc />
        public override void TranslateRight(Double relativeX)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position += Vector3.Multiply(Right, relativeX);
            }
        }

        /// <inheritdoc />
        public override void TranslateUp(Double relativeY)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position += Vector3.Multiply(Up, relativeY);
            }
        }

        /// <inheritdoc />
        public override void TranslateForward(Double relativeZ)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position += Vector3.Multiply(Forward, relativeZ);
            }
            else if (Type == CameraType.FirstPerson)
            {
                //@TODO implement
            }
            else
            {
                Radius -= relativeZ;
            }
        }

        /// <inheritdoc />
        public override void RotateRelativeXY(float angleX, float angleY)
        {
            if ((Type == CameraType.ThirdPersonFree) || (Type == CameraType.ThirdPersonFreeAlt) ||
                (Type == CameraType.ThirdPersonLocked))
            {
                angleX = -angleX;
                angleY = -angleY;
            }

            if (Type == CameraType.FirstPerson)
            {
                //@TODO implement
            }
            else if (Type == CameraType.ThirdPersonLocked)
            {
                Rotation =
                   QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(angleX)),
                      Rotation);
            }
            else if (Type == CameraType.ThirdPersonFreeAlt)
            {
                Rotation =
                   QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(angleX)),
                      Rotation);
                Rotation = QuaternionF.Multiply(Rotation, QuaternionF.RotationAxis(HostUpVector, MathHelper.DegreesToRadians(angleY)));
            }
            else
            {
                Rotation =
                   QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(angleX)), Rotation);
                Rotation =
                   QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitY, MathHelper.DegreesToRadians(angleY)), Rotation);
            }
        }

        /// <inheritdoc />
        public override void RotateRight(float angle)
        {
            if ((Type == CameraType.ThirdPersonFree) || (Type == CameraType.ThirdPersonFreeAlt) ||
                (Type == CameraType.ThirdPersonLocked))
            {
                angle = -angle;
            }

            Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(angle)), Rotation);
        }

        /// <inheritdoc />
        public override void RotateUp(float angle)
        {
            if (Type != CameraType.ThirdPersonLocked)
            {
                if ((Type == CameraType.ThirdPersonFree) ||
                    (Type == CameraType.ThirdPersonFreeAlt))
                {
                    angle = -angle;
                }

                if (Type == CameraType.ThirdPersonFreeAlt)
                {
                    Rotation = QuaternionF.Multiply(Rotation, QuaternionF.RotationAxis(HostUpVector, MathHelper.DegreesToRadians(angle)));
                }
                else
                {
                    Rotation =
                       QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitY, MathHelper.DegreesToRadians(angle)),
                          Rotation);
                }
            }
        }

        /// <inheritdoc />
        public override void RotateForward(float angle)
        {
            if (Type != CameraType.ThirdPersonLocked)
            {
                Rotation =
                   QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitZ, MathHelper.DegreesToRadians(angle)),
                      Rotation);
            }
        }

        /// <inheritdoc />
        public override void SetFreeCamera(Vector3 position, Vector3 lookAt, Vector3F up)
        {
            DeleteThirdPersonConfig(); // sets camera type to Free here (inside method)

            LookAt = lookAt;
            Up = up;
            ViewMatrix = Matrix4x4F.LookAtLH(Vector3F.Zero, lookAt, up);

            // stub variables for Decompose function
            Vector3F scale, translation;
            QuaternionF decomposedRotation;
            ViewMatrix.Decompose(out scale, out decomposedRotation, out translation);
            Rotation = decomposedRotation;
            Type = CameraType.Free;
        }

        /// <inheritdoc />
        public override void SetFreeCamera()
        {
            DeleteThirdPersonConfig(); // sets camera type to Free here (inside method)
            Type = CameraType.Free;
        }

        /// <inheritdoc />
        public override void SetFreePosition(Vector3 position)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position = position;
            }
        }

        /// <inheritdoc />
        public override void SetFreeLookAt(Vector3 lookAt)
        {
            if (Type == CameraType.Free)
            {
                // convert to offset world presentation (float)
                lookAt -= Owner.Transform.Position;
                Vector3F up = new Vector3F(ViewMatrix.M12, ViewMatrix.M22, ViewMatrix.M32);

                ViewMatrix = Matrix4x4F.LookAtLH(Vector3F.Zero, lookAt, up);

                // stub variables for Decompose function
                Vector3F scale, translation;
                QuaternionF decomposedRotation;
                ViewMatrix.Decompose(out scale, out decomposedRotation, out translation);
                Rotation = decomposedRotation;
            }
        }

        /// <inheritdoc />
        public override void SetFirstPersonCamera(Vector3 position, QuaternionF objectRotation, Double faceDistance)
        {
            Type = CameraType.FirstPerson;

            Owner.Transform.Position = position;
            Radius = faceDistance;
            Rotation = objectRotation;
        }

        /// <inheritdoc />
        public override void SetFirstPersonPositionRotation(Vector3 position, QuaternionF objectRotation)
        {
            if (Type == CameraType.FirstPerson)
            {
                Owner.Transform.Position = position;
                Rotation = objectRotation;
            }
        }

        /// <inheritdoc />
        public override void SetThirdPersonCamera(Entity hostObject, Vector3F initialRelRotation, CameraType desiredType,
           Vector3? lookAt = null, Double? distanceToObject = null)
        {
            if (!desiredType.IsThirdPerson() || hostObject == Owner)
            {
                return;
            }
            if (hostObject != null)
            {
                Owner.Owner = hostObject;

                HostUpVector = EntityRotationMatrix.Up;

                LookAt = lookAt;
                if (desiredType.IsThirdPerson())
                {
                    if (distanceToObject != null)
                    {
                        Radius = (Double)distanceToObject;
                    }
                    else
                    {
                        Radius = hostObject.GetDiameter() * (Fov/2);
                    }
                }

                if (desiredType != CameraType.ThirdPersonLocked)
                {
                    Rotation = SyncRotationWithEntityForwardLH();
                    Rotation *= QuaternionF.RotationYawPitchRoll(MathHelper.DegreesToRadians(initialRelRotation.Y), MathHelper.DegreesToRadians(initialRelRotation.X), MathHelper.DegreesToRadians(initialRelRotation.Z));
                    //Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(initialRelRotation.X)), Rotation);
                    //Rotation = QuaternionF.Multiply(Rotation, QuaternionF.RotationAxis(HostUpVector, MathHelper.DegreesToRadians(initialRelRotation.Y)));
                    //Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitZ, MathHelper.DegreesToRadians(initialRelRotation.Z)), Rotation);
                }
                else
                {
                    Rotation = QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(initialRelRotation.X));
                }
                Type = desiredType;
            }
        }

        /// <inheritdoc />
        public override void SetSpecialCamera(Vector3 lookAt)
        {
            Type = CameraType.Special;

            LookAt = lookAt;
        }

        /// <inheritdoc />
        public override void SetRadius(Double radius)
        {
            if (Type != CameraType.Free)
            {
                Radius = radius;
            }
        }

        /// <inheritdoc />
        public override void SetThirdPersonLookAt(Vector3 lookAt)
        {
            if ((Type == CameraType.ThirdPersonFree) || (Type == CameraType.ThirdPersonFreeAlt))
            {
                LookAt = lookAt;
            }
        }

        /// <inheritdoc />
        public override void SetThirdPersonLookBackwards(bool lookBackwards)
        {
            if (lookBackwards != IsLookingBackwards)
            {
                if ((Type == CameraType.ThirdPersonFree) ||
                    (Type == CameraType.ThirdPersonFreeAlt))
                {
                    if (Owner != null)
                    {
                        IsLookingBackwards = lookBackwards;

                        if (lookBackwards == true)
                        {
                            FormerRotation = Rotation;

                            Rotation = SyncRotationWithEntityBackwardLH();
                        }
                        else
                        {
                            Rotation = FormerRotation;
                        }
                    }
                    else
                    {
                        IsLookingBackwards = false; // for robustness
                    }
                }
            }
        }

        /// <inheritdoc />
        public override void DeleteThirdPersonConfig()
        {
            Type = CameraType.Free;
            Owner.Owner = null;
        }
    }
}
