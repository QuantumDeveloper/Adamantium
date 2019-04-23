using Adamantium.Engine.Graphics;
using Adamantium.EntityFramework;
using Adamantium.Mathematics;

namespace Adamantium.Engine.Templates.Tools
{
   public class OrientationToolTemplate: BaseToolTemplate
   {
      private float size;
      private QuaternionF rotation;
      private int tesselation;

      public OrientationToolTemplate(float size, Vector3F baseScale, QuaternionF rotation)
      {
         this.size = size;
         this.baseScale = baseScale;
         this.rotation = rotation;
         this.tesselation = 4;
      }

      public override Entity BuildEntity(Entity owner, string name)
      {
         var root = new Entity(owner, name);
         var partSize = size / 3;
         Vector3F translation = new Vector3F(partSize, 0, 0);

         var centralMesh = Shapes.Cube.GenerateGeometry(GeometryType.Solid, size/4, 1);
         var xAxisRightTransform = Matrix4x4F.RotationY(MathHelper.DegreesToRadians(45)) * 
            Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(-90)) * Matrix4x4F.Translation(translation);

         var pyramidMeshRight = Shapes.Cone.GenerateGeometry(GeometryType.Solid, partSize, 0, partSize, tesselation, xAxisRightTransform);
         var pyramidMeshLeft = pyramidMeshRight.Clone(Matrix4x4F.RotationY(MathHelper.DegreesToRadians(180)));

         BuildSubEntity(root, "CentralManipulator", centralMesh, Colors.LightGray, BoundingVolume.OrientedBox);

         BuildSubEntity(root, "RightAxisManipulator", pyramidMeshRight, Colors.Red, BoundingVolume.OrientedBox);
         BuildSubEntity(root, "LeftAxisManipulator", pyramidMeshLeft, Colors.LightGray, BoundingVolume.OrientedBox);

         var pyramidMeshUp = pyramidMeshRight.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(90)) * Matrix4x4F.RotationQuaternion(rotation));
         var pyramidMeshDown = pyramidMeshRight.Clone(Matrix4x4F.RotationZ(MathHelper.DegreesToRadians(-90)) * Matrix4x4F.RotationQuaternion(rotation));

         BuildSubEntity(root, "UpAxisManipulator", pyramidMeshUp, Colors.Green, BoundingVolume.OrientedBox);
         BuildSubEntity(root, "DownAxisManipulator", pyramidMeshDown, Colors.LightGray, BoundingVolume.OrientedBox);

         var pyramidMeshForward = pyramidMeshRight.Clone(Matrix4x4F.RotationY(MathHelper.DegreesToRadians(-90)) * Matrix4x4F.RotationQuaternion(rotation));
         var pyramidMeshBackward = pyramidMeshRight.Clone(Matrix4x4F.RotationY(MathHelper.DegreesToRadians(90)) * Matrix4x4F.RotationQuaternion(rotation));

         BuildSubEntity(root, "ForwardAxisManipulator", pyramidMeshForward, Colors.Blue, BoundingVolume.OrientedBox);
         BuildSubEntity(root, "BackwardAxisManipulator", pyramidMeshBackward, Colors.LightGray, BoundingVolume.OrientedBox);

         return root;
      }
   }
}
