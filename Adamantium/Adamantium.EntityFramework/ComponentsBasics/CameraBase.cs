using Adamantium.Mathematics;
using System;
using Adamantium.Engine.Core;
using Adamantium.Core;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public abstract class CameraBase : ActivatableComponent
    {
        private QuaternionF rotation = QuaternionF.Identity;
        private Vector3 offset;
        private uint width;
        private uint height;
        private Single fov;
        private Single fovY;
        private Single tanFov;
        private Vector3F up;
        private Vector3? lookAt;
        private Double radius;
        private bool isDepthInversed;
        private CameraType cameraType;
        private Single zNear;
        private Single zFar;
        private Single aspectRatio;
        private Entity lookAtObject;


        public QuaternionF Rotation
        {
            get => rotation;
            set => SetProperty(ref rotation, value);
        }

        public Matrix4x4F ViewMatrix { get; set; } = Matrix4x4F.Identity;

        public Matrix4x4F PerspectiveProjection { get; set; }

        public Matrix4x4F UiProjection { get; set; }

        public Matrix4x4F OrthoProjection { get; set; } = Matrix4x4F.Identity;

        public Matrix4x4F IsometricProjection { get; set; }

        public Matrix4x4F RotationMatrix => Matrix4x4F.RotationQuaternion(Rotation);

        public BoundingFrustum Frustum { get; set; }

        public Vector3? LookAt { get; set; }

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
        public Vector3 Right { get; protected set; }

        /// <summary>
        /// Camera relative Y axis from view matrix
        /// </summary>
        public Vector3 Up { get; protected set; }

        /// <summary>
        /// Camera relative Z axis from view matrix
        /// </summary>
        public Vector3 Forward { get; protected set; }

        public Vector3 Backward => -Forward;

        public Entity LookAtObject
        {
            get => lookAtObject;
            set => SetProperty(ref lookAtObject, value);
        }

        /// <summary>
        /// Gets or Sets value indicating does ZNear and ZFar planes should be reversed for inverted depth buffer
        /// </summary>
        public bool IsDepthInversed
        {
            get => isDepthInversed;
            set => SetProperty(ref isDepthInversed, value);
        }

        public UInt32 Width
        {
            get => width;
            set => SetProperty(ref width, value);
        }

        public UInt32 Height
        {
            get => height;
            set => SetProperty(ref height, value);
        }

        public float TanFov
        {
            get => tanFov;
            protected set => SetProperty(ref tanFov, value);
        }

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

        public CameraType Type
        {
            get => cameraType;
            set => SetProperty(ref cameraType, value);
        }

        public CameraProjectionType ProjectionType { get; set; }

        public virtual Matrix4x4F ProjectionMatrix
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

        ///<summary>
        ///Returns rotation matrix for camera host object all third person related properties and set the camera type to Free.
        ///</summary>
        public Matrix4x4F EntityRotationMatrix
        {
            get
            {
                if (Owner?.Owner == null)
                {
                    return Matrix4x4F.Identity;
                }

                return Matrix4x4F.RotationQuaternion(Owner.Transform.Rotation);
            }
        }

        public abstract void Update(AppTime gameTime);

        ///<summary>
        ///Rotate camera absolutely around base X, Y and Z axis.
        ///</summary>
        ///<remarks>
        ///Angles must be in degrees.
        ///</remarks>
        public abstract void SetAbsoluteRotation(float angleX, float angleY, float angleZ);

        ///<summary>
        ///Move camera along X axis relative to the current position and view direction.
        ///</summary>
        ///<remarks>
        ///relativeX: value defines distance on which camera will be moved along X axis.
        ///</remarks>
        public abstract void TranslateRight(Double relativeX);

        ///<summary>
        ///Move camera along Y axis relative to the current position and view direction.
        ///</summary>
        ///<remarks>
        ///relativeY: value defines distance on which camera will be moved along Y axis.
        ///</remarks>
        public abstract void TranslateUp(Double relativeY);

        ///<summary>
        ///Move camera along Z axis relative to the current position and view direction.
        ///</summary>
        ///<remarks>
        ///relativeZ: value defines distance on which camera will be moved along Z axis.
        ///</remarks>
        public abstract void TranslateForward(Double relativeZ);

        ///<summary>
        ///Rotate camera around X axis.
        ///</summary>
        ///<remarks>
        ///Angle must be in degrees.
        ///</remarks>
        public abstract void RotateRight(float angle);

        ///<summary>
        ///Rotate camera around Y axis.
        ///</summary>
        ///<remarks>
        ///Angle must be in degrees.
        ///</remarks>
        public abstract void RotateUp(float angle);

        ///<summary>
        ///Rotate camera around Z axis.
        ///</summary>
        ///<remarks>
        ///Angle must be in degrees.
        ///</remarks>
        public abstract void RotateForward(float angle);

        ///<summary>
        ///Rotate camera around X and Y axis.
        ///</summary>
        ///<remarks>
        ///Angles must be in degrees.
        ///</remarks>
        public abstract void RotateRelativeXY(float angleX, float angleY);

        ///<summary>
        ///Sets camera as Free with specific parameters.
        ///</summary>
        public abstract void SetFreeCamera(Vector3 position, Vector3 lookAt, Vector3 up);

        ///<summary>
        ///Sets camera as Free without any parameters
        ///</summary>
        public abstract void SetFreeCamera();

        ///<summary>
        ///Changes the Position (Offset) of Free camera.
        ///</summary>
        public abstract void SetFreePosition(Vector3 position);

        ///<summary>
        ///Changes the LookAt of Free camera.
        ///</summary>
        public abstract void SetFreeLookAt(Vector3 lookAt);

        ///<summary>
        ///Sets camera as First Person with specific parameters.
        ///</summary>
        ///<remarks>
        ///objectRotation - rotation of the camera host (e.g. position of human body (and face) in space).
        ///</remarks>
        public abstract void SetFirstPersonCamera(Vector3 position, QuaternionF objectRotation, Double faceDistance);

        ///<summary>
        ///Changes the Position (Offset) and Rotation of First Person camera.
        ///</summary>
        public abstract void SetFirstPersonPositionRotation(Vector3 position, QuaternionF objectRotation);

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
        public abstract void SetThirdPersonCamera(
            Entity hostObject,
            Vector3F initialRelRotation,
            CameraType desiredType,
            Vector3? lookAt = null,
            Double? distanceToObject = null);

        public abstract void SetSpecialCamera(Vector3 lookAt);

        ///<summary>
        ///Sets the Radius of spinning for 1st and 3rd person camera.
        ///</summary>
        public abstract void SetRadius(Double radius);

        ///<summary>
        ///Sets the LookAt of Third Person camera.
        ///</summary>
        public abstract void SetThirdPersonLookAt(Vector3 lookAt);

        ///<summary>
        ///Sets the camera's rear view.
        ///</summary>
        public abstract void SetThirdPersonLookBackwards(bool lookBackwards);

        ///<summary>
        ///Resets all third person related properties and set the camera type to Free.
        ///</summary>
        public abstract void DeleteThirdPersonConfig();

        /// <summary>
        /// Make camera jump to a certain object in the specified time
        /// </summary>
        /// <param name="lookAtObject"></param>
        /// <param name="time"></param>
        public abstract void JumpToObject(Entity lookAtObject, int time);

        ///<summary>
        ///Returns quaternion, which has the same angle as the forward vector of the hosted object in left handed coordinate system
        ///</summary>
        public QuaternionF SyncRotationWithEntityForwardLH()
        {
            return QuaternionF.RotationLookAtLH(RotationMatrix.Forward, RotationMatrix.Up);
        }

        ///<summary>
        ///Returns quaternion, which has the same angle as the backward vector of the hosted object in left handed coordinate system
        ///</summary>
        public QuaternionF SyncRotationWithEntityBackwardLH()
        {
            return QuaternionF.RotationLookAtLH(RotationMatrix.Backward, RotationMatrix.Up);
        }
    }
}
