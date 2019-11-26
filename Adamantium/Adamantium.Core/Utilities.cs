using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

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

        public static int SizeOf<T>(T[] array) where T : struct
        {
            return array == null ? 0 : array.Length * SizeOf<T>();
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
            var size = SizeOf<T>();
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

        public static ushort SwapEndianness(ushort value)
        {
            var b1 = (value >> 0) & 0xff;
            var b2 = (value >> 8) & 0xff;

            return (ushort)(b1 << 8 | b2 << 0);
        }

        public static IEnumerable<byte> GetBytesWithReversedEndian(int value)
        {
            return BitConverter.GetBytes(value).Reverse();
        }

        public static IEnumerable<byte> GetBytesWithReversedEndian(uint value)
        {
            return BitConverter.GetBytes(value).Reverse();
        }

        public static IEnumerable<byte> GetBytesWithReversedEndian(ushort value)
        {
            return BitConverter.GetBytes(value).Reverse();
        }

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

        public static bool IsTypeInheritFrom(Type type, string baseType)
        {
            throw new NotImplementedException();
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
    }
}
