using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Vector2 = Adamantium.Mathematics.Vector2;
using Vector3 = Adamantium.Mathematics.Vector3;

namespace Adamantium.Core
{
    public static class Utilities
    {
        /// <summary>
        /// Release memory obtained from <see cref="AllocateMemory(int, int)"/> or <see cref="AllocateMemory(nuint)"/>.
        /// <para>
        /// Must match the allocator EXACTLY, which is why both allocate through <c>NativeMemory</c> and this frees
        /// through it - the MODERN allocator, which is where the engine is heading. It used to free with
        /// <c>Marshal.FreeHGlobal</c> while one of the two overloads allocated with
        /// <c>NativeMemory.Alloc</c> - handing a block from one heap to another allocator corrupts the process heap,
        /// and the crash surfaces later at an unrelated allocation. That is what killed the app on a DDS cube map
        /// (STATUS_HEAP_CORRUPTION, 0xC0000374), with the pixel buffers taking the <c>nuint</c> overload.
        /// </para>
        /// </summary>
        public static unsafe void FreeMemory(IntPtr pointer)
        {
            if (pointer == IntPtr.Zero) return;
#if NET6_0_OR_GREATER
            NativeMemory.Free(pointer.ToPointer());
#else
            // netstandard2.0 has no NativeMemory, so that target allocates through Marshal below and frees the same way.
            Marshal.FreeHGlobal(pointer);
#endif
        }

        public static unsafe void ClearMemory(ref IntPtr dest, byte value, int sizeInBytesToClear)
        {
            #if NETCORE
            Span<byte> bytes = new Span<byte>(dest.ToPointer(), sizeInBytesToClear);
            bytes.Fill(value);
            #else
            var bytes = new byte[sizeInBytesToClear];
            Marshal.Copy(dest, bytes, 0, sizeInBytesToClear);
            #endif
        }

        public static bool IsEnum<T>(T type)
        {
            return type is Enum;
        }

#if NET6_0_OR_GREATER
        /// <summary>
        /// Allocate native memory. Goes through the SAME heap as every other allocation here, because
        /// <see cref="FreeMemory"/> is the only release path and it cannot know which allocator a pointer came from.
        /// <para>
        /// This used to call <c>NativeMemory.Alloc</c> while <see cref="FreeMemory"/> released with
        /// <c>Marshal.FreeHGlobal</c>. Handing a block from one allocator to another corrupts the process heap, and the
        /// damage only surfaces later at some unrelated allocation - which is how a dropped DDS cube map killed the app
        /// with STATUS_HEAP_CORRUPTION (0xC0000374). Callers passing a <c>uint</c> size bound to this overload, and the
        /// image pixel buffers do exactly that.
        /// </para>
        /// </summary>
        public static unsafe IntPtr AllocateMemory(nuint sizeInBytes)
        {
            return new IntPtr(NativeMemory.Alloc(sizeInBytes));
        }
#endif
        /// <summary>
        /// Allocate native memory. <paramref name="align"/> is a MINIMUM the platform allocator already satisfies (it
        /// hands back memory aligned for any primitive - 16 bytes on both x64 targets), so it needs no arithmetic here.
        /// <para>
        /// It used to over-allocate and return a pointer offset INTO the block to force alignment - which threw the
        /// base address away, so the matching free released an address the allocator never handed out and corrupted the
        /// heap. Nothing in the engine asks for more than 16.
        /// </para>
        /// </summary>
        public static unsafe IntPtr AllocateMemory(int sizeInBytes, int align = 1)
        {
            if (align > 16)
            {
                throw new ArgumentOutOfRangeException(nameof(align), align,
                    "Alignment beyond what the platform allocator guarantees needs AlignedAlloc/AlignedFree, which this pair does not use.");
            }
#if NET6_0_OR_GREATER
            return AllocateMemory((nuint)sizeInBytes);
#else
            return Marshal.AllocHGlobal(sizeInBytes);
#endif
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
                return Array.Empty<byte>();
            }

            long size = readLength;

            if (size == 0)
            {
                readLength = stream.Length - stream.Position;
            }

            size = readLength;

            if (size == 0)
            {
                return Array.Empty<byte>();
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

        public static int SizeOf<T>(T[] array) where T : struct
        {
            return array == null ? 0 : array.Length * SizeOf<T>();
        }

        public static unsafe void CopyMemory(IntPtr destination, IntPtr source, long sizeInBytesToCopy)
        {
            Buffer.MemoryCopy(source.ToPointer(), destination.ToPointer(), sizeInBytesToCopy, sizeInBytesToCopy);
        }

        public static void Write<T>(IntPtr destination, ref T value) where T : struct
        {
            var size = SizeOf<T>();
            IntPtr source = AllocateMemory(SizeOf<T>());
            Marshal.StructureToPtr(value, source, false);
            CopyMemory(destination, source, size);
            FreeMemory(source);   // must match AllocateMemory above - not FreeHGlobal, which is a different heap
        }

        /// <summary>
        /// Copy <paramref name="count"/> elements starting at <paramref name="data"/>[<paramref name="offset"/>] to
        /// <paramref name="destination"/>, and return the address just past what was written.
        /// <para>
        /// It used to ignore BOTH arguments and copy the whole array - so a caller asking for the first N elements of a
        /// longer array wrote straight past the end of the destination allocation. That is a heap overrun: the process
        /// dies later, at some unrelated allocation, with STATUS_HEAP_CORRUPTION. A dropped DDS cube map hit it through
        /// the pixel-buffer flip, which allocates exactly one row-stride and passes a full-image array.
        /// </para>
        /// </summary>
        public static IntPtr Write<T>(IntPtr destination, T[] data, int offset, int count) where T : struct
        {
            var size = SizeOf<T>();
            var source = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                CopyMemory(destination, IntPtr.Add(source.AddrOfPinnedObject(), offset * size), (long)count * size);
            }
            finally
            {
                source.Free();
            }

            return IntPtr.Add(destination, count * size);
        }

        public static T Read<T>(IntPtr source) where T : struct
        {
            return Marshal.PtrToStructure<T>(source);
        }

        public static void Read<T>(IntPtr source, ref T data) where T : struct
        {
            data = Marshal.PtrToStructure<T>(source);
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
            (elem1, elem2) = (elem2, elem1);
        }

        public static ushort ToLittleEndian(byte left, byte right)
        {
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

        public static ushort SwapEndianness(ushort value)
        {
            var b1 = (value >> 0) & 0xff;
            var b2 = (value >> 8) & 0xff;

            return (ushort)(b1 << 8 | b2 << 0);
        }
        
        #if NET9_0_OR_GREATER
        public static IEnumerable<byte> GetBytesWithReversedEndian<T>(T value) where T : unmanaged, IBinaryInteger<T>
        {
            int size = System.Runtime.CompilerServices.Unsafe.SizeOf<T>();
            byte[] bytes = new byte[size];

            if (BitConverter.IsLittleEndian)
            {
                value.WriteBigEndian(bytes);
            }
            else
            {
                value.WriteLittleEndian(bytes);
            }

            return bytes;
        }
        #endif
        
        public static IEnumerable<byte> GetBytesWithReversedEndian(uint value, int size)
        {
            byte[] bytes = new byte[size];
        
            for (int i = 0; i < size; i++)
            {
                bytes[size - 1 - i] = (byte)(value >> (i * 8));
            }

            return bytes;
        }

        public static IEnumerable<byte> GetBytesWithReversedEndian(int value) 
            => GetBytesWithReversedEndian((uint)value, sizeof(int));

        public static IEnumerable<byte> GetBytesWithReversedEndian(uint value) 
            => GetBytesWithReversedEndian(value, sizeof(uint));

        public static IEnumerable<byte> GetBytesWithReversedEndian(ushort value) 
            => GetBytesWithReversedEndian(value, sizeof(ushort));

        public static void Dispose<T>(ref T arg) where T: IDisposable
        {
            var disposable = arg as IDisposable;
            disposable?.Dispose();
        }

        public static byte GetBitsCount(this int i)
        {
            byte count = 0;

            while (i >> 1 > 0)
            {
                i >>= 1;
                count++;
            }

            return count;
        }

        public static byte ReverseByte(this byte val)
        {
            byte result = 0x00;

            for (byte mask = 0x80; Convert.ToInt32(mask) > 0; mask >>= 1)
            {
                // shift right current result
                result = (byte)(result >> 1);

                // tempbyte = 1 if there is a 1 in the current position
                var tempbyte = (byte)(val & mask);
                if (tempbyte != 0x00)
                {
                    // Insert a 1 in the left
                    result = (byte)(result | 0x80);
                }
            }

            return result;
        }

        /// <summary>
        /// String helper join method to display an array of object as a single string.
        /// </summary>
        /// <param name="separator">The separator.</param>
        /// <param name="array">The array.</param>
        /// <returns>A string with array elements separated by the separator.</returns>
        public static string Join<T>(string separator, T[] array)
        {
            var text = new StringBuilder();
            if (array != null)
            {
                for (int i = 0; i < array.Length; i++)
                {
                    if (i > 0) text.Append(separator);
                    text.Append(array[i]);
                }
            }
            return text.ToString();
        }

        /// <summary>
        /// String helper join method to display an enumerable of object as a single string.
        /// </summary>
        /// <param name="separator">The separator.</param>
        /// <param name="elements">The enumerable.</param>
        /// <returns>A string with array elements separated by the separator.</returns>
        public static string Join(string separator, IEnumerable elements)
        {
            var elementList = new List<string>();
            foreach (var element in elements)
                elementList.Add(element.ToString());

            var text = new StringBuilder();
            for (int i = 0; i < elementList.Count; i++)
            {
                var element = elementList[i];
                if (i > 0) text.Append(separator);
                text.Append(element);
            }
            return text.ToString();
        }

        /// <summary>
        /// Compares two collection, element by elements.
        /// </summary>
        /// <param name="left">A "from" enumerator.</param>
        /// <param name="right">A "to" enumerator.</param>
        /// <returns><c>true</c> if lists are identical, <c>false</c> otherwise.</returns>
        public static bool Compare(IEnumerable left, IEnumerable right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            return Compare(left.GetEnumerator(), right.GetEnumerator());
        }

        /// <summary>
        /// Compares two collection, element by elements.
        /// </summary>
        /// <param name="leftIt">A "from" enumerator.</param>
        /// <param name="rightIt">A "to" enumerator.</param>
        /// <returns><c>true</c> if lists are identical; otherwise, <c>false</c>.</returns>
        public static bool Compare(IEnumerator leftIt, IEnumerator rightIt)
        {
            if (ReferenceEquals(leftIt, rightIt))
                return true;
            if (ReferenceEquals(leftIt, null) || ReferenceEquals(rightIt, null))
                return false;

            bool hasLeftNext;
            bool hasRightNext;
            while (true)
            {

                hasLeftNext = leftIt.MoveNext();
                hasRightNext = rightIt.MoveNext();
                if (!hasLeftNext || !hasRightNext)
                    break;

                if (!Equals(leftIt.Current, rightIt.Current))
                    return false;
            }

            // If there is any left element
            if (hasLeftNext != hasRightNext)
                return false;

            return true;
        }

        /// <summary>
        /// Compares two collection, element by elements.
        /// </summary>
        /// <param name="left">The collection to compare from.</param>
        /// <param name="right">The collection to compare to.</param>
        /// <returns><c>true</c> if lists are identical (but not necessarily of the same time); otherwise , <c>false</c>.</returns>
        public static bool Compare(ICollection left, ICollection right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
                return false;

            if (left.Count != right.Count)
                return false;

            int count = 0;
            var leftIt = left.GetEnumerator();
            var rightIt = right.GetEnumerator();
            while (leftIt.MoveNext() && rightIt.MoveNext())
            {
                if (!Equals(leftIt.Current, rightIt.Current))
                    return false;
                count++;
            }

            if (count != left.Count)
                return false;

            return true;
        }

        public static bool IsTypeInheritFrom(Type type, Type baseType)
        {
            return baseType is not null && baseType.IsAssignableFrom(type);
        }

        /// <summary>
        /// Compute a FNV1-modified improved hash version.
        /// </summary>
        /// <param name="data">Data to compute the hash from.</param>
        /// <returns>A hash value.</returns>
        public static int ComputeHashFNV1Modified(byte[] data)
        {
            const uint prime = 16777619;
            uint hash = 2166136261;
            foreach (byte b in data)
                hash = (hash ^ b) * prime;

            hash += hash << 13;
            hash ^= hash >> 7;
            hash += hash << 3;
            hash ^= hash >> 17;
            hash += hash << 5;
            return unchecked((int)hash);
        }
        
        public static Vector2[] ToVector2(IEnumerable<Vector3> array)
        {
            var collection = new List<Vector2>();
            foreach (var vector in array)
            {
                collection.Add((Vector2)vector);
            }

            return collection.ToArray();
        }

        public static Vector3[] ToVector3(IEnumerable<Vector2> array)
        {
            var collection = new List<Vector3>();
            foreach (var vector in array)
            {
                collection.Add((Vector3)vector);
            }

            return collection.ToArray();
        }
        
        public static ulong AlignSize(ulong size, ulong alignment)
        {
            return (size + alignment - 1) & ~(alignment - 1);
        }
    }
}
