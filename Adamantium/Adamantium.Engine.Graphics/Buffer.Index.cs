using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public partial class Buffer
    {
        /// <summary>
        /// Index buffer helper methods.
        /// </summary>
        public static class Index
        {
            /// <summary>
            /// Creates a new index buffer with <see cref="MemoryPropertyFlags.Default"/> memoryFlags by default.
            /// </summary>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="size">The size in bytes.</param>
            /// <param name="memoryFlags">The memoryFlags.</param>
            /// <returns>A index buffer</returns>
            public static Buffer New(GraphicsDevice device, int size, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
            {
                return Buffer.New(device, size, BufferUsageFlags.IndexBuffer, memoryFlags);
            }

            /// <summary>
            /// Creates a new index buffer with <see cref="MemoryPropertyFlags.Default"/> memoryFlags by default.
            /// </summary>
            /// <typeparam name="T">Type of the index buffer to get the sizeof from</typeparam>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="indexCount">Number of indices.</param>
            /// <param name="memoryFlags">The memoryFlags.</param>
            /// <returns>A index buffer</returns>
            public static Buffer<T> New<T>(GraphicsDevice device, int indexCount, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
            {
                return Buffer.New<T>(device, indexCount, BufferUsageFlags.IndexBuffer, memoryFlags);
            }

            /// <summary>
            /// Creates a new index buffer with <see cref="MemoryPropertyFlags.Immutable"/> memoryFlags by default.
            /// </summary>
            /// <typeparam name="T">Type of the index buffer to get the sizeof from</typeparam>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="value">The value to initialize the index buffer.</param>
            /// <param name="memoryFlags">The memoryFlags of this resource.</param>
            /// <returns>A index buffer</returns>
            public static Buffer<T> New<T>(GraphicsDevice device, ref T value, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.Protected) where T : struct
            {
                return Buffer.New(device, ref value, BufferUsageFlags.IndexBuffer, memoryFlags);
            }

            /// <summary>
            /// Creates a new index buffer with <see cref="MemoryPropertyFlags.Immutable"/> memoryFlags by default.
            /// </summary>
            /// <typeparam name="T">Type of the index buffer to get the sizeof from</typeparam>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="value">The value to initialize the index buffer.</param>
            /// <param name="memoryFlags">The memoryFlags of this resource.</param>
            /// <returns>A index buffer</returns>
            public static Buffer<T> New<T>(GraphicsDevice device, T[] value, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.Protected) where T : struct
            {
                return Buffer.New(device, value, BufferUsageFlags.IndexBuffer, memoryFlags);
            }

            /// <summary>
            /// Creates a new index buffer with <see cref="MemoryPropertyFlags.Immutable"/> memoryFlags by default.
            /// </summary>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="value">The value to initialize the index buffer.</param>
            /// <param name="is32BitIndex">Set to true if the buffer is using a 32 bit index or false for 16 bit index.</param>
            /// <param name="memoryFlags">The memoryFlags of this resource.</param>
            /// <returns>A index buffer</returns>
            public static Buffer New(GraphicsDevice device, byte[] value, bool is32BitIndex, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.Protected)
            {
                return Buffer.New(device, value, is32BitIndex ? 4 : 2, BufferUsageFlags.IndexBuffer, Format.UNDEFINED, memoryFlags);
            }

            /// <summary>
            /// Creates a new index buffer with <see cref="MemoryPropertyFlags.Immutable"/> memoryFlags by default.
            /// </summary>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="value">The value to initialize the index buffer.</param>
            /// <param name="memoryFlags">The memoryFlags of this resource.</param>
            /// <returns>A index buffer</returns>
            public static Buffer New(GraphicsDevice device, DataPointer value, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.Protected)
            {
                return Buffer.New(device, value, 0, BufferUsageFlags.IndexBuffer, memoryFlags);
            }
        }
    }
}
