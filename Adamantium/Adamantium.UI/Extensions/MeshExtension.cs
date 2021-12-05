using Adamantium.Engine.Core.Models;

namespace Adamantium.UI.Extensions
{
    public static class MeshExtension
    {
        public static UIVertex[] ToUIVertices(this Mesh mesh)
        {
            if (!mesh.Semantic.HasFlag(VertexSemantic.Position))
            {
                return null;
            }

            int length = mesh.Points.Length;

            var normals = mesh.Semantic & VertexSemantic.Normal;
            var colors = mesh.Semantic.HasFlag(VertexSemantic.Color);
            var texcoords0 = mesh.Semantic & VertexSemantic.UV0;

            var vertices = new UIVertex[length];

            for (int i = 0; i < length; i++)
            {
                var vertex = new UIVertex() { Position = mesh.Points[i] };
                if (normals > 0)
                {
                    vertex.Normal = mesh.Normals[i];
                }
                if (texcoords0 > 0)
                {
                    vertex.UV0 = mesh.UV0[i];
                }

                if (colors)
                {
                    vertex.Color = mesh.Colors[i].ToVector4();
                }

                vertices[i] = vertex;
            }

            return vertices;
        }
    }
}