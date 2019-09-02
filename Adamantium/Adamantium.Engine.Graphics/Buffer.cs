using Adamantium.Core;
using Adamantium.Imaging;
using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VulkanBuffer = AdamantiumVulkan.Core.Buffer;

namespace Adamantium.Engine.Graphics
{
    public partial class Buffer : DisposableObject
    {
        private VulkanBuffer VulkanBuffer;
        private DeviceMemory BufferMemory;
        private GraphicsDevice GraphicsDevice;

        public BufferUsageFlags Usage { get; private set; }

        public SharingMode SharingMode { get; private set; }

        public MemoryPropertyFlags MemoryFlags { get; private set; }

        public uint ElementSize { get; private set; }

        public uint ElementCount { get; private set; }

        public ulong TotalSize => ElementSize * ElementCount;

        /// <summary>
        /// Initializes a new instance of the <see cref="Buffer" /> class.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="description">The description.</param>
        /// <param name="bufferFlags">Type of the buffer.</param>
        /// <param name="viewFormat">The view format.</param>
        /// <param name="dataPointer">The data pointer.</param>
        protected Buffer(GraphicsDevice device, BufferUsageFlags bufferFlags, DataPointer dataPointer, MemoryPropertyFlags memoryFlags, SharingMode sharingMode)
        {
            GraphicsDevice = device;
            Usage = bufferFlags;
            MemoryFlags = memoryFlags;
            SharingMode = sharingMode;
            ElementSize = (uint)(dataPointer.Size / dataPointer.Count);
            ElementCount = dataPointer.Count;

            Initialize(dataPointer);
        }

        protected Buffer(GraphicsDevice device, BufferCreateInfo description, uint count, MemoryPropertyFlags memoryFlags)
        {
            GraphicsDevice = device;
            Usage = (BufferUsageFlags)description.Usage;
            MemoryFlags = memoryFlags;
            SharingMode = description.SharingMode;
            ElementSize = (uint)(description.Size / count);
            ElementCount = count;
        }

        protected Buffer(GraphicsDevice device, BufferUsageFlags bufferFlags, ulong size, uint count, MemoryPropertyFlags memoryflags, SharingMode sharingMode)
        {
            GraphicsDevice = device;
            Usage = bufferFlags;
            MemoryFlags = memoryflags;
            SharingMode = sharingMode;
            ElementSize = (uint)size / count;
            ElementCount = count;

            CreateBuffer(size, BufferUsageFlags.TransferDst | Usage, MemoryFlags, out VulkanBuffer, out BufferMemory);
        }

        private void Initialize(DataPointer dataPointer)
        {
            VulkanBuffer stagingBuffer;
            DeviceMemory stagingBufferMemory;
            CreateBuffer(TotalSize, BufferUsageFlags.TransferSrc, MemoryPropertyFlags.HostCached | MemoryPropertyFlags.HostCoherent, out stagingBuffer, out stagingBufferMemory);

            var data = GraphicsDevice.MapMemory(stagingBufferMemory, 0, TotalSize, 0);
            unsafe
            {
                System.Buffer.MemoryCopy(dataPointer.Pointer.ToPointer(), data.ToPointer(), (long)TotalSize, (long)TotalSize);
            }

            GraphicsDevice.UnmapMemory(stagingBufferMemory);

            CreateBuffer(TotalSize, BufferUsageFlags.TransferDst | Usage, MemoryFlags, out VulkanBuffer, out BufferMemory);
            CopyBuffer(stagingBuffer, VulkanBuffer, TotalSize);

            stagingBuffer.Destroy(GraphicsDevice);
            stagingBufferMemory.FreeMemory(GraphicsDevice);
        }

        /// <summary>
        /// Return an equivalent staging texture CPU read-writable from this instance.
        /// </summary>
        /// <returns>A new instance of this buffer as a staging resource</returns>
        //public Buffer ToStaging()
        //{
        //    stagingDesc.BindFlags = BindFlags.None;
        //    stagingDesc.CpuAccessFlags = CpuAccessFlags.Read | CpuAccessFlags.Write;
        //    stagingDesc.Usage = MemoryPropertyFlags.Staging;
        //    stagingDesc.OptionFlags = ResourceOptionFlags.None;
        //    return new Buffer(GraphicsDevice, stagingDesc, BufferUsageFlags, ViewFormat, IntPtr.Zero);
        //}

        private void CreateBuffer(ulong size, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties, out VulkanBuffer buffer, out DeviceMemory bufferMemory)
        {
            BufferCreateInfo bufferInfo = new BufferCreateInfo();
            bufferInfo.Size = size;
            bufferInfo.Usage = (uint)usage;
            bufferInfo.SharingMode = SharingMode.Exclusive;

            buffer = GraphicsDevice.LogicalDevice.CreateBuffer(bufferInfo);

            MemoryRequirements memoryRequirements = GraphicsDevice.LogicalDevice.GetBufferMemoryRequirements(buffer);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo();
            allocInfo.AllocationSize = memoryRequirements.Size;
            allocInfo.MemoryTypeIndex = GraphicsDevice.Instance.CurrentDevice.FindMemoryIndex(memoryRequirements.MemoryTypeBits, memoryProperties);

            bufferMemory = GraphicsDevice.LogicalDevice.AllocateMemory(allocInfo);

            var result = GraphicsDevice.LogicalDevice.BindBufferMemory(buffer, bufferMemory, 0);
            if (result != Result.Success)
            {
                throw new GraphicsEngineException("Failed to bind buffer memory to buffer");
            }
        }

        private void CopyBuffer(VulkanBuffer srcBuffer, VulkanBuffer dstBuffer, ulong size)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommand();

            BufferCopy copyRegin = new BufferCopy();
            copyRegin.Size = size;
            var regions = new BufferCopy[1] { copyRegin };
            commandBuffer.CmdCopyBuffer(srcBuffer, dstBuffer, 1, regions);

            GraphicsDevice.EndSingleTimeCommands(commandBuffer);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="description">The description of the buffer.</param>
        /// <param name="viewFormat">View format used if the buffer is used as a shared resource view.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, BufferCreateInfo description, uint count, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return new Buffer(device, description, count, memoryFlags);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="bufferSize">Size of the buffer in bytes.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, int bufferSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return New(device, bufferSize, 0, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="elementCount">Number of T elements in this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(GraphicsDevice device, uint elementCount, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            int elementSize = Utilities.SizeOf<T>();
            var bufferSize = elementSize * elementCount;
            BufferCreateInfo info = new BufferCreateInfo();
            info.SharingMode = SharingMode.Exclusive;
            info.Size = (ulong)bufferSize;
            info.Usage = (uint)bufferFlags;

            return new Buffer<T>(device, info, elementCount, memoryFlags);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="bufferSize">Size of the buffer in bytes.</param>
        /// <param name="elementSize">Size of an element in the buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, int bufferSize, int elementSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive)
        {
            return New(device, bufferSize, elementSize, bufferFlags, memoryFlags, sharingMode);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="bufferSize">Size of the buffer in bytes.</param>
        /// <param name="elementSize">Size of an element in the buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="viewFormat">The view format must be specified if the buffer is declared as a shared resource view.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, int bufferSize, int elementSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return New(device, bufferSize, elementSize, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="value">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(GraphicsDevice device, ref T value, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            return New(device, ref value, bufferFlags, memoryFlags);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="value">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="viewFormat">The view format must be specified if the buffer is declared as a shared resource view.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(GraphicsDevice device, ref T value, BufferUsageFlags bufferFlags, Format viewFormat, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                int bufferSize = Utilities.SizeOf<T>();
                int elementSize = Utilities.SizeOf<T>();
                var data = new DataPointer(handle.AddrOfPinnedObject(), (ulong)bufferSize, (uint)(bufferSize / elementSize));

                return new Buffer<T>(device, bufferFlags, data, memoryFlags);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="initialValue">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(GraphicsDevice device, T[] initialValue, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            return New(device, initialValue, bufferFlags, memoryFlags);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="initialValue">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="viewFormat">The view format must be specified if the buffer is declared as a shared resource view.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(GraphicsDevice device, T[] initialValue, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(initialValue, GCHandleType.Pinned);
            try
            {
                int elementSize = Utilities.SizeOf<T>();
                int bufferSize = elementSize * initialValue.Length;
                var data = new DataPointer(handle.AddrOfPinnedObject(), (uint)initialValue.Length, (uint)elementSize);

                return new Buffer<T>(device, bufferFlags, data, memoryFlags, sharingMode);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance from a byte array.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="initialValue">The initial value of this buffer.</param>
        /// <param name="elementSize">Size of an element. Must be equal to 2 or 4 for an index buffer, or to the size of a struct for a structured/typed buffer. Can be set to 0 for other buffers.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="viewFormat">The view format must be specified if the buffer is declared as a shared resource view.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, byte[] initialValue, int elementSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            GCHandle handle = GCHandle.Alloc(initialValue, GCHandleType.Pinned);
            try
            {
                int bufferSize = initialValue.Length;
                var data = new DataPointer(handle.AddrOfPinnedObject(), (ulong)initialValue.Length, (uint)(initialValue.Length / elementSize));

                return new Buffer(device, bufferFlags, data, memoryFlags, SharingMode.Exclusive);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="dataPointer">The data pointer.</param>
        /// <param name="elementSize">Size of the element.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, DataPointer dataPointer, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return New(device, dataPointer, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="dataPointer">The data pointer.</param>
        /// <param name="elementSize">Size of the element.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="viewFormat">The view format must be specified if the buffer is declared as a shared resource view.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(GraphicsDevice device, DataPointer dataPointer, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive)
        {
            return new Buffer(device, bufferFlags, dataPointer, memoryFlags, sharingMode);
        }

        public static implicit operator VulkanBuffer(Buffer buffer)
        {
            return buffer.VulkanBuffer;
        }
    }

    /// <summary>
    /// A buffer with typed information.
    /// </summary>
    /// <typeparam name="T">Type of an element of this buffer.</typeparam>
    public class Buffer<T> : Buffer where T : struct
    {
        internal Buffer(GraphicsDevice device, BufferUsageFlags bufferFlags, DataPointer dataPointer, MemoryPropertyFlags memoryFlags, SharingMode sharingMode = SharingMode.Exclusive)
           : base(device, bufferFlags, dataPointer, memoryFlags, sharingMode)
        {
        }

        internal Buffer(GraphicsDevice device, BufferCreateInfo bufferInfo, uint count, MemoryPropertyFlags memoryFlags)
           : base(device, bufferInfo, count, memoryFlags)
        {
        }

        /// <summary>
        /// Gets the content of this texture to an array of data.
        /// </summary>
        /// <returns>An array of data.</returns>
        /// <msdn-id>ff476457</msdn-id>
        ///   <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        ///   <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        /// <remarks>This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice" />.
        /// This method creates internally a staging resource if this texture is not already a staging resource, copies to it and map it to memory. Use method with explicit staging resource
        /// for optimal performances.</remarks>
        //public T[] GetData()
        //{
        //    return GetData<T>();
        //}

        /// <summary>
        /// Copies the content of a single structure data from CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        //public void SetData(ref T fromData, int offsetInBytes = 0, SetDataOptions options = SetDataOptions.Discard)
        //{
        //    base.SetData(ref fromData, offsetInBytes, options);
        //}

        /// <summary>
        /// Copies the content an array of data from CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="startIndex">Index to start copying from.</param>
        /// <param name="elementCount">Number of elements to copy.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        //public void SetData(T[] fromData, int startIndex = 0, int elementCount = 0, int offsetInBytes = 0, SetDataOptions options = SetDataOptions.Discard)
        //{
        //    base.SetData(fromData, startIndex, elementCount, offsetInBytes, options);
        //}

        /// <summary>
        /// Copies the content of a single structure data from CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        //public void SetData(GraphicsDevice device, ref T fromData, int offsetInBytes = 0, SetDataOptions options = SetDataOptions.Discard)
        //{
        //    base.SetData(device, ref fromData, offsetInBytes, options);
        //}

        /// <summary>
        /// Copies the content an array of data from CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="startIndex">Buffer index to begin copying from.</param>
        /// <param name="elementCount">Number of elements to copy.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        //public void SetData(GraphicsDevice device, T[] fromData, int startIndex = 0, int elementCount = 0, int offsetInBytes = 0, SetDataOptions options = SetDataOptions.Discard)
        //{
        //    base.SetData(device, fromData, startIndex, elementCount, offsetInBytes, options);
        //}
    }
}
