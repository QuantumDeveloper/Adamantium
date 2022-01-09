using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Tools
{
   public class MoveToolTemplate : BaseToolTemplate
   {
      private double axisLength;
      private int tesselation;

      public MoveToolTemplate(
         double axisLength,
         Vector3F baseScale,
         int tesselation = 20)
      {
         this.axisLength = axisLength;
         this.tesselation = tesselation;
         this.baseScale = baseScale;
      }

      public override Entity BuildEntity(Entity owner, string name)
      {
         var root = new Entity(owner, name);
         var xSize = new Vector3(axisLength, 0, 0);
         var ySize = new Vector3(0, axisLength, 0);
         var zSize = new Vector3(0, 0, axisLength);

         var quarterPart = axisLength * 0.25;

         var xAxisMesh = Shapes.Line.GenerateGeometry(GeometryType.Outlined, new Vector3(quarterPart, 0, 0), xSize, 0);
         var xAxisTransform = Matrix4x4.RotationZ(MathHelper.DegreesToRadians(-90)) * Matrix4x4.Translation(xSize);
         var coneMeshX = Shapes.Cone.GenerateGeometry(GeometryType.Solid, quarterPart, 0, axisLength * 0.1f, tesselation, xAxisTransform);

         var rightAxis = BuildSubEntity(root, "RightAxis", xAxisMesh, Colors.Red);
         BuildSubEntity(rightAxis, "RightAxisManipulator", coneMeshX, Colors.Red, BoundingVolume.OrientedBox);

         var yAxisMesh = Shapes.Line.GenerateGeometry(GeometryType.Outlined, new Vector3(0, quarterPart, 0), ySize, 0);
         var coneMeshY = coneMeshX.Clone(Matrix4x4.RotationZ(MathHelper.DegreesToRadians(90)));

         var upAxis = BuildSubEntity(root, "UpAxis", yAxisMesh, Colors.Green);
         BuildSubEntity(upAxis, "UpAxisManipulator", coneMeshY, Colors.Green, BoundingVolume.OrientedBox);

         var zAxisMesh = Shapes.Line.GenerateGeometry(GeometryType.Outlined, new Vector3(0, 0, quarterPart), zSize, 0);
         var coneMeshZ = coneMeshX.Clone(Matrix4x4.RotationY(MathHelper.DegreesToRadians(-90)));

         var forwardAxis = BuildSubEntity(root, "ForwardAxis", zAxisMesh, Colors.Blue);
         BuildSubEntity(forwardAxis, "ForwardAxisManipulator", coneMeshZ, Colors.Blue, BoundingVolume.OrientedBox);

         var xzPlane = Shapes.Plane.GenerateGeometry(
             GeometryType.Solid,
            quarterPart,
            quarterPart,
            1,
            Matrix4x4.Translation(axisLength - quarterPart, 0, axisLength - quarterPart));

         BuildSubEntity(root, "RightForwardManipulator", xzPlane, Colors.Orange, BoundingVolume.OrientedBox);

         var xyPlane = Shapes.Plane.GenerateGeometry(
             GeometryType.Solid,
            quarterPart,
            quarterPart,
            1,
            Matrix4x4.RotationX(MathHelper.DegreesToRadians(90)) * Matrix4x4.Translation(axisLength - quarterPart, axisLength - quarterPart, 0));

         BuildSubEntity(root, "RightUpManipulator", xyPlane, Colors.DarkOrchid, BoundingVolume.OrientedBox);

         var rot = QuaternionF.RotationYawPitchRoll(MathHelper.DegreesToRadians(90), MathHelper.DegreesToRadians(90), 0);
         var zyPlane = Shapes.Plane.GenerateGeometry(
             GeometryType.Solid,
            quarterPart,
            quarterPart,
            1,
            Matrix4x4.RotationQuaternion(rot) * Matrix4x4.Translation(0, axisLength - quarterPart, axisLength - quarterPart));

         BuildSubEntity(root, "UpForwardManipulator", zyPlane, Colors.CornflowerBlue, BoundingVolume.OrientedBox);

         var centralCube = Shapes.Cube.GenerateGeometry(GeometryType.Outlined, quarterPart, 1);
         BuildSubEntity(root, "CentralManipulator", centralCube, Colors.Turquoise, BoundingVolume.OrientedBox);

         return root;
      }
   }
}
