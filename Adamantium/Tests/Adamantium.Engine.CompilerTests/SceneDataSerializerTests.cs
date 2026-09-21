using Adamantium.Graphics.Core;
using Adamantium.Graphics.Core.Models;
using Adamantium.Mathematics;
using NUnit.Framework;

namespace Adamantium.Engine.CompilerTests
{
    [TestFixture]
    public class SceneDataSerializerTests
    {
        [Test]
        public void RoundTrip_PreservesHierarchyMeshAndSkeleton()
        {
            var scene = new SceneData { Name = "TestScene" };

            // model tree: root -> child (carrying a mesh)
            var child = scene.CreateMesh(scene.Models, "node1", "Body");
            child.Position = new Vector3F(1, 2, 3);
            child.Scale = new Vector3F(2, 2, 2);

            var mesh = new Mesh(PrimitiveType.TriangleList) { MaterialID = "mat1" };
            mesh.SetPoints([new Vector3(0, 0, 0), new Vector3(1, 0, 0), new Vector3(0, 1, 0)]);
            mesh.SetIndices([0, 1, 2]);
            mesh.SetNormals([new Vector3F(0, 0, 1), new Vector3F(0, 0, 1), new Vector3F(0, 0, 1)]);
            mesh.SetUVs(0, [new Vector2F(0, 0), new Vector2F(1, 0), new Vector2F(0, 1)]);
            mesh.SetColors([Colors.White, Colors.White, Colors.White]);
            mesh.SetTangentsAndBiTangents(
                [new Vector4F(1, 0, 0, 1), new Vector4F(1, 0, 0, 1), new Vector4F(1, 0, 0, 1)],
                [new Vector3F(0, 1, 0), new Vector3F(0, 1, 0), new Vector3F(0, 1, 0)]);
            child.Meshes.Add(mesh);

            scene.Materials["mat1"] = new SceneData.Material { ID = "mat1", DiffuseColor = new Vector4F(1, 0, 0, 1) };

            // skeleton: root joint -> child joint (exercises Joint.ParentJoint rebuild)
            var rootJoint = new SceneData.Joint { JointName = "root" };
            var childJoint = new SceneData.Joint { JointName = "spine" };
            rootJoint.Children.Add(childJoint);
            scene.Skeletons["skel1"] = [rootJoint];

            var bytes = SceneDataSerializer.Serialize(scene);
            var loaded = SceneDataSerializer.Deserialize(bytes);

            Assert.That(loaded, Is.Not.Null);
            Assert.That(loaded.Name, Is.EqualTo("TestScene"));

            // hierarchy + Parent back-reference rebuilt
            Assert.That(loaded.Models.Dependencies.Count, Is.EqualTo(1));
            var loadedChild = loaded.Models.Dependencies[0];
            Assert.That(loadedChild.ID, Is.EqualTo("node1"));
            Assert.That(loadedChild.Name, Is.EqualTo("Body"));
            Assert.That(loadedChild.Parent, Is.SameAs(loaded.Models));
            Assert.That(loadedChild.Position, Is.EqualTo(new Vector3F(1, 2, 3)));

            // mesh round-trip through MeshFormatter / MeshGeometry
            Assert.That(loadedChild.Meshes.Count, Is.EqualTo(1));
            var loadedMesh = loadedChild.Meshes[0];
            Assert.That(loadedMesh.MaterialID, Is.EqualTo("mat1"));
            Assert.That(loadedMesh.MeshTopology, Is.EqualTo(PrimitiveType.TriangleList));
            Assert.That(loadedMesh.Points.Length, Is.EqualTo(3));
            Assert.That(loadedMesh.Points[1], Is.EqualTo(new Vector3(1, 0, 0)));
            Assert.That(loadedMesh.Indices, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(loadedMesh.Normals[0], Is.EqualTo(new Vector3F(0, 0, 1)));
            Assert.That(loadedMesh.UV0[2], Is.EqualTo(new Vector2F(0, 1)));
            Assert.That(loadedMesh.Tangents[0], Is.EqualTo(new Vector4F(1, 0, 0, 1)));
            Assert.That(loadedMesh.BiTangents[0], Is.EqualTo(new Vector3F(0, 1, 0)));
            Assert.That(loadedMesh.Semantic.HasFlag(VertexSemantic.Normal), Is.True);
            Assert.That(loadedMesh.Semantic.HasFlag(VertexSemantic.TangentBiNormal), Is.True);

            // material round-trip (Dictionary<,> subclass)
            Assert.That(loaded.Materials.ContainsKey("mat1"), Is.True);
            Assert.That(loaded.Materials["mat1"].DiffuseColor, Is.EqualTo(new Vector4F(1, 0, 0, 1)));

            // skeleton + Joint.ParentJoint back-reference rebuilt
            Assert.That(loaded.Skeletons.ContainsKey("skel1"), Is.True);
            var loadedRootJoint = loaded.Skeletons["skel1"][0];
            Assert.That(loadedRootJoint.JointName, Is.EqualTo("root"));
            Assert.That(loadedRootJoint.Children.Count, Is.EqualTo(1));
            Assert.That(loadedRootJoint.Children[0].ParentJoint, Is.SameAs(loadedRootJoint));
        }
    }
}
