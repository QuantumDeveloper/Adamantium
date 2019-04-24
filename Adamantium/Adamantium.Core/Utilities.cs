﻿using System;
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
            Marshal.FreeHGlobal(pointer);
            //Marshal.FreeHGlobal(((IntPtr*)pointer)[-1]);
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

        public static unsafe IntPtr AllocateMemory(int sizeInBytes, int align = 16)
        {
            int mask = align - 1;
            return Marshal.AllocHGlobal(sizeInBytes);
            //var memPtr = Marshal.AllocHGlobal(sizeInBytes + mask + IntPtr.Size);
            //var ptr = (long)((byte*)memPtr + sizeof(void*) + mask) & ~mask;
            //((IntPtr*)ptr)[-1] = memPtr;
            //return new IntPtr((void*)ptr);
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

        public static IntPtr Read<T>(IntPtr source, T[] data, int offset, int count) where T : struct
        {
            var size = Marshal.SizeOf(typeof(T));
            var startPos = IntPtr.Add(source, offset);
            IntPtr currentPtr = startPos;
            for (int i = 0; i < count; i++)
            {
                currentPtr = IntPtr.Add(startPos, i * size);
                data[i] = Marshal.PtrToStructure<T>(currentPtr);
            }

            return currentPtr;
        }

        public static T GetCustomAttribute<T>(MemberInfo memberInfo, bool inherit = true) where T : Attribute
        {
            return memberInfo.GetCustomAttribute<T>(inherit);
        }

        public static IEnumerable<T> GetCustomAttributes<T>(MemberInfo memberInfo) where T: Attribute
        {
            return memberInfo.GetCustomAttributes<T>();
        }
    }
}
