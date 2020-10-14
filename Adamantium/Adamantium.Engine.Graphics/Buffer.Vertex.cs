using Adamantium.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public partial class Buffer
    {
        /// <summary>
        /// Vertex buffer helper methods.
        /// </summary>
        public static class Vertex
        {
            private static BufferUsageFlags BufferUsage = BufferUsageFlags.TransferDst | BufferUsageFlags.VertexBuffer;

            /// <summary>
            /// Creates a new Vertex buffer with <see cref="MemoryPropertyFlags.Default"/> memoryFlags by default.
            /// </summary>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="size">The size in bytes.</param>
            /// <param name="memoryFlags">The memoryFlags.</param>
            /// <returns>A Vertex buffer</returns>
            public static Buffer New(GraphicsDevice device, int size, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
            {
                return Buffer.New(device, size, BufferUsage, memoryFlags);
            }

            /// <summary>
            /// Creates a new Vertex buffer with <see cref="MemoryPropertyFlags.Default"/> memoryFlags by default.
            /// </summary>
            /// <typeparam name="T">Type of the Vertex buffer to get the sizeof from</typeparam>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="vertexBufferCount">Number of vertex in this buffer with the sizeof(T).</param>
            /// <param name="memoryFlags">The memoryFlags.</param>
            /// <returns>A Vertex buffer</returns>
            public static Buffer<T> New<T>(GraphicsDevice device, uint vertexBufferCount, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
            {
                return Buffer.New<T>(device, vertexBufferCount, BufferUsage, memoryFlags);
            }

            /// <summary>
            /// Creates a new Vertex buffer with <see cref="MemoryPropertyFlags.Immutable"/> memoryFlags by default.
            /// </summary>
            /// <typeparam name="T">Type of the Vertex buffer to get the sizeof from</typeparam>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="value">The value to initialize the Vertex buffer.</param>
            /// <param name="memoryFlags">The memoryFlags of this resource.</param>
            /// <returns>A Vertex buffer</returns>
            public static Buffer<T> New<T>(GraphicsDevice device, T[] value, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
            {
                return Buffer.New(device, value, BufferUsage, memoryFlags);
            }

            /// <summary>
            /// Creates a new Vertex buffer with <see cref="MemoryPropertyFlags.Immutable"/> memoryFlags by default.
            /// </summary>
            /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
            /// <param name="value">The value to initialize the Vertex buffer.</param>
            /// <param name="memoryFlags">The memoryFlags of this resource.</param>
            /// <returns>A Vertex buffer</returns>
            public static Buffer New(GraphicsDevice device, DataPointer value, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
            {
                return Buffer.New(device, value, BufferUsage, memoryFlags);
            }
        }
    }
}
