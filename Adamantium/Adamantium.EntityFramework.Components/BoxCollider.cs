using System.Collections.Generic;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.Components
{
    public class BoxCollider : Collider
    {
        private OrientedBoundingBox obb;
        public BoxCollider()
        {
            ColliderData = new Dictionary<CameraBase, OrientedBoundingBox>();
        }
        
        protected Dictionary<CameraBase, OrientedBoundingBox> ColliderData { get; }

        public override void ClearData()
        {
            ColliderData.Clear();
        }

        public override bool ContainsDataFor(CameraBase camera)
        {
            return ColliderData.ContainsKey(camera);
        }

        public override Mesh GetVisualRepresentation()
        {
            Geometry = Shapes.Cube.GenerateGeometry(GeometryType.Outlined, Bounds.Size.X, Bounds.Size.Y, Bounds.Size.Z, 1, Matrix4x4.Translation(Bounds.Center));
            return Geometry;
        }

        public override void UpdateForCamera(CameraBase camera)
        {
            var bb = obb.Transform(Owner.Transform.Scale, Owner.Transform.Rotation, Owner.Transform.GetMetadata(camera).RelativePosition);

            ColliderData[camera] = bb;
        }

        public override ContainmentType IsInsideCameraFrustum(Camera camera)
        {
            if (ColliderData.TryGetValue(camera, out var bb))
            {
                return camera.Frustum.Contains(bb.GetCorners());
            }
            return camera.Frustum.Contains(obb.GetCorners());
        }

        public override void Transform(ref Vector3F scale, ref QuaternionF rotation, ref Vector3F translation)
        {
            obb = obb.Transform(scale, rotation, translation);
        }

        public override void Transform(ref float uniformScale, ref QuaternionF rotation, ref Vector3F translation)
        {
            obb = obb.Transform(uniformScale, rotation, translation);
        }

        public override void CalculateFromMesh(Mesh mesh)
        {
            obb = mesh.Bounds;
            base.CalculateFromMesh(mesh);
        }

        public void CalculateFromPoints(Vector3F[] points)
        {
            obb = OrientedBoundingBox.FromPoints(points);
            Bounds = Bounds.FromBoundingBox(obb);
        }

        public override void Merge(Collider collider)
        {
            var bounds = collider.Bounds;
            var obb0 = (OrientedBoundingBox)bounds;
            obb = OrientedBoundingBox.Merge(ref obb, ref obb0);
            Bounds = Bounds.FromBoundingBox(obb);
        }

        public override bool Intersects(ref Ray ray, out Vector3F point)
        {
            return obb.Intersects(ref ray, out point);
        }

        public override bool IntersectsForCamera(Camera camera, ref Ray ray, out Vector3F point)
        {
            point = Vector3F.Zero;
            if (ColliderData.ContainsKey(camera))
            {
                return ColliderData[camera].Intersects(ref ray, out point);
            }
            return false;
        }
    }

}
