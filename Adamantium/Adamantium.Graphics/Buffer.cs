using System;
using System.Runtime.InteropServices;
using Adamantium.Core;
using Adamantium.Graphics.Core;
using AdamantiumVulkan.Core;
using VulkanBuffer = AdamantiumVulkan.Core.Buffer;

namespace Adamantium.Graphics
{
    /*
    Member VMA_ALLOCATION_CREATE_USER_DATA_COPY_STRING_BIT
    Preserved for backward compatibility. Consider using vmaSetAllocationName() instead.
    Member VMA_MEMORY_USAGE_CPU_COPY
    Obsolete, preserved for backward compatibility. Prefers not VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT.
    Member VMA_MEMORY_USAGE_CPU_ONLY
    Obsolete, preserved for backward compatibility. Guarantees VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT and VK_MEMORY_PROPERTY_HOST_COHERENT_BIT.
    Member VMA_MEMORY_USAGE_CPU_TO_GPU
    Obsolete, preserved for backward compatibility. Guarantees VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT, prefers VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT.
    Member VMA_MEMORY_USAGE_GPU_ONLY
    Obsolete, preserved for backward compatibility. Prefers VK_MEMORY_PROPERTY_DEVICE_LOCAL_BIT.
    Member VMA_MEMORY_USAGE_GPU_TO_CPU
    Obsolete, preserved for backward compatibility. Guarantees VK_MEMORY_PROPERTY_HOST_VISIBLE_BIT, prefers VK_MEMORY_PROPERTY_HOST_CACHED_BIT.
    */
    
    public partial class Buffer : GraphicsResource, IBuffer
    {
        private VulkanBuffer VulkanBuffer;
        private DeviceMemory BufferMemory;

        public BufferUsageFlags Usage { get; private set; }

        public SharingMode SharingMode { get; private set; }

        public MemoryPropertyFlags MemoryFlags { get; private set; }

        public uint ElementSize { get; private set; }

        public ulong ElementCount { get; private set; }

        public ulong TotalSize => (ElementSize * ElementCount);

        /// <summary>
        /// Initializes a new instance of the <see cref="Adamantium.Graphics.Buffer" /> class.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="memoryFlags">Memory flags.</param>
        /// <param name="bufferFlags">Type of the buffer.</param>
        /// <param name="sharingMode">Sharing mode.</param>
        /// <param name="dataPointer">The data pointer.</param>
        protected Buffer(IGraphicsDevice device, 
            BufferUsageFlags bufferFlags, 
            DataPointer dataPointer,
            MemoryPropertyFlags memoryFlags, 
            SharingMode sharingMode) : base(device)
        {
            Usage = bufferFlags;
            MemoryFlags = memoryFlags;
            SharingMode = sharingMode;
            ElementSize = (uint)(dataPointer.Size / dataPointer.Count);
            ElementCount = dataPointer.Count;

            Initialize(dataPointer);
        }

        protected Buffer(IGraphicsDevice device, 
            BufferCreateInfo description, 
            ulong count, 
            MemoryPropertyFlags memoryFlags) : this(device, (BufferUsageFlags)description.Usage, description.Size, count, memoryFlags, description.SharingMode)
        {
        }

        protected Buffer(IGraphicsDevice device,
            BufferUsageFlags bufferFlags,
            ulong size,
            ulong count,
            MemoryPropertyFlags memoryFlags,
            SharingMode sharingMode) : base(device)
        {
            Usage = bufferFlags;
            MemoryFlags = memoryFlags;
            SharingMode = sharingMode;
            ElementSize = (uint)(size / count);
            ElementCount = count;

            CreateBuffer(size,
                BufferUsageFlags.TransferDst | Usage,
                MemoryFlags | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent,
                out VulkanBuffer,
                out BufferMemory);
        }

        private void Initialize(DataPointer dataPointer)
        {
            var stagingMemoryFlags = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent; // Windows
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            // {
            //     stagingMemoryFlags = MemoryPropertyFlags.DeviceLocal | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent | MemoryPropertyFlags.HostCached; // MacOS
            // }

            CreateBuffer(TotalSize, BufferUsageFlags.TransferSrc, stagingMemoryFlags, out var stagingBuffer, out var stagingBufferMemory);

            UpdateBufferContent(stagingBufferMemory, dataPointer);
            CreateBuffer(TotalSize, 
                BufferUsageFlags.TransferDst | Usage, 
                MemoryFlags | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent, 
                out VulkanBuffer, 
                out BufferMemory);
            CopyBuffer(stagingBuffer, VulkanBuffer, TotalSize);

            GraphicsDevice.Destroy(stagingBuffer);
            GraphicsDevice.Destroy(stagingBufferMemory);
        }

        private unsafe void UpdateBufferContent(DeviceMemory bufferMemory, DataPointer dataPointer, ulong offset = 0)
        {
            var data = GraphicsDevice.MapMemory(bufferMemory, offset, dataPointer.Size, 0);
            System.Buffer.MemoryCopy(dataPointer.Pointer.ToPointer(), data, dataPointer.Size, dataPointer.Size);
            GraphicsDevice.UnmapMemory(bufferMemory);
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
            bufferInfo.Usage = (BufferUsageFlagBits)usage;
            bufferInfo.SharingMode = SharingMode.Exclusive;

            buffer = GraphicsDevice.LogicalDevice.CreateBuffer(bufferInfo);

            MemoryRequirements memoryRequirements = GraphicsDevice.LogicalDevice.GetBufferMemoryRequirements(buffer);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo();
            allocInfo.AllocationSize = memoryRequirements.Size;
            allocInfo.MemoryTypeIndex = GraphicsDevice.Adapter.FindMemoryIndex(memoryRequirements.MemoryTypeBits, memoryProperties);

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
            var regions = new BufferCopy[] { copyRegin };
            commandBuffer.CopyBuffer(srcBuffer, dstBuffer, 1, regions);

            GraphicsDevice.EndSingleTimeCommand(commandBuffer);
        }

        public void CopyFrom(DataBuffer buffer)
        {
            DataPointer pointer = new DataPointer
            {
                Pointer = buffer.DataPointer,
                Size = buffer.Size
            };
            SetData(pointer);
        }

        /// <summary>
        /// Gets the content of this buffer to an array of data.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>.
        /// This method creates internally a staging resource if this texture is not already a staging resource, copies to it and map it to memory. Use method with explicit staging resource
        /// for optimal performances.</remarks>
        /// <msdn-id>ff476457</msdn-id>	
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>	
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>	
        public TData[] GetData<TData>() where TData : struct
        {
            var toData = new TData[TotalSize / (ulong)Utilities.SizeOf<TData>()];
            GetData(toData);
            return toData;
        }

        /// <summary>
        /// Copies the content of this buffer from GPU memory to an array of data on CPU memory using a specific staging resource.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="stagingTexture">The staging buffer used to transfer the buffer.</param>
        /// <param name="toData">To data.</param>
        /// <exception cref="System.ArgumentException">When strides is different from optimal strides, and TData is not the same size as the pixel format, or Width * Height != toData.Length</exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>	
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>	
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>	
        public void GetData<TData>(ref TData toData) where TData : struct
        {
            GCHandle handle = GCHandle.Alloc(toData, GCHandleType.Pinned);
            GetData(new DataPointer(handle.AddrOfPinnedObject(), (ulong)Utilities.SizeOf<TData>()));
            handle.Free();
        }

        /// <summary>
        /// Copies the content of this buffer from GPU memory to an array of data on CPU memory using a specific staging resource.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="stagingTexture">The staging buffer used to transfer the buffer.</param>
        /// <param name="toData">To data.</param>
        /// <exception cref="System.ArgumentException">When strides is different from optimal strides, and TData is not the same size as the pixel format, or Width * Height != toData.Length</exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>	
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>	
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>	
        public void GetData<TData>(TData[] toData) where TData : struct
        {
            GCHandle handle = GCHandle.Alloc(toData, GCHandleType.Pinned);
            GetData(new DataPointer(handle.AddrOfPinnedObject(), (ulong)(toData.Length * Utilities.SizeOf<TData>())));
            handle.Free();
        }

        /// <summary>
        /// Copies the content of this buffer from GPU memory to a CPU memory using a specific staging resource.
        /// </summary>
        /// <param name="stagingTexture">The staging buffer used to transfer the buffer.</param>
        /// <param name="toData">To data pointer.</param>
        /// <exception cref="System.ArgumentException">When strides is different from optimal strides, and TData is not the same size as the pixel format, or Width * Height != toData.Length</exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>	
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>	
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>	
        public unsafe void GetData(DataPointer toData)
        {
            // Check size validity of data to copy to
            if (toData.Size > TotalSize)
                throw new ArgumentException("Length of TData is larger than size of buffer");

            var data = GraphicsDevice.MapMemory(BufferMemory, 0, toData.Size, 0);
            System.Buffer.MemoryCopy(data, toData.Pointer.ToPointer(), toData.Size, toData.Size);
            GraphicsDevice.UnmapMemory(BufferMemory);
        }

        public unsafe void* MapMemory()
        {
            return GraphicsDevice.MapMemory(BufferMemory, 0, TotalSize, 0);
        }

        public void UnmapMemory()
        {
            GraphicsDevice.UnmapMemory(BufferMemory);
        }

        /// <summary>
        /// Copies the content an array of data on CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        public void SetData<TData>(ref TData fromData, uint offsetInBytes = 0) where TData : struct
        {
            GCHandle handle = GCHandle.Alloc(fromData, GCHandleType.Pinned);
            SetData(new DataPointer(handle.AddrOfPinnedObject(), (ulong)Utilities.SizeOf<TData>()), offsetInBytes);
            handle.Free();
        }

        /// <summary>
        /// Copies the content an array of data on CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="startIndex">The starting index to begin setting data from.</param>
        /// <param name="elementCount">The number of elements to set.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        public unsafe void SetData<TData>(TData[] fromData, uint startIndex = 0, uint elementCount = 0, uint offsetInBytes = 0) where TData : struct
        {
            GCHandle handle = GCHandle.Alloc(fromData, GCHandleType.Pinned);
            byte* bytePtr = (byte*)handle.AddrOfPinnedObject();
            var sizeOfT = Utilities.SizeOf<TData>();
            var sourcePtr = (IntPtr)(bytePtr + startIndex * sizeOfT);
            var sizeOfData = (elementCount == 0 ? (uint)fromData.Length : elementCount) * sizeOfT;
            SetData(new DataPointer(sourcePtr, (ulong)sizeOfData), offsetInBytes);
            handle.Free();
        }

        /// <summary>
        /// Copies the content an array of data on CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="fromData">A data pointer.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <msdn-id>ff476457</msdn-id>
        ///   <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        ///   <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        /// <remarks>
        /// See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        public void SetData(DataPointer fromData, uint offsetInBytes = 0)
        {
            // Check size validity of data to copy to
            if ((fromData.Size + offsetInBytes) > TotalSize)
                throw new ArgumentException("Size of data to upload + offset is larger than size of buffer");

            UpdateBufferContent(BufferMemory, fromData, offsetInBytes);
        }
        
        public UInt64 GetDeviceAddress()
        {
            var bufferDeviceAddressInfo = new BufferDeviceAddressInfo();
            bufferDeviceAddressInfo.SType  = StructureType.BufferDeviceAddressInfo;
            bufferDeviceAddressInfo.Buffer = VulkanBuffer;
            return GraphicsDevice.LogicalDevice.GetBufferDeviceAddress(bufferDeviceAddressInfo);
        }

        public VulkanBuffer GetBuffer()
        {
            return VulkanBuffer;
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="description">The description of the buffer.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, BufferCreateInfo description, uint count, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return new Buffer(device, description, count, memoryFlags);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="bufferSize">Size of the buffer in bytes.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, ulong bufferSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return New(device, bufferSize, (int)bufferSize, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="elementCount">Number of T elements in this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <param name="sharingMode">Sharing mode</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        public static Buffer<T> New<T>(
            IGraphicsDevice device, 
            ulong elementCount, 
            BufferUsageFlags bufferFlags, 
            MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, 
            SharingMode sharingMode = SharingMode.Exclusive) where T : struct
        {
            var elementSize = Utilities.SizeOf<T>();
            var bufferSize = (ulong)elementSize * elementCount;
            var info = new BufferCreateInfo();
            info.SharingMode = sharingMode;
            info.Size = (ulong)bufferSize;
            info.Usage = (BufferUsageFlagBits)bufferFlags;

            return new Buffer<T>(device, info, elementCount, memoryFlags);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="bufferSize">Size of the buffer in bytes.</param>
        /// <param name="elementSize">Size of an element in the buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, ulong bufferSize, int elementSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive)
        {
            uint count = (uint)(bufferSize / (ulong)elementSize);

            return new Buffer(device, bufferFlags, bufferSize, count, memoryFlags, sharingMode);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="bufferSize">Size of the buffer in bytes.</param>
        /// <param name="elementSize">Size of an element in the buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, ulong bufferSize, int elementSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return New(device, bufferSize, elementSize, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="value">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(IGraphicsDevice device, ref T value, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                ulong bufferSize = (ulong)Utilities.SizeOf<T>();
                int elementSize = Utilities.SizeOf<T>();
                var data = new DataPointer(handle.AddrOfPinnedObject(), bufferSize, (uint)(bufferSize / (ulong)elementSize));

                return new Buffer<T>(device, bufferFlags, data, memoryFlags);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="initialValue">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(IGraphicsDevice device, T[] initialValue, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            return New(device, initialValue, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <typeparam name="T">Type of the buffer, to get the sizeof from.</typeparam>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="initialValue">The initial value of this buffer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(IGraphicsDevice device, T[] initialValue, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(initialValue, GCHandleType.Pinned);
            try
            {
                int elementSize = Utilities.SizeOf<T>();
                ulong bufferSize = (ulong)(elementSize * initialValue.Length);
                var data = new DataPointer(handle.AddrOfPinnedObject(), bufferSize, (uint)initialValue.Length);

                return new Buffer<T>(device, bufferFlags, data, memoryFlags, sharingMode);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance from a byte array.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="initialValue">The initial value of this buffer.</param>
        /// <param name="elementSize">Size of an element. Must be equal to 2 or 4 for an index buffer, or to the size of a struct for a structured/typed buffer. Can be set to 0 for other buffers.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, byte[] initialValue, int elementSize, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            GCHandle handle = GCHandle.Alloc(initialValue, GCHandleType.Pinned);
            try
            {
                ulong bufferSize = (ulong)initialValue.Length;
                var data = new DataPointer(handle.AddrOfPinnedObject(), bufferSize, (uint)initialValue.Length);

                return new Buffer(device, bufferFlags, data, memoryFlags, SharingMode.Exclusive);
            }
            finally
            {
                handle.Free();
            }
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="dataPointer">The data pointer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, DataPointer dataPointer, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal)
        {
            return New(device, dataPointer, bufferFlags, memoryFlags, SharingMode.Exclusive);
        }

        /// <summary>
        /// Creates a new <see cref="Adamantium.Graphics.Buffer" /> instance.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="dataPointer">The data pointer.</param>
        /// <param name="bufferFlags">The buffer flags to specify the type of buffer.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Adamantium.Graphics.Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer New(IGraphicsDevice device, DataPointer dataPointer, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive)
        {
            return new Buffer(device, bufferFlags, dataPointer, memoryFlags, sharingMode);
        }

        public static implicit operator VulkanBuffer(Buffer buffer)
        {
            return buffer.VulkanBuffer;
        }
        
        public static implicit operator DeviceMemory(Buffer buffer)
        {
            return buffer.BufferMemory;
        }
        
        protected override void Dispose(bool disposeManagedResources)
        {
            GraphicsDevice.Destroy(VulkanBuffer);
            GraphicsDevice.Destroy(BufferMemory);
        }
    }
}
