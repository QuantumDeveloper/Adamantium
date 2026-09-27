using System;

namespace Adamantium.Graphics.Core
{
    [Flags]
    public enum SurfaceTransform
    {
        Identity = 1,

        Rotate90 = 2,

        Rotate180 = 4,

        Rotate270 = 8,

        HorizontalMirror = 16,

        HorizontalMirrorRotate90 = 32,

        HorizontalMirrorRotate180 = 64,

        HorizontalMirrorRotate270 = 128,

        Inherit = 256,
    }
}
