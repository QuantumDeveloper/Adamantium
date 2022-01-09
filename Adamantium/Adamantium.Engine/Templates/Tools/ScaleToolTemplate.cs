using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Tools
{
    public class ScaleToolTemplate : BaseToolTemplate
    {
        private float axisLength;

        public ScaleToolTemplate(
            float axisLength,
            Vector3F baseScale)
        {
            this.axisLength = axisLength;
            this.baseScale = baseScale;
        }

        public override Entity BuildEntity(
            Entity owner,
            string name)
        {
            var root = new Entity(owner, name);

            var cubeSize = axisLength * 0.15f;

            var xSize = new Vector3(axisLength, 0, 0);
            var ySize = new Vector3(0, axisLength, 0);
            var zSize = new Vector3(0, 0, axisLength);

            var xAxisMesh = Shapes.Line.GenerateGeometry(GeometryType.Outlined, Vector3.Zero, xSize, 0);
            var yAxisMesh = Shapes.Line.GenerateGeometry(GeometryType.Outlined, Vector3.Zero, ySize, 0);
            var zAxisMesh = Shapes.Line.GenerateGeometry(GeometryType.Outlined, Vector3.Zero, zSize, 0);

            var xAxisRoot = BuildSubEntity(root, "RightAxis", xAxisMesh, Colors.Red);
            var yAxisRoot = BuildSubEntity(root, "UpAxis", yAxisMesh, Colors.Green);
            var zAxisRoot = BuildSubEntity(root, "ForwardAxis", zAxisMesh, Colors.Blue);

            var transform = Matrix4x4.Translation(new Vector3(axisLength, 0, 0));
            var xManipulator = Shapes.Cube.GenerateGeometry(GeometryType.Solid, cubeSize, 1, transform);

            transform = Matrix4x4.Translation(new Vector3(0, axisLength, 0));
            var yManipulator = Shapes.Cube.GenerateGeometry(GeometryType.Solid, cubeSize, 1, transform);

            transform = Matrix4x4.Translation(new Vector3(0, 0, axisLength));
            var zManipulator = Shapes.Cube.GenerateGeometry(GeometryType.Solid, cubeSize, 1, transform);

            var centralManipulator = Shapes.Cube.GenerateGeometry(GeometryType.Solid, cubeSize, 1);

            BuildSubEntity(xAxisRoot, "RightAxisManipulator", xManipulator, Colors.Red, BoundingVolume.OrientedBox);
            BuildSubEntity(yAxisRoot, "UpAxisManipulator", yManipulator, Colors.Green, BoundingVolume.OrientedBox);
            BuildSubEntity(zAxisRoot, "ForwardAxisManipulator", zManipulator, Colors.Blue, BoundingVolume.OrientedBox);
            BuildSubEntity(root, "CentralManipulator", centralManipulator, Colors.Turquoise, BoundingVolume.OrientedBox);

            var xzCube = Shapes.Cube.GenerateGeometry(
                GeometryType.Solid,
                cubeSize,
                1,
                Matrix4x4.Translation(axisLength, 0, axisLength));

            BuildSubEntity(root, "RightForwardManipulator", xzCube, Colors.Orange, BoundingVolume.OrientedBox);

            var xyCube = Shapes.Cube.GenerateGeometry(
                GeometryType.Solid,
                cubeSize,
                1,
                Matrix4x4.RotationX(MathHelper.DegreesToRadians(90)) * Matrix4x4.Translation(axisLength, axisLength, 0));

            BuildSubEntity(root, "RightUpManipulator", xyCube, Colors.DarkCyan, BoundingVolume.OrientedBox);

            var rot = QuaternionF.RotationYawPitchRoll(MathHelper.DegreesToRadians(90), MathHelper.DegreesToRadians(90), 0);
            var zyCube = Shapes.Cube.GenerateGeometry(
                GeometryType.Solid,
                cubeSize,
                1,
                Matrix4x4.RotationQuaternion(rot) * Matrix4x4.Translation(0, axisLength, axisLength));

            BuildSubEntity(root, "UpForwardManipulator", zyCube, Colors.DarkViolet, BoundingVolume.OrientedBox);

            root.Transform.BaseScale = baseScale;

            return root;
        }
    }
}
