using System;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;

namespace Adamantium.Engine.Graphics
{
    public class Shape : DisposableObject
    {
        public static readonly int StripSeparatorValue = -1;

        //public Buffer<SkinnedMeshVertex> VertexBuffer { get; private set; }

        //public Buffer<Int32> IndexBuffer { get; private set; }

        //public static readonly VertexInputLayout InputLayout = VertexInputLayout.New<SkinnedMeshVertex>(0);

        public static readonly Boolean Is32BitIndices = true;

        public PrimitiveType PrimitiveType { get; private set; }

        protected readonly GraphicsDevice GraphicsDevice;

        public Shape(GraphicsDevice device, Mesh mesh)
        {
            GraphicsDevice = device;

            int length = mesh.Positions.Length;

            var normals = mesh.Semantic & VertexSemantic.Normal;
            var texcoords0 = mesh.Semantic & VertexSemantic.UV0;
            var texcoords1 = mesh.Semantic & VertexSemantic.UV1;
            var texcoords2 = mesh.Semantic & VertexSemantic.UV2;
            var texcoords3 = mesh.Semantic & VertexSemantic.UV3;
            var tanBitan = mesh.Semantic & VertexSemantic.TangentBiNormal;

            var jointindices = mesh.Semantic & VertexSemantic.JointIndices;
            var jointWeights = mesh.Semantic & VertexSemantic.JointWeights;

            SkinnedMeshVertex[] vertices = new SkinnedMeshVertex[length];

            for (int i = 0; i < length; i++)
            {
                var vertex = new SkinnedMeshVertex() { Position = mesh.Positions[i] };
                if (normals > 0)
                {
                    vertex.Normal = mesh.Normals[i];
                }
                if (texcoords0 > 0)
                {
                    vertex.UV0 = mesh.UV0[i];
                }
                if (texcoords1 > 0)
                {
                    vertex.UV1 = mesh.UV1[i];
                }
                if (texcoords2 > 0)
                {
                    vertex.UV2 = mesh.UV2[i];
                }
                if (texcoords3 > 0)
                {
                    vertex.UV3 = mesh.UV3[i];
                }

                if (tanBitan > 0)
                {
                    vertex.Tangent = mesh.Tangents[i];
                    vertex.BiTangent = mesh.BiTangents[i];
                }

                if (jointindices > 0 && jointWeights > 0)
                {
                    vertex.JointIndices = mesh.BoneIndices[i];
                    vertex.JointWeights = mesh.BoneWeights[i];
                }
                vertices[i] = vertex;
            }

            //VertexBuffer = ToDispose(Buffer.Vertex.New(device.MainDevice, vertices, ResourceUsage.Dynamic));
            //IndexBuffer = ToDispose(Buffer.Index.New(device.MainDevice, mesh.Indices, ResourceUsage.Dynamic));

            PrimitiveType = mesh.MeshTopology;
        }

        public virtual void Draw(/*EffectPass pass = null*/)
        {
            //pass?.Apply();

            //GraphicsDevice.SetVertexBuffer(VertexBuffer);
            //GraphicsDevice.VertexInputLayout = InputLayout;
            //GraphicsDevice.SetIndexBuffer(IndexBuffer);
            //GraphicsDevice.DrawIndexed(PrimitiveType, IndexBuffer.ElementCount);
        }
    }
}
