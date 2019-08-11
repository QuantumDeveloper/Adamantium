﻿using System;
using System.Collections.Generic;
using Adamantium.Core;
using Adamantium.Engine.Core;
using Adamantium.Engine.Core.Models;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Graphics
{
    public partial class Shapes
    {
        public class Sphere
        {
            private class UVSphere
            {
                public static Mesh GenerateGeometry(
                    float diameter = 1.0f,
                    int tessellation = 40,
                    bool toRightHanded = false)
                {

                    int verticalSegments = tessellation;
                    int horizontalSegments = tessellation * 2;

                    var vertices = new Vector3F[(verticalSegments + 1) * (horizontalSegments + 1)];
                    var uvs = new Vector2F[(verticalSegments + 1) * (horizontalSegments + 1)];
                    var indices = new int[verticalSegments * (horizontalSegments + 1) * 6];

                    float radius = diameter / 2;

                    int vertexCount = 0;
                    // Create rings of vertices at progressively higher latitudes.
                    for (int i = 0; i <= verticalSegments; i++)
                    {
                        float v = 1.0f - (float) i / verticalSegments;

                        var latitude = (float) ((i * Math.PI / verticalSegments) - Math.PI / 2.0);
                        var dy = (float) Math.Sin(latitude);
                        var dxz = (float) Math.Cos(latitude);

                        // Create a single ring of vertices at this latitude.
                        for (int j = 0; j <= horizontalSegments; j++)
                        {
                            float u = (float) j / horizontalSegments;

                            var longitude = (float) (j * 2.0 * Math.PI / horizontalSegments);
                            var dx = (float) Math.Sin(longitude);
                            var dz = (float) Math.Cos(longitude);

                            dx *= dxz;
                            dz *= dxz;

                            var normal = new Vector3F(dx, dy, dz);
                            var textureCoordinate = new Vector2F(1.0f - u, v);

                            vertices[vertexCount] = normal * radius;
                            uvs[vertexCount++] = textureCoordinate;
                        }
                    }

                    // Fill the index buffer with triangles joining each pair of latitude rings.
                    int stride = horizontalSegments + 1;

                    int indexCount = 0;
                    for (int i = 0; i < verticalSegments; i++)
                    {
                        for (int j = 0; j <= horizontalSegments; j++)
                        {
                            int nextI = i + 1;
                            int nextJ = (j + 1) % stride;

                            indices[indexCount++] = (i * stride + j);
                            indices[indexCount++] = (i * stride + nextJ);
                            indices[indexCount++] = (nextI * stride + j);

                            indices[indexCount++] = (i * stride + nextJ);
                            indices[indexCount++] = (nextI * stride + nextJ);
                            indices[indexCount++] = (nextI * stride + j);
                        }
                    }

                    var mesh = new Mesh();
                    mesh.MeshTopology = PrimitiveType.TriangleList;
                    mesh.SetPositions(vertices);
                    mesh.SetUVs(0, uvs);
                    mesh.SetIndices(indices);
                    if (toRightHanded)
                    {
                        mesh.ReverseWinding();
                    }
                    mesh.CalculateNormals();

                    return mesh;
                }
            }

            private class GeoSphere
            {
                public static readonly PrimitiveType PrimitiveType = PrimitiveType.TriangleList;

                //--------------------------------------------------------------------------------------
                // Geodesic sphere
                //--------------------------------------------------------------------------------------

                private static readonly Vector3F[] OctahedronVertices =
                {
                    // when looking down the negative z-axis (into the screen)
                    new Vector3F(0, 1, 0), // 0 top
                    new Vector3F(0, 0, -1), // 1 front
                    new Vector3F(1, 0, 0), // 2 right
                    new Vector3F(0, 0, 1), // 3 back
                    new Vector3F(-1, 0, 0), // 4 left
                    new Vector3F(0, -1, 0), // 5 bottom
                };

                private static readonly int[] OctahedronIndices = new int[]
                {
                    0, 2, 1,// top front-right face
                    0, 3, 2, // top back-right face
                    0, 4, 3, // top back-left face
                    0, 1, 4, // top front-left face
                    5, 4, 1, // bottom front-left face
                    5, 3, 4, // bottom back-left face
                    5, 2, 3, // bottom back-right face
                    5, 1, 2, // bottom front-right face
                };

                private List<Vector3F> vertexPositions;

                private List<int> indexList;

                private List<Vector3F> vertices;
                private List<Vector2F> uvs;

                private unsafe int* indices;

                // Key: an edge
                // Value: the index of the vertex which lies midway between the two vertices pointed to by the key value
                // This map is used to avoid duplicating vertices when subdividing triangles along edges.
                Dictionary<UndirectedEdge, int> subdividedEdges;

                /// <summary>
                /// Creates a Geodesic sphere
                /// </summary>
                /// <param name="diameter">The diameter.</param>
                /// <param name="tessellation">The tessellation.</param>
                /// <param name="toRightHanded">if set to <c>true</c> vertices and indices will be transformed to left handed. Default is true.</param>
                /// <returns>A <see cref="Mesh"/> containing all necesseray info in raw format.</returns>
                public static Mesh GenerateGeometry(
                    float diameter = 1.0f,
                    int tessellation = 3,
                    bool toRightHanded = false)
                {
                    var sphere = new GeoSphere();
                    return sphere.GenerateSphere(diameter, tessellation, toRightHanded);
                }

                /// <summary>
                /// Creates a Geodesic sphere.
                /// </summary>
                /// <param name="diameter">The diameter.</param>
                /// <param name="tessellation">The tessellation.</param>
                /// <param name="toRightHanded">if set to <c>true</c> vertices and indices will be transformed to left handed. Default is true.</param>
                /// <returns>A Geodesic sphere.</returns>
                private unsafe Mesh GenerateSphere(
                    float diameter = 1.0f,
                    int tessellation = 3,
                    bool toRightHanded = false)
                {
                    subdividedEdges = new Dictionary<UndirectedEdge, int>();

                    float radius = diameter / 2.0f;

                    // Start with an octahedron; copy the data into the vertex/index collection.
                    vertexPositions = new List<Vector3F>(OctahedronVertices);
                    indexList = new List<int>(OctahedronIndices);
                    uvs = new List<Vector2F>();

                    // We know these values by looking at the above index list for the octahedron. Despite the subdivisions that are
                    // about to go on, these values aren't ever going to change because the vertices don't move around in the array.
                    // We'll need these values later on to fix the singularities that show up at the poles.
                    const int northPoleIndex = 0;
                    const int southPoleIndex = 5;

                    for (int iSubdivision = 0; iSubdivision < tessellation; ++iSubdivision)
                    {
                        // The new index collection after subdivision.
                        var newIndices = new List<int>();
                        subdividedEdges.Clear();

                        int triangleCount = indexList.Count / 3;
                        for (int iTriangle = 0; iTriangle < triangleCount; ++iTriangle)
                        {
                            // For each edge on this triangle, create a new vertex in the middle of that edge.
                            // The winding order of the triangles we output are the same as the winding order of the inputs.

                            // Indices of the vertices making up this triangle
                            int iv0 = indexList[iTriangle * 3 + 0];
                            int iv1 = indexList[iTriangle * 3 + 2];
                            int iv2 = indexList[iTriangle * 3 + 1];

                            // Get the new vertices
                            Vector3F v01; // vertex on the midpoint of v0 and v1
                            Vector3F v12; // ditto v1 and v2
                            Vector3F v20; // ditto v2 and v0
                            int iv01; // index of v01
                            int iv12; // index of v12
                            int iv20; // index of v20

                            // Add/get new vertices and their indices
                            DivideEdge(iv0, iv1, out v01, out iv01);
                            DivideEdge(iv1, iv2, out v12, out iv12);
                            DivideEdge(iv0, iv2, out v20, out iv20);

                            // Add the new indices. We have four new triangles from our original one:
                            //        v0
                            //        o
                            //       /a\
                            //  v20 o---o v01
                            //     /b\c/d\
                            // v2 o---o---o v1
                            //       v12

                            // a
                            newIndices.Add(iv0);
                            newIndices.Add(iv20);
                            newIndices.Add(iv01);

                            // b
                            newIndices.Add(iv20);
                            newIndices.Add(iv2);
                            newIndices.Add(iv12);

                            // d
                            newIndices.Add(iv20);
                            newIndices.Add(iv12);
                            newIndices.Add(iv01);

                            // d
                            newIndices.Add(iv01);
                            newIndices.Add(iv12);
                            newIndices.Add(iv1);
                        }

                        indexList.Clear();
                        indexList.AddRange(newIndices);
                    }

                    // Now that we've completed subdivision, fill in the final vertex collection
                    vertices = new List<Vector3F>(vertexPositions.Count);
                    for (int i = 0; i < vertexPositions.Count; i++)
                    {
                        var vertexValue = vertexPositions[i];

                        var normal = vertexValue;
                        normal.Normalize();

                        var pos = normal * radius;

                        // calculate texture coordinates for this vertex
                        float longitude = (float) Math.Atan2(normal.X, -normal.Z);
                        float latitude = (float) Math.Acos(normal.Y);

                        float u = (float) (longitude / (Math.PI * 2.0) + 0.5);
                        float v = (float) (latitude / Math.PI);

                        var texcoord = new Vector2F(u, v);
                        vertices.Add(pos);
                        uvs.Add(texcoord);
                    }

                    const float XMVectorSplatEpsilon = 1.192092896e-7f;

                    // There are a couple of fixes to do. One is a texture coordinate wraparound fixup. At some point, there will be
                    // a set of triangles somewhere in the mesh with texture coordinates such that the wraparound across 0.0/1.0
                    // occurs across that triangle. Eg. when the left hand side of the triangle has a U coordinate of 0.98 and the
                    // right hand side has a U coordinate of 0.0. The intent is that such a triangle should render with a U of 0.98 to
                    // 1.0, not 0.98 to 0.0. If we don't do this fixup, there will be a visible seam across one side of the sphere.
                    // 
                    // Luckily this is relatively easy to fix. There is a straight edge which runs down the prime meridian of the
                    // completed sphere. If you imagine the vertices along that edge, they circumscribe a semicircular arc starting at
                    // y=1 and ending at y=-1, and sweeping across the range of z=0 to z=1. x stays zero. It's along this edge that we
                    // need to duplicate our vertices - and provide the correct texture coordinates.
                    int preCount = vertices.Count;
                    var indicesArray = indexList.ToArray();
                    fixed (void* pIndices = indicesArray)
                    {
                        indices = (int*) pIndices;

                        for (int i = 0; i < preCount; ++i)
                        {
                            // This vertex is on the prime meridian if position.x and texcoord.u are both zero (allowing for small epsilon).
                            bool isOnPrimeMeridian = MathHelper.WithinEpsilon(vertices[i].X, 0, XMVectorSplatEpsilon)
                                                     && MathHelper.WithinEpsilon(uvs[i].X, 0, XMVectorSplatEpsilon);

                            if (isOnPrimeMeridian)
                            {
                                int newIndex = vertices.Count;

                                // copy this vertex, correct the texture coordinate, and add the vertex
                                var uv = uvs[i];
                                uv.X = 1.0f;
                                uvs[i] = uv;

                                // Now find all the triangles which contain this vertex and update them if necessary
                                for (int j = 0; j < indexList.Count; j += 3)
                                {
                                    var triIndex0 = &indices[j + 0];
                                    var triIndex1 = &indices[j + 1];
                                    var triIndex2 = &indices[j + 2];

                                    if (*triIndex0 == i)
                                    {
                                        // nothing; just keep going
                                    }
                                    else if (*triIndex1 == i)
                                    {
                                        Utilities.Swap(ref *triIndex0, ref *triIndex1);
                                        Utilities.Swap(ref *triIndex1, ref *triIndex2);
                                    }
                                    else if (*triIndex2 == i)
                                    {
                                        Utilities.Swap(ref *triIndex0, ref *triIndex2);
                                        Utilities.Swap(ref *triIndex1, ref *triIndex2);
                                    }
                                    else
                                    {
                                        // this triangle doesn't use the vertex we're interested in
                                        continue;
                                    }

                                    // check the other two vertices to see if we might need to fix this triangle
                                    if (Math.Abs(uvs[*triIndex0].X - uvs[*triIndex1].X) > 0.5f ||
                                        Math.Abs(uvs[*triIndex0].X - uvs[*triIndex2].X) > 0.5f)
                                    {
                                        // yep; replace the specified index to point to the new, corrected vertex
                                        indices[j + 0] = newIndex;
                                    }
                                }
                            }
                        }

                        //FixPole(northPoleIndex);
                        //FixPole(southPoleIndex);

                        // Clear indices as it will not be accessible outside the fixed statement
                        indices = (int*) 0;
                    }

                    var mesh = new Mesh();
                    mesh.MeshTopology = PrimitiveType;
                    mesh.SetPositions(vertices);
                    mesh.SetUVs(0, uvs);
                    mesh.SetIndices(indicesArray);
                    if (toRightHanded)
                    {
                        mesh.ReverseWinding();
                    }
                    mesh.CalculateNormals();

                    return mesh;
                }

                private unsafe void FixPole(
                    int poleIndex)
                {
                    var poleUV = uvs[poleIndex];
                    bool overwrittenPoleVertex = false; // overwriting the original pole vertex saves us one vertex

                    for (ushort i = 0; i < indexList.Count; i += 3)
                    {
                        // These pointers point to the three indices which make up this triangle. pPoleIndex is the pointer to the
                        // entry in the index array which represents the pole index, and the other two pointers point to the other
                        // two indices making up this triangle.
                        int* pPoleIndex;
                        int* pOtherIndex0;
                        int* pOtherIndex1;
                        if (indices[i + 0] == poleIndex)
                        {
                            pPoleIndex = &indices[i + 0];
                            pOtherIndex0 = &indices[i + 1];
                            pOtherIndex1 = &indices[i + 2];
                        }
                        else if (indices[i + 1] == poleIndex)
                        {
                            pPoleIndex = &indices[i + 1];
                            pOtherIndex0 = &indices[i + 2];
                            pOtherIndex1 = &indices[i + 0];
                        }
                        else if (indices[i + 2] == poleIndex)
                        {
                            pPoleIndex = &indices[i + 2];
                            pOtherIndex0 = &indices[i + 0];
                            pOtherIndex1 = &indices[i + 1];
                        }
                        else
                        {
                            continue;
                        }

                        // Calculate the texcoords for the new pole vertex, add it to the vertices and update the index
                        var newPoleUV = poleUV;
                        newPoleUV.X = (vertices[*pOtherIndex0].X + vertices[*pOtherIndex1].X) * 0.5f;
                        newPoleUV.Y = poleUV.Y;

                        if (!overwrittenPoleVertex)
                        {
                            uvs[poleIndex] = newPoleUV;
                            overwrittenPoleVertex = true;
                        }
                        else
                        {
                            *pPoleIndex = vertices.Count;
                            uvs.Add(newPoleUV);
                        }
                    }
                }

                // Function that, when given the index of two vertices, creates a new vertex at the midpoint of those vertices.
                private void DivideEdge(
                    int i0,
                    int i1,
                    out Vector3F outVertex,
                    out int outIndex)
                {
                    var edge = new UndirectedEdge(i0, i1);

                    // Check to see if we've already generated this vertex
                    if (subdividedEdges.TryGetValue(edge, out outIndex))
                    {
                        // We've already generated this vertex before
                        outVertex = vertexPositions[outIndex]; // and the vertex itself
                    }
                    else
                    {
                        // Haven't generated this vertex before: so add it now
                        outVertex = (vertexPositions[i0] + vertexPositions[i1]) * 0.5f;
                        outIndex = vertexPositions.Count;
                        vertexPositions.Add(outVertex);

                        // Now add it to the map.
                        subdividedEdges[edge] = outIndex;
                    }
                }

                // An undirected edge between two vertices, represented by a pair of indexes into a vertex array.
                // Because this edge is undirected, (a,b) is the same as (b,a).
                private struct UndirectedEdge : IEquatable<UndirectedEdge>
                {
                    public UndirectedEdge(
                        int item1,
                        int item2)
                    {
                        // Makes an undirected edge. Rather than overloading comparison operators to give us the (a,b)==(b,a) property,
                        // we'll just ensure that the larger of the two goes first. This'll simplify things greatly.
                        Item1 = Math.Max(item1, item2);
                        Item2 = Math.Min(item1, item2);
                    }

                    public readonly int Item1;

                    public readonly int Item2;

                    public bool Equals(
                        UndirectedEdge other)
                    {
                        return Item1 == other.Item1 && Item2 == other.Item2;
                    }

                    public override bool Equals(
                        object obj)
                    {
                        if (ReferenceEquals(null, obj))
                            return false;
                        return obj is UndirectedEdge && Equals((UndirectedEdge) obj);
                    }

                    public override int GetHashCode()
                    {
                        unchecked
                        {
                            return (Item1.GetHashCode() * 397) ^ Item2.GetHashCode();
                        }
                    }

                    public static bool operator ==(
                        UndirectedEdge left,
                        UndirectedEdge right)
                    {
                        return left.Equals(right);
                    }

                    public static bool operator !=(
                        UndirectedEdge left,
                        UndirectedEdge right)
                    {
                        return !left.Equals(right);
                    }
                }
            }

            private class CubeSphere
            {
                public static Mesh GenerateGeometry(
                    float diameter,
                    int tessellation = 3,
                    bool toRightHanded = false)
                {
                    if (diameter <= 0)
                    {
                        throw new ArgumentOutOfRangeException(nameof(diameter), "diameter parameter must be more than 0");
                    }

                    var mesh = Cube.GenerateGeometry(GeometryType.Solid, diameter, diameter, diameter, tessellation, null, toRightHanded);

                    var vertices = mesh.Positions;
                    var radius = diameter / 2;

                    for (int i = 0; i < vertices.Length; i++)
                    {
                        double x = 0;
                        double y = 0;
                        double z = 0;

                        double sqrX = 0;
                        double sqrY = 0;
                        double sqrZ = 0;

                        double d = 0;
                        double theta = 0;
                        double phi = 0;

                        x = vertices[i].X;
                        y = vertices[i].Y;
                        z = vertices[i].Z;

                        sqrX = Math.Pow(x, 2);
                        sqrY = Math.Pow(y, 2);
                        sqrZ = Math.Pow(z, 2);

                        d = Math.Sqrt(sqrX + sqrY + sqrZ);

                        theta = Math.Acos(z / d);
                        phi = Math.Atan2(y, x);

                        Vector3F position = new Vector3F();
                        position.X = (float) (radius * Math.Sin(theta) * Math.Cos(phi));
                        position.Y = (float) (radius * Math.Sin(theta) * Math.Sin(phi));
                        position.Z = (float) (radius * Math.Cos(theta));

                        vertices[i] = position;
                    }

                    mesh.SetPositions(vertices);
                    mesh.CalculateNormals();

                    return mesh;
                }
            }

            public static Mesh GenerateOutlinedGeometry(
                float diameter = 1.0f,
                int tessellation = 40,
                bool toRightHanded = false)
            {
                PrimitiveType primitiveType = PrimitiveType.LineStrip;

                List<Vector3F> vertices = new List<Vector3F>();
                List<int> indices = new List<int>();
                Vector3F center = Vector3F.Zero;
                int lastIndex = 0;
                float bottomRadius = diameter / 2;

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = center.X + bottomRadius * (float) Math.Cos(angle);
                    float y = center.Y + bottomRadius * (float) Math.Sin(angle);
                    vertices.Add(new Vector3F(x, 0, y));
                    indices.Add(lastIndex++);
                }
                indices.Add(Shape.StripSeparatorValue);

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = center.X + bottomRadius * (float) Math.Cos(angle);
                    float y = center.Y + bottomRadius * (float) Math.Sin(angle);
                    vertices.Add(new Vector3F(x, y, 0));
                    indices.Add(lastIndex++);
                }

                indices.Add(Shape.StripSeparatorValue);

                for (int i = 0; i <= tessellation; ++i)
                {
                    float angle = (float) Math.PI * 2 / tessellation * i;

                    float x = center.X + bottomRadius * (float) Math.Cos(angle);
                    float y = center.Y + bottomRadius * (float) Math.Sin(angle);
                    vertices.Add(new Vector3F(0, x, y));
                    indices.Add(lastIndex++);
                }

                Mesh mesh = new Mesh();
                mesh.MeshTopology = primitiveType;
                mesh.SetPositions(vertices);
                mesh.SetIndices(indices);

                return mesh;
            }

            /// <summary>
            /// Creates a sphere primitive.
            /// </summary>
            /// <param name="device">The device.</param>
            /// <param name="diameter">The diameter.</param>
            /// <param name="tessellation">The tessellation.</param>
            /// <param name="toRightHanded">if set to <c>true</c> vertices and indices will be transformed to left handed. Default is true.</param>
            /// <returns>A sphere primitive.</returns>
            /// <exception cref="System.ArgumentOutOfRangeException">tessellation;Must be >= 3</exception>
            public static Shape New(
                GraphicsDevice device,
                GeometryType geometryType,
                SphereType sphereType,
                float diameter = 1.0f,
                int tessellation = 8,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                var geometry = GenerateGeometry(geometryType, sphereType, diameter, tessellation, transform, toRightHanded);
                // Create the primitive object.
                return new Shape(device, geometry);
            }

            public static Mesh GenerateGeometry(
                GeometryType geometryType,
                SphereType sphereType,
                float diameter = 1.0f,
                int tessellation = 40,
                Matrix4x4F? transform = null,
                bool toRightHanded = false)
            {
                if (tessellation < 3 && sphereType != SphereType.GeoSphere)
                {
                    tessellation = 3;
                }

                if (sphereType == SphereType.GeoSphere && geometryType == GeometryType.Solid && tessellation > 7)
                {
                    tessellation = 7;
                }

                if (sphereType == SphereType.GeoSphere && tessellation < 0)
                {
                    tessellation = 0;
                }

                Mesh mesh;
                if (geometryType == GeometryType.Outlined)
                {
                    mesh = GenerateOutlinedGeometry(diameter, tessellation, toRightHanded);
                }
                else
                {
                    switch (sphereType)
                    {
                        case SphereType.GeoSphere:
                            mesh = GeoSphere.GenerateGeometry(diameter, tessellation, toRightHanded);
                            break;
                        case SphereType.CubeSphere:
                            mesh = CubeSphere.GenerateGeometry(diameter, tessellation, toRightHanded);
                            break;
                        default:
                            mesh = UVSphere.GenerateGeometry(diameter, tessellation, toRightHanded);
                            break;
                    }
                }

                if (transform.HasValue)
                {
                    mesh.ApplyTransform(transform.Value);
                }

                return mesh;
            }
        }
    }
}
