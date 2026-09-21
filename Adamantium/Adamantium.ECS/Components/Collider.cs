using Adamantium.ECS.Components;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.ECS.Components
{
    public abstract class Collider : ActivatableComponent
    {
        public Vector3F Size { get; set; }

        public Vector3F LocalCenter => Bounds.Center;

        public Vector3F Scale { get; set; }

        public QuaternionF Rotation { get; set; }

        public Vector3F Position { get; set; }

        public Bounds Bounds { get; set; }

        public Vector3F Center { get; set; }

        public bool DisplayCollider
        {
            get => displayCollider;
            set => SetProperty(ref displayCollider, value);
        }

        // The CPU mesh used to visualise the collider bounds (debug draw). Its GPU buffers are built + owned by the
        // render-layer geometry cache, keyed by this mesh - the collider no longer holds any GPU resource itself.
        public Mesh Geometry { get; protected set; }

        private bool displayCollider;

        public override void Initialize()
        {
            var meshData = Owner?.GetComponent<MeshData>();
            if (meshData == null) return;
            CalculateFromMesh(meshData.Mesh);
            base.Initialize();
        }

        public abstract void ClearData();


        public abstract bool ContainsDataFor(CameraBase camera);

        protected Collider()
        {
        }

        public abstract Mesh GetVisualRepresentation();

        public abstract void UpdateForCamera(CameraBase camera);

        public abstract ContainmentType IsInsideCameraFrustum(Camera camera);

        public abstract void Transform(ref Vector3F scale, ref QuaternionF rotation, ref Vector3F translation);

        public abstract void Transform(ref float uniformScale, ref QuaternionF rotation, ref Vector3F translation);

        public virtual void CalculateFromMesh(Mesh mesh)
        {
            Bounds = mesh.Bounds;
        }

        public abstract void Merge(Collider collider);

        public abstract bool Intersects(ref Ray ray, out Vector3F point);

        public abstract bool IntersectsForCamera(Camera camera, ref Ray ray, out Vector3F point);
    }
}
