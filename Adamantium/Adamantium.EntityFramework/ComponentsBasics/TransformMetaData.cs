using Adamantium.Mathematics;

namespace Adamantium.EntityFramework.ComponentsBasics
{
    public class TransformMetaData
    {
        private bool enabled;

        public TransformMetaData()
        {
            Enabled = true;
            WorldMatrix = Matrix4x4F.Identity;
            Scale = Vector3F.One;
        }

        public Camera Camera { get; set; }

        public Vector3F RelativePosition { get; set; }

        public Matrix4x4F WorldMatrix { get; set; }

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
