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

        public IntPtr Pointer { get; }

        public ulong Size { get; }

        public uint Count { get; }

        public bool IsEmpty => Pointer == IntPtr.Zero && Size == 0;
    }
}
