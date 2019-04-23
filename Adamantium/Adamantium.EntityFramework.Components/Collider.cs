using Adamantium.Engine.Core.Models;
using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework.ComponentsBasics;
using Adamantium.Mathematics;
using Buffer = Adamantium.Engine.Graphics.Buffer;

namespace Adamantium.EntityFramework.Components
{
    public abstract class Collider : ActivatableComponent
    {
        protected Mesh Geometry { get; set; }

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

        protected Buffer<MeshVertex> VertexBuffer;
        protected Buffer<int> IndexBuffer;
        protected VertexInputLayout InputLayout;

        private bool displayCollider;

        public override void Initialize()
        {
            var meshData = Owner?.GetComponent<MeshData>();
            if (meshData != null)
            {
                CalculateFromMesh(meshData.Mesh);
                base.Initialize();
            }
        }

        public abstract void ClearData();


        public abstract bool ContainsDataFor(Camera camera);


        protected Collider()
        {
        }

        public abstract Mesh GetVisualRepresentation();

        public abstract void UpdateForCamera(Camera camera);

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

        public virtual void Draw(D3DGraphicsDevice renderContext, Camera camera)
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
            renderContext.VertexInputLayout = InputLayout;

            if (Geometry.Indices != null)
            {
                renderContext.SetIndexBuffer(IndexBuffer);
            }

            if (Geometry.Indices != null)
            {
                renderContext.DrawIndexed(Geometry.MeshTopology, Geometry.Indices.Length);
            }
            else
            {
                renderContext.Draw(Geometry.MeshTopology, Geometry.Positions.Length);
            }
        }

        protected void CreateVertexData(D3DGraphicsDevice renderContext)
        {
            int length = Geometry.Positions.Length;

            var normals = Geometry.Semantic.HasFlag(VertexSemantic.Normal);
            var texcoords0 = Geometry.Semantic.HasFlag(VertexSemantic.UV0);
            var texcoords1 = Geometry.Semantic.HasFlag(VertexSemantic.UV1);
            var texcoords2 = Geometry.Semantic.HasFlag(VertexSemantic.UV2);
            var texcoords3 = Geometry.Semantic.HasFlag(VertexSemantic.UV3);
            var tanBitan = Geometry.Semantic.HasFlag(VertexSemantic.TangentBiNormal);

            var vertices = new MeshVertex[length];

            for (int i = 0; i < length; ++i)
            {
                var vertex = new MeshVertex() { Position = Geometry.Positions[i] };
                if (normals)
                {
                    vertex.Normal = Geometry.Normals[i];
                }
                if (texcoords0)
                {
                    vertex.UV0 = Geometry.UV0[i];
                }
                if (texcoords1)
                {
                    vertex.UV1 = Geometry.UV1[i];
                }
                if (texcoords2)
                {
                    vertex.UV2 = Geometry.UV2[i];
                }
                if (texcoords3)
                {
                    vertex.UV3 = Geometry.UV3[i];
                }

                if (tanBitan)
                {
                    vertex.Tangent = Geometry.Tangents[i];
                    vertex.BiTangent = Geometry.BiTangents[i];
                }

                vertices[i] = vertex;
            }

            VertexBuffer?.Dispose();
            VertexBuffer = ToDispose(Buffer.Vertex.New(renderContext, vertices, ResourceUsage.Dynamic));

            IndexBuffer?.Dispose();
            IndexBuffer = ToDispose(Buffer.Index.New(renderContext, Geometry.Indices, ResourceUsage.Dynamic));

            InputLayout = VertexInputLayout.FromBuffer(0, VertexBuffer);
        }
    }
}
