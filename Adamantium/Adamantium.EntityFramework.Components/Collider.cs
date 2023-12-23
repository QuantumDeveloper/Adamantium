using System;
using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
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

        protected Mesh Geometry { get; set; }
        protected Buffer<MeshVertex> VertexBuffer { get; set; }
        protected Buffer<int> IndexBuffer { get; set; }
        protected Type LayoutType { get; set; }

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

        public virtual void Draw(GraphicsDevice renderContext, Camera camera)
        {
            if (!Initialized)
            {
                Initialize();
            }

            if (!DisplayCollider || !ContainsDataFor(camera))
            {
                return;
            }

            if (Geometry == null)
            {
                GetVisualRepresentation();
            }

            if (Geometry.IsModified)
            {
                CreateVertexData(renderContext);
                Geometry.AcceptChanges();
            }

            renderContext.SetVertexBuffer(VertexBuffer);
            renderContext.VertexType = LayoutType;
            renderContext.PrimitiveTopology = Geometry.MeshTopology;

            if (Geometry.Indices != null)
            {
                renderContext.SetIndexBuffer(IndexBuffer);
            }

            if (Geometry.Indices != null)
            {
                renderContext.DrawIndexed(VertexBuffer, IndexBuffer);
            }
            else
            {
                renderContext.Draw(VertexBuffer.ElementCount, 1);
            }
        }

        protected void CreateVertexData(GraphicsDevice renderContext)
        {
            var meshVertices = Geometry.ToMeshVertices();

            VertexBuffer?.Dispose();
            VertexBuffer = ToDispose(Buffer.Vertex.New(renderContext, meshVertices));

            IndexBuffer?.Dispose();
            IndexBuffer = ToDispose(Buffer.Index.New(renderContext, Geometry.Indices));

            LayoutType = typeof(MeshVertex);
        }
    }
}
