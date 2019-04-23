using System;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.EntityFramework.Extensions;
using Adamantium.Mathematics;
using BoundingFrustum = Adamantium.Mathematics.BoundingFrustum;

namespace Adamantium.EntityFramework.Components
{
    public enum CameraProjectionType
    {
        Perspective = 0,
        Ortho = 1,
        UI = 2,
        Isometric = 3
    }

    /// <summary>
    /// Component represents camera
    /// </summary>
    public class Camera : ActivatableComponent
    {
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
        public Camera(Single fov, Int32 width, Int32 height, Single znear, Single zfar, bool inverseDepth = true)
        {
            IsDepthInversed = inverseDepth;
            Rotation = QuaternionF.Identity;

            LookAt = Vector3F.ForwardLH;
            Up = Vector3F.UnitY;
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
        public Camera(Vector3F lookAt, Vector3F up, Single fov, Int32 width, Int32 height,
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
        /// Initialize view/projection matrices for camera in free mode
        /// </summary>
        public sealed override void Initialize()
        {
            GetAxisFromViewMatrix();
            BuildPerspectiveFoV();
            BuildOffscreenProjective();
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

        public CameraProjectionType ProjectionType { get; set; }
        private Vector3D offset;
        private int width;
        private int height;
        private QuaternionF rotation = QuaternionF.Identity;
        private Single fov;
        private Vector3F up;
        private Vector3D? lookAt;
        private Double radius;
        private bool isDepthInversed;

        public Matrix4x4F ViewMatrix { get; set; } = Matrix4x4F.Identity;

        public Matrix4x4F PerspectiveProjection { get; set; }

        public Matrix4x4F UiProjection { get; set; }

        public Matrix4x4F OrthoProjection { get; set; } = Matrix4x4F.Identity;

        public Matrix4x4F IsometricProjection { get; set; }

        public Matrix4x4F ProjectionMatrix
        {
            get
            {
                switch (ProjectionType)
                {
                    case CameraProjectionType.Perspective:
                        return PerspectiveProjection;
                    case CameraProjectionType.Ortho:
                        return OrthoProjection;
                    case CameraProjectionType.UI:
                        return UiProjection;
                    case CameraProjectionType.Isometric:
                        return IsometricProjection;
                    default:
                        return PerspectiveProjection;
                }
            }
        }

        private Matrix4x4F _viewProjectionMatrix;

        public Matrix4x4F ViewProjectionMatrix => _viewProjectionMatrix;

        public BoundingFrustum Frustum { get; private set; }


        public Int32 Width
        {
            get => width;
            set => SetProperty(ref width, value);
        }

        public Int32 Height
        {
            get => height;
            set => SetProperty(ref height, value);
        }

        private float _tanFov;

        public float TanFov => _tanFov;

        public Single Fov
        {
            get => fov;
            set => SetProperty(ref fov, value);
        }

        public Single FovY
        {
            get => fovY;
            set => SetProperty(ref fovY, value);
        }

        public Single ZNear
        {
            get => zNear;
            set => SetProperty(ref zNear, value);
        }

        public Single ZFar
        {
            get => zFar;
            set => SetProperty(ref zFar, value);
        }

        public Single AspectRatio
        {
            get => aspectRatio;
            set => SetProperty(ref aspectRatio, value);
        }

        // ------- 3rd person related
        public Double Radius
        {
            get => radius;
            set => SetProperty(ref radius, value);
        }

        public Vector3F HostUpVector { get; set; }

        public bool IsLookingBackwards { get; set; }

        public QuaternionF FormerRotation { get; set; }
        // ------- 3rd person related END

        private CameraType cameraType;

        public CameraType Type
        {
            get => cameraType;
            set => SetProperty(ref cameraType, value);
        }

        //public Vector3D Offset
        //{
        //    get
        //    {
        //        return offset;
        //    }
        //    set
        //    {
        //        if (offset == value)
        //        {
        //            return;
        //        }

        //        offset = value;
        //        IsInActualState = false;
        //    }
        //}

        public QuaternionF Rotation
        {
            get => rotation1;
            set => SetProperty(ref rotation1, value);
        }

        public Vector3D? LookAt
        {
            get => lookAt;
            set => SetProperty(ref lookAt, value);
        }

        public Double Velocity { get; set; }

        public Double DragVelocity { get; set; }

        public Double CurrentVelocity { get; set; }

        public Double WheelVelocity { get; set; }

        public Single RotationSpeed { get; set; }

        public Single MouseSensitivity { get; set; }

        public Boolean IsUserControlled { get; set; }

        /// <summary>
        /// Gets or gets value does this camera is currently active (bind to window and displaying content)
        /// </summary>
        public Boolean IsActive { get; set; }

        /// <summary>
        /// Camera relative X axis from view matrix
        /// </summary>
        public Vector3F Right { get; private set; }

        /// <summary>
        /// Camera relative Y axis from view matrix
        /// </summary>
        public Vector3F Up { get; private set; }

        /// <summary>
        /// Camera relative Z axis from view matrix
        /// </summary>
        public Vector3F Forward { get; private set; }

        public Vector3F Backward => -Forward;

        /// <summary>
        /// Gets or Sets value indicating does ZNear and ZFar planes should be reversed for inverted depth buffer
        /// </summary>
        public bool IsDepthInversed
        {
            get => isDepthInversed;
            set => SetProperty(ref isDepthInversed, value);
        }

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
            if (IsDepthInversed)
            {
                znear = ZFar;
                zfar = ZNear;
            }

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

        public Single OrthoScaleFactor = 1;

        private void BuildOffscreenProjective()
        {
            float znear = ZNear;
            float zfar = ZFar;
            if (IsDepthInversed)
            {
                znear = ZFar;
                zfar = ZNear;
            }
            OrthoProjection = Matrix4x4F.OrthoLH(Width / OrthoScaleFactor, Height / OrthoScaleFactor, zfar, znear);
            UiProjection = Matrix4x4F.OrthoOffCenterLH(0, Width, Height, 0, zfar, znear);
        }

        private void BuildPerspectiveFovX(float zNear, float zFar)
        {
            float aspect = (float)Width / Height;
            float e = 1.0f / (float)Math.Tan(MathHelper.DegreesToRadians(Fov / 2.0f));
            float aspectInv = 1.0f / aspect;
            float fovX = 2.0f * (float)Math.Atan(aspectInv / e);
            float xScale = 1.0f / (float)Math.Tan(0.5f * fovX);
            float yScale = xScale / aspectInv;
            var fovY = (float)(2 * Math.Atan(Math.Tan(Fov / 2.0f) * ((float)Height / Width)));
            FovY = MathHelper.RadiansToDegrees(fovY);
            _tanFov = (float)Math.Tan(MathHelper.DegreesToRadians(Fov));
            PerspectiveProjection = new Matrix4x4F
            {
                M11 = xScale,
                M22 = yScale,
                M33 = zFar / (zFar - zNear),
                M34 = 1.0f,
                M43 = -zNear * zFar / (zFar - zNear),
            };
        }

        private void BuildPerspectiveFovY(float zNear, float zFar)
        {
            PerspectiveProjection = Matrix4x4F.PerspectiveFovLH(Fov, (float)Width / Height, zNear,
                zFar);
        }

        private void GetAxisFromViewMatrix()
        {
            Right = new Vector3F(ViewMatrix.M11, ViewMatrix.M21, ViewMatrix.M31);
            Up = new Vector3F(ViewMatrix.M12, ViewMatrix.M22, ViewMatrix.M32);
            Forward = new Vector3F(ViewMatrix.M13, ViewMatrix.M23, ViewMatrix.M33);
        }

        private QuaternionF rotationToSync;
        private int rotationTime;
        private bool rotationDone = true;
        private double rotationDuration;
        private QuaternionF startingRotation;
        private Vector3D lookAtRotationPoint;
        
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
                diameter = ((Vector3D)Forward * LookAtObject.GetDiameter() * Fov);
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

        public void SetLookAtObject(Entity lookAtObject)
        {
            LookAtObject = lookAtObject;
        }

        private Entity LookAtObject;


        private int moveTime;
        private bool moveToObjectDone = true;
        private double moveToDuration = 0;
        private Vector3D startingOffset;
        private Vector3D endingPosition;
        private Vector3D distance;
        private Vector3D diameter;
        private Vector3D center;
        private float fovY;
        private float zNear;
        private float zFar;
        private float aspectRatio;
        private QuaternionF rotation1;

        public void JumpToObject(Entity lookAtObject, int time)
        {
            SetLookAtObject(lookAtObject);
            moveTime = time;
            moveToObjectDone = false;
            moveToDuration = 0;
            startingOffset = Owner.Transform.Position;
            diameter = ((Vector3D) Forward * lookAtObject.GetDiameter() * Fov);
            endingPosition = lookAtObject.GetCenterAbsolute() - diameter;
            Type = CameraType.Free;
        }

        private void MoveToPoint(IGameTime gameTime)
        {
            if (!moveToObjectDone)
            {
                moveToDuration += gameTime.FrameTime * 1000;
                var weight = (float)moveToDuration / moveTime;
                Owner.Transform.Position = Vector3D.Lerp(startingOffset, endingPosition, weight);
                if (moveToDuration >= moveTime)
                {
                    Owner.Transform.Position = endingPosition;
                    moveToObjectDone = true;
                    moveToDuration = 0;
                    SetFreeCamera();
                }
            }
        }


        public void Update(IGameTime gameTime)
        {
            Rotation.Normalize();
            MoveToPoint(gameTime);
            if (Type == CameraType.Free)
            {
                ViewMatrix = Matrix4x4F.RotationQuaternion(Rotation);
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
                    Owner.Transform.Position = (center) - new Vector3D(rotMatrix.M13, rotMatrix.M23, rotMatrix.M33) * distance.Length();
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
                Owner.Transform.Position = center1 - Vector3D.Multiply(new Vector3D(rotMatrix.M13, rotMatrix.M23, rotMatrix.M33), Radius);

                if (LookAt != null)
                {
                    ViewMatrix = Matrix4x4F.LookAtLH(
                        Vector3F.Zero,
                        (Vector3D) LookAt - Owner.Transform.Position,
                        new Vector3F(rotMatrix.M12, rotMatrix.M22, rotMatrix.M32));
                }
                else
                {
                    ViewMatrix = Matrix4x4F.LookAtLH(
                        Vector3F.Zero,
                        center1 - Owner.Transform.Position,
                        new Vector3F(rotMatrix.M12, rotMatrix.M22, rotMatrix.M32));
                }

            }

            GetAxisFromViewMatrix();
            UpdateFrustum();
            _viewProjectionMatrix = ViewMatrix * ProjectionMatrix;
        }

        ///<summary>
        ///Rotate camera absolutely around base X, Y and Z axis.
        ///</summary>
        ///<remarks>
        ///Angles must be in degrees.
        ///</remarks>
        public void SetAbsoluteRotation(float angleX, float angleY, float angleZ)
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

        ///<summary>
        ///Move camera along X axis relative to the current position and view direction.
        ///</summary>
        ///<remarks>
        ///relativeX: value defines distance on which camera will be moved along X axis.
        ///</remarks>
        public void TranslateRight(Double relativeX)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position += Vector3D.Multiply(Right, relativeX);
            }
        }

        ///<summary>
        ///Move camera along Y axis relative to the current position and view direction.
        ///</summary>
        ///<remarks>
        ///relativeY: value defines distance on which camera will be moved along Y axis.
        ///</remarks>
        public void TranslateUp(Double relativeY)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position += Vector3D.Multiply(Up, relativeY);
            }
        }

        ///<summary>
        ///Move camera along Z axis relative to the current position and view direction.
        ///</summary>
        ///<remarks>
        ///relativeZ: value defines distance on which camera will be moved along Z axis.
        ///</remarks>
        public void TranslateForward(Double relativeZ)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position += Vector3D.Multiply(Forward, relativeZ);
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

        ///<summary>
        ///Rotate camera around X and Y axis.
        ///</summary>
        ///<remarks>
        ///Angles must be in degrees.
        ///</remarks>
        public void RotateRelativeXY(float angleX, float angleY)
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

        ///<summary>
        ///Rotate camera around X axis.
        ///</summary>
        ///<remarks>
        ///Angle must be in degrees.
        ///</remarks>
        public void RotateRight(float angle)
        {
            if ((Type == CameraType.ThirdPersonFree) || (Type == CameraType.ThirdPersonFreeAlt) ||
                (Type == CameraType.ThirdPersonLocked))
            {
                angle = -angle;
            }

            Rotation = QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitX, MathHelper.DegreesToRadians(angle)), Rotation);
        }

        ///<summary>
        ///Rotate camera around Y axis.
        ///</summary>
        ///<remarks>
        ///Angle must be in degrees.
        ///</remarks>
        public void RotateUp(float angle)
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

        ///<summary>
        ///Rotate camera around Z axis.
        ///</summary>
        ///<remarks>
        ///Angle must be in degrees.
        ///</remarks>
        public void RotateForward(float angle)
        {
            if (Type != CameraType.ThirdPersonLocked)
            {
                Rotation =
                   QuaternionF.Multiply(QuaternionF.RotationAxis(Vector3F.UnitZ, MathHelper.DegreesToRadians(angle)),
                      Rotation);
            }
        }

        ///<summary>
        ///Sets camera as Free with specific parameters.
        ///</summary>
        public void SetFreeCamera(Vector3D position, Vector3D lookAt, Vector3F up)
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

        ///<summary>
        ///Sets camera as Free without any parameters
        ///</summary>
        public void SetFreeCamera()
        {
            DeleteThirdPersonConfig(); // sets camera type to Free here (inside method)
            Type = CameraType.Free;
        }

        ///<summary>
        ///Changes the Position (Offset) of Free camera.
        ///</summary>
        public void SetFreePosition(Vector3D position)
        {
            if (Type == CameraType.Free)
            {
                Owner.Transform.Position = position;
            }
        }

        ///<summary>
        ///Changes the LookAt of Free camera.
        ///</summary>
        public void SetFreeLookAt(Vector3D lookAt)
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

        ///<summary>
        ///Sets camera as First Person with specific parameters.
        ///</summary>
        ///<remarks>
        ///objectRotation - rotation of the camera host (e.g. position of human body (and face) in space).
        ///</remarks>
        public void SetFirstPersonCamera(Vector3D position, QuaternionF objectRotation, Double faceDistance)
        {
            Type = CameraType.FirstPerson;

            Owner.Transform.Position = position;
            Radius = faceDistance;
            Rotation = objectRotation;
        }

        ///<summary>
        ///Changes the Position (Offset) and Rotation of First Person camera.
        ///</summary>
        public void SetFirstPersonPositionRotation(Vector3D position, QuaternionF objectRotation)
        {
            if (Type == CameraType.FirstPerson)
            {
                Owner.Transform.Position = position;
                Rotation = objectRotation;
            }
        }

        ///<summary>
        ///Sets camera as Third Person with specific parameters.
        ///</summary>
        ///<remarks>
        ///<param name="hostObject">the host object of the camera. Camera spins around host object's center.</param>
        ///<param name="initialRelRotation">initial rotation relative to host object's rotation.</param>
        ///<param name="lookAt">initial point at which camera is looking at creation (not always the same as center!).</param>
        ///<param name="distanceToObject">initial distance from host object's center</param>
        ///<param name="desiredType">is camera mode alternative (spinning around self Y axis (alt = false) or host object Y axis (alt = true)) - ignored, if isLocked is true.</param>
        ///</remarks>
        public void SetThirdPersonCamera(Entity hostObject, Vector3F initialRelRotation, CameraType desiredType,
           Vector3D? lookAt = null, Double? distanceToObject = null)
        {
            if (!desiredType.IsThirdPerson() || hostObject == Owner)
            {
                return;
            }
            if (hostObject != null)
            {
                Owner.Owner = hostObject;

                HostUpVector = GetEntityRotationMatrix().Up;

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

        public void SetSpecialCamera(Vector3D lookAt)
        {
            Type = CameraType.Special;

            LookAt = lookAt;
        }

        ///<summary>
        ///Sets the Radius of spinning for 1st and 3rd person camera.
        ///</summary>
        public void SetRadius(Double radius)
        {
            if (Type != CameraType.Free)
            {
                Radius = radius;
            }
        }

        ///<summary>
        ///Sets the LookAt of Third Person camera.
        ///</summary>
        public void SetThirdPersonLookAt(Vector3D lookAt)
        {
            if ((Type == CameraType.ThirdPersonFree) || (Type == CameraType.ThirdPersonFreeAlt))
            {
                LookAt = lookAt;
            }
        }

        ///<summary>
        ///Sets the camera's rear view.
        ///</summary>
        public void SetThirdPersonLookBackwards(bool lookBackwards)
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

        ///<summary>
        ///Resets all third person related properties and set the camera type to Free.
        ///</summary>
        public void DeleteThirdPersonConfig()
        {
            Type = CameraType.Free;
            Owner.Owner = null;
        }

        ///<summary>
        ///Returns rotation matrix for camera host object all third person related properties and set the camera type to Free.
        ///</summary>
        public Matrix4x4F GetEntityRotationMatrix()
        {
            if (Owner?.Owner == null)
            {
                return Matrix4x4F.Identity;
            }
            return Matrix4x4F.RotationQuaternion(Owner.Transform.Rotation);
        }

        public Matrix4x4F GetCameraRotationMatrix()
        {
            return Matrix4x4F.RotationQuaternion(Rotation);
        }

        ///<summary>
        ///Returns quaternion, which has the same angle as the forward vector of the hosted object in left handed coordinate system
        ///</summary>
        public QuaternionF SyncRotationWithEntityForwardLH()
        {
            var rotMatr = GetEntityRotationMatrix();
            return QuaternionF.RotationLookAtLH(rotMatr.Forward, rotMatr.Up);

        }

        ///<summary>
        ///Returns quaternion, which has the same angle as the backward vector of the hosted object in left handed coordinate system
        ///</summary>
        public QuaternionF SyncRotationWithEntityBackwardLH()
        {
            var rotMatr = GetEntityRotationMatrix();
            return QuaternionF.RotationLookAtLH(rotMatr.Backward, rotMatr.Up);
        }

    }
}
