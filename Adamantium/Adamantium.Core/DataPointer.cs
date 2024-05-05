using System;

namespace Adamantium.Core
{
    public struct DataPointer
    {
        public DataPointer(IntPtr pointer, ulong size, uint count)
        {
            Pointer = pointer;
            Size = size;
            Count = count;
        }

        public DataPointer(IntPtr pointer, ulong size) : this(pointer, size, 0)
        {
        }

        public IntPtr Pointer;

        public ulong Size;

        public uint Count;

        public bool IsEmpty => Pointer == IntPtr.Zero && Size == 0;
    }
}
