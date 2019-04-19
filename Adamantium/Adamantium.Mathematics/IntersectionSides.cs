using System;

namespace Adamantium.Mathematics
{
    [Flags]
    internal enum IntersectionSides
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 4,
        Down = 8,
        All = Left|Right|Up|Down
    }
}
