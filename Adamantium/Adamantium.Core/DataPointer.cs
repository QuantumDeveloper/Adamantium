using System;

namespace Adamantium.Core
{
    public struct DataPointer
    {
        public DataPointer(IntPtr pointer, int size)
        {
            Pointer = pointer;
            Size = size;
        }

        public IntPtr Pointer { get; }

        public int Size { get; }

        public bool IsEmpty => Pointer == IntPtr.Zero && Size == 0;
    }
}
