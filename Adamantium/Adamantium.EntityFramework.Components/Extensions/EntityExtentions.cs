using System;
using Adamantium.Engine.Core;
using Adamantium.EntityFramework.Components;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Extensions
{
    public static class EntityExtentions
    {
        public static void SetWireFrame(this Entity entity, bool isWireFrame = true)
        {
            entity.TraverseInDepth(
               current =>
               {
                   var geometry = current.GetComponent<MeshRendererBase>();

                   if (geometry != null)
                   {
                       geometry.IsWireFrame = isWireFrame;
                   }
               });
        }

        public static void BringIntoView(this Entity owner, CameraBase camera)
        {
            camera?.SetThirdPersonCamera(owner, Vector3F.Zero, CameraType.ThirdPersonFree);
        }

        public static Vector3D GetCenterAbsolute(this Entity owner)
        {
            var collision = owner.GetComponent<Collider>();
            if (collision == null)
            {
                return owner.Transform.Position;
            }
            var transform = owner.Transform;
            return (collision.LocalCenter * transform.Scale) + transform.Pivot;
        }

        public static Vector3F GetLocalCenter(this Entity owner)
        {
            var collision = owner?.GetComponent<Collider>();
            if (collision == null)
            {
                return Vector3F.Zero;
            }

            return collision.LocalCenter;
        }

        public static Vector3F GetCenterRelative(this Entity owner, CameraBase camera)
        {
            var collision = owner?.GetComponent<Collider>();
            if (collision != null && collision.ContainsDataFor(camera))
            {
                return owner.Transform.GetMetadata(camera).RelativePosition + collision.LocalCenter;
            }
            return Vector3F.Zero;
        }

        public static Double GetRadius(this Entity owner)
        {
            var collision = owner.GetComponent<Collider>();

            if (collision == null)
            {
                throw new NullReferenceException("GetObjectCenter. Collision component is null");
            }
            return collision.Bounds.HalfExtent.Length();
        }

        public static Double GetDiameter(this Entity owner)
        {
            var collision = owner.GetComponent<Collider>();
            if (collision == null)
            {
                return Vector3F.Max(owner.Transform.Position);
            }
            return Vector3F.Max(collision.Bounds.Size);
        }

        public static Matrix4x4F GetActualMatrix(this Entity owner, CameraBase camera)
        {
            return owner.Transform.GetMetadata(camera).WorldMatrix;
        }

        public static Single GetDistanceToCamera(this Entity owner, CameraBase camera)
        {
            return owner.GetRelativePosition(camera).Length();
        }

        public static Vector3F GetRelativePosition(this Entity owner, CameraBase camera)
        {
            return owner.Transform.GetRelativePosition(camera.GetOwnerPosition());
        }

        public static Vector3D GetOwnerPosition(this IEntityOwner component)
        {
            return component.Owner.Transform.Position;
        }

        public static QuaternionF GetOwnerRotation(this IEntityOwner component)
        {
            return component.Owner.Transform.Rotation;
        }

        public static Vector3D GetPositionForNewObject(this Entity owner, CameraBase camera)
        {
            return camera.GetOwnerPosition() + (owner.GetDiameter() * (Vector3D)camera.Forward * camera.Fov);
        }

        public static Vector3D GetPositionForNewObject(this Entity owner, CameraBase camera, double diameter)
        {
            return camera.GetOwnerPosition() + (diameter * (Vector3D)camera.Forward * (camera.Fov/4));
        }
    }
}
