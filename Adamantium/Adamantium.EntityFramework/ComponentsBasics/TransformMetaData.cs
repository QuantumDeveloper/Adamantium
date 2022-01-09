using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public class TransformMetaData
    {
        private bool enabled;

        public TransformMetaData()
        {
            Enabled = true;
            Scale = Vector3F.One;
            WorldMatrixF = Matrix4x4F.Identity;
            WorldMatrix = Matrix4x4.Identity;
        }

        public CameraBase Camera { get; set; }

        public Vector3F RelativePosition { get; set; }

        public Matrix4x4F WorldMatrixF { get; set; }
        
        public Matrix4x4 WorldMatrix { get; set; }

        public QuaternionF Rotation { get; set; }

        public Vector3F Scale { get; set; }

        public Vector3F Pivot { get; set; }

        public bool Enabled { get; set; }

        public bool IsSelected { get; set; }

        public static TransformMetaData New()
        {
            return new TransformMetaData();
        }
    }
}
