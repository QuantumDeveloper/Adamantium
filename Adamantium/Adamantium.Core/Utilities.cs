using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace Adamantium.Core
{
    public static class Utilities
    {
        public static unsafe void FreeMemory(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return;
            Marshal.FreeHGlobal(((IntPtr*)pointer)[-1]);
        }

        public static void ClearMemory(IntPtr dest, byte value, int sizeInBytesToClear)
        {
            unsafe
            {
                Span<byte> bytes = new Span<byte>(dest.ToPointer(), sizeInBytesToClear);
                bytes.Fill(value);
            }
        }

        public static bool IsEnum<T>(T type)
        {
            return type is Enum;
        }

        public static unsafe IntPtr AllocateMemory(int sizeInBytes, int align = 1)
        {
            int mask = align - 1;
            var memPtr = Marshal.AllocHGlobal(sizeInBytes + mask + IntPtr.Size);
            var ptr = (long)((byte*)memPtr + sizeof(void*) + mask) & ~mask;
            ((IntPtr*)ptr)[-1] = memPtr;
            return new IntPtr((void*)ptr);
        }

        public static byte[] ReadStream(Stream stream)
        {
            long readLength = 0;
            return ReadStream(stream, ref readLength);
        }

        public static byte[] ReadStream(Stream stream, ref long readLength)
        {
            if (stream == null || !stream.CanRead)
            {
                return new byte[0];
            }

            long size = readLength;

            if (size == 0)
            {
                readLength = stream.Length - stream.Position;
            }

            size = readLength;

            if (size == 0)
            {
                return new byte[0];
            }

            var buffer = new byte[size];

            stream.Read(buffer, 0, (int)size);

            return buffer;
        }

        /// <summary>
        /// Determines whether the specified memory pointer is aligned in memory.
        /// </summary>
        /// <param name="memoryPtr">The memory pointer.</param>
        /// <param name="align">The align.</param>
        /// <returns><c>true</c> if the specified memory pointer is aligned in memory; otherwise, <c>false</c>.</returns>
        public static bool IsMemoryAligned(IntPtr memoryPtr, int align = 16)
        {
            return ((memoryPtr.ToInt64() & (align - 1)) == 0);
        }

        public static int SizeOf<T>() where T : struct
        {
            return Marshal.SizeOf<T>();
        }

        public static unsafe void CopyMemory(IntPtr destination, IntPtr source, int sizeInBytesToCopy)
        {
            Buffer.MemoryCopy(source.ToPointer(), destination.ToPointer(), sizeInBytesToCopy, sizeInBytesToCopy);
        }

        public static void Write<T>(IntPtr destination, ref T value) where T : struct
        {
            var size = SizeOf<T>();
            IntPtr source = IntPtr.Zero;
            Marshal.StructureToPtr<T>(value, source, false);
            CopyMemory(destination, source, size);
        }

        public static IntPtr Write<T>(IntPtr destination, T[] data, int offset, int count) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var startPos = IntPtr.Add(destination, offset);
            var source = GCHandle.Alloc(data, GCHandleType.Pinned);
            CopyMemory(destination, source.AddrOfPinnedObject(), size * data.Length);
            source.Free();

            return IntPtr.Add(startPos, count * size);
        }

        public static T Read<T>(IntPtr source) where T : struct
        {
            return Marshal.PtrToStructure<T>(source);
        }

        /// <summary>
        /// Reads the specified array T[] data from a memory location.
        /// </summary>
        /// <typeparam name="T">Type of a data to read.</typeparam>
        /// <param name="source">Memory location to read from.</param>
        /// <param name="data">The data write to.</param>
        /// <param name="offset">The offset in the array to write to.</param>
        /// <param name="count">The number of T element to read from the memory location.</param>
        /// <returns>source pointer + sizeof(T) * count.</returns>
        public static unsafe IntPtr Read<T>(IntPtr source, T[] data, int offset, int count) where T : struct
        {
            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            var ptr = handle.AddrOfPinnedObject();
            int size = Marshal.SizeOf(typeof(T)) * count;
            Buffer.MemoryCopy((void*)source, (void*)(ptr+ (offset * Marshal.SizeOf<T>())), size, size);
            handle.Free();
            return new IntPtr(size + (byte*)source);
        }

        public static T GetCustomAttribute<T>(MemberInfo memberInfo, bool inherit = true) where T : Attribute
        {
            return memberInfo.GetCustomAttribute<T>(inherit);
        }

        public static IEnumerable<T> GetCustomAttributes<T>(MemberInfo memberInfo) where T: Attribute
        {
            return memberInfo.GetCustomAttributes<T>();
        }

        public static void Swap<T>(ref T elem1, ref T elem2)
        {
            var tmp = elem1;
            elem1 = elem2;
            elem2 = tmp;
        }

        public static ushort ToLittleEndian(byte left, byte right)
        {
            var res = BitConverter.ToUInt16(new byte[] { right, left });
            var result = (ushort)(right | left << 8);
            return result;
        }

        public static int SwapEndianness(int value)
        {
            var b1 = (value >> 0) & 0xff;
            var b2 = (value >> 8) & 0xff;
            var b3 = (value >> 16) & 0xff;
            var b4 = (value >> 24) & 0xff;

            return b1 << 24 | b2 << 16 | b3 << 8 | b4 << 0;
        }
    }
}
