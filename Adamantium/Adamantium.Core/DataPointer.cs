using System;

namespace Adamantium.Core
{
    public struct DataPointer
    {
        public DataPointer(IntPtr pointer, int size, uint count)
        {
            Pointer = pointer;
            Size = size;
            Count = count;
        }

        public DataPointer(IntPtr pointer, int size) : this(pointer, size, 0)
        {
        }

        public IntPtr Pointer;

        public int Size;

        public uint Count;

        public bool IsEmpty => Pointer == IntPtr.Zero && Size == 0;
    }
}
