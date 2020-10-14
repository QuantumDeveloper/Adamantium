using Adamantium.Core;
using AdamantiumVulkan.Core;
using System;
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

        public int TotalSize => (int)(ElementSize * ElementCount);

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

        protected Buffer(GraphicsDevice device, BufferUsageFlags bufferFlags, int size, uint count, MemoryPropertyFlags memoryflags, SharingMode sharingMode)
        {
            GraphicsDevice = device;
            Usage = bufferFlags;
            MemoryFlags = memoryflags;
            SharingMode = sharingMode;
            ElementSize = (uint)size / count;
            ElementCount = count;

            CreateBuffer(size, BufferUsageFlags.TransferDst | Usage, MemoryFlags | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent, out VulkanBuffer, out BufferMemory);
        }

        private void Initialize(DataPointer dataPointer)
        {
            VulkanBuffer stagingBuffer;
            DeviceMemory stagingBufferMemory;

            var stagingMemoryFlags = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent; // Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                stagingMemoryFlags = MemoryPropertyFlags.DeviceLocal | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent | MemoryPropertyFlags.HostCached; // MacOS
            }

            CreateBuffer(TotalSize, BufferUsageFlags.TransferSrc, stagingMemoryFlags, out stagingBuffer, out stagingBufferMemory);

            UpdateBufferContent(stagingBufferMemory, dataPointer);
            CreateBuffer(TotalSize, BufferUsageFlags.TransferDst | Usage, MemoryFlags | MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent, out VulkanBuffer, out BufferMemory);
            CopyBuffer(stagingBuffer, VulkanBuffer, TotalSize);

            stagingBuffer.Destroy(GraphicsDevice);
            stagingBufferMemory.FreeMemory(GraphicsDevice);
        }

        private unsafe void UpdateBufferContent(DeviceMemory bufferMemory, DataPointer dataPointer, ulong offset = 0)
        {
            var data = GraphicsDevice.MapMemory(bufferMemory, offset, (ulong)TotalSize, 0);
            System.Buffer.MemoryCopy(dataPointer.Pointer.ToPointer(), data.ToPointer(), TotalSize, dataPointer.Size);
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

        private void CreateBuffer(int size, BufferUsageFlags usage, MemoryPropertyFlags memoryProperties, out VulkanBuffer buffer, out DeviceMemory bufferMemory)
        {
            BufferCreateInfo bufferInfo = new BufferCreateInfo();
            bufferInfo.Size = (ulong)size;
            bufferInfo.Usage = (uint)usage;
            bufferInfo.SharingMode = SharingMode.Exclusive;

            buffer = GraphicsDevice.LogicalDevice.CreateBuffer(bufferInfo);

            MemoryRequirements memoryRequirements = GraphicsDevice.LogicalDevice.GetBufferMemoryRequirements(buffer);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo();
            allocInfo.AllocationSize = memoryRequirements.Size;
            allocInfo.MemoryTypeIndex = GraphicsDevice.VulkanInstance.CurrentDevice.FindMemoryIndex(memoryRequirements.MemoryTypeBits, memoryProperties);

            bufferMemory = GraphicsDevice.LogicalDevice.AllocateMemory(allocInfo);

            var result = GraphicsDevice.LogicalDevice.BindBufferMemory(buffer, bufferMemory, 0);
            if (result != Result.Success)
            {
                throw new GraphicsEngineException("Failed to bind buffer memory to buffer");
            }
        }

        private void CopyBuffer(VulkanBuffer srcBuffer, VulkanBuffer dstBuffer, int size)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();

            BufferCopy copyRegin = new BufferCopy();
            copyRegin.Size = (ulong)size;
            var regions = new BufferCopy[1] { copyRegin };
            commandBuffer.CopyBuffer(srcBuffer, dstBuffer, 1, regions);

            GraphicsDevice.EndSingleTimeCommands(commandBuffer);
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
            var toData = new TData[TotalSize / Utilities.SizeOf<TData>()];
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
            GetData(new DataPointer(handle.AddrOfPinnedObject(), Utilities.SizeOf<TData>()));
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
            GetData(new DataPointer(handle.AddrOfPinnedObject(), toData.Length * Utilities.SizeOf<TData>()));
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
        public void GetData(DataPointer toData)
        {
            // Check size validity of data to copy to
            if (toData.Size > TotalSize)
                throw new ArgumentException("Length of TData is larger than size of buffer");

            var data = GraphicsDevice.MapMemory(BufferMemory, 0, (ulong)toData.Size, 0);
            unsafe
            {
                System.Buffer.MemoryCopy(data.ToPointer(), toData.Pointer.ToPointer(), toData.Size, toData.Size);
            }
            GraphicsDevice.UnmapMemory(BufferMemory);
        }

        /// <summary>
        /// Copies the content of a single structure data from CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Data writing behavior</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        public void SetData<TData>(ref TData fromData, uint offsetInBytes = 0) where TData : struct
        {
            SetData(GraphicsDevice, ref fromData, offsetInBytes);
        }

        /// <summary>
        /// Copies the content an array of data from CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="startIndex">Index to begin setting data from.</param>
        /// <param name="elementCount">The number of elements to set.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        public void SetData<TData>(TData[] fromData, uint startIndex = 0, uint elementCount = 0, uint offsetInBytes = 0) where TData : struct
        {
            SetData(GraphicsDevice, fromData, startIndex, elementCount, offsetInBytes);
        }

        /// <summary>
        /// Copies the content an array of data on CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <param name="fromData">A data pointer.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <msdn-id>ff476457</msdn-id>
        ///   <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        ///   <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        /// <remarks>
        /// This method is only working when called from the main thread that is accessing the main <see cref="GraphicsDevice"/>. See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        public void SetData(DataPointer fromData, uint offsetInBytes = 0)
        {
            SetData(GraphicsDevice, fromData, offsetInBytes);
        }

        /// <summary>
        /// Copies the content an array of data on CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        public void SetData<TData>(GraphicsDevice device, ref TData fromData, uint offsetInBytes = 0) where TData : struct
        {
            GCHandle handle = GCHandle.Alloc(fromData, GCHandleType.Pinned);
            SetData(device, new DataPointer(handle.AddrOfPinnedObject(), Utilities.SizeOf<TData>()), offsetInBytes);
            handle.Free();
        }

        /// <summary>
        /// Copies the content an array of data on CPU memory to this buffer into GPU memory.
        /// </summary>
        /// <typeparam name="TData">The type of the T data.</typeparam>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="fromData">The data to copy from.</param>
        /// <param name="startIndex">The starting index to begin setting data from.</param>
        /// <param name="elementCount">The number of elements to set.</param>
        /// <param name="offsetInBytes">The offset in bytes to write to.</param>
        /// <param name="options">Buffer data behavior.</param>
        /// <exception cref="System.ArgumentException"></exception>
        /// <remarks>
        /// See the unmanaged documentation about Map/UnMap for usage and restrictions.
        /// </remarks>
        /// <msdn-id>ff476457</msdn-id>
        /// <unmanaged>HRESULT ID3D11DeviceContext::Map([In] ID3D11Resource* pResource,[In] unsigned int Subresource,[In] D3D11_MAP MapType,[In] D3D11_MAP_FLAG MapFlags,[Out] D3D11_MAPPED_SUBRESOURCE* pMappedResource)</unmanaged>
        /// <unmanaged-short>ID3D11DeviceContext::Map</unmanaged-short>
        public unsafe void SetData<TData>(GraphicsDevice device, TData[] fromData, uint startIndex = 0, uint elementCount = 0, uint offsetInBytes = 0) where TData : struct
        {
            GCHandle handle = GCHandle.Alloc(fromData, GCHandleType.Pinned);
            byte* bytePtr = (byte*)handle.AddrOfPinnedObject();
            var sizeOfT = Utilities.SizeOf<TData>();
            var sourcePtr = (IntPtr)(bytePtr + startIndex * sizeOfT);
            var sizeOfData = (elementCount == 0 ? (uint)fromData.Length : elementCount) * sizeOfT;
            SetData(device, new DataPointer(sourcePtr, (int)sizeOfData), offsetInBytes);
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
        public unsafe void SetData(GraphicsDevice device, DataPointer fromData, uint offsetInBytes = 0)
        {
            // Check size validity of data to copy to
            if ((fromData.Size + offsetInBytes) > TotalSize)
                throw new ArgumentException("Size of data to upload + offset is larger than size of buffer");

            UpdateBufferContent(BufferMemory, fromData, offsetInBytes);
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
            return New(device, bufferSize, bufferSize, bufferFlags, memoryFlags, SharingMode.Exclusive);
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
        public static Buffer<T> New<T>(GraphicsDevice device, uint elementCount, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal, SharingMode sharingMode = SharingMode.Exclusive) where T : struct
        {
            int elementSize = Utilities.SizeOf<T>();
            var bufferSize = elementSize * elementCount;
            var info = new BufferCreateInfo();
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
            uint count = (uint)(bufferSize / elementSize);

            return new Buffer(device, bufferFlags, bufferSize, count, memoryFlags, sharingMode);
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
        /// <param name="viewFormat">The view format must be specified if the buffer is declared as a shared resource view.</param>
        /// <param name="memoryFlags">The memoryFlags.</param>
        /// <returns>An instance of a new <see cref="Buffer" /></returns>
        /// <msdn-id>ff476501</msdn-id>
        /// <unmanaged>HRESULT ID3D11Device::CreateBuffer([In] const D3D11_BUFFER_DESC* pDesc,[In, Optional] const D3D11_SUBRESOURCE_DATA* pInitialData,[Out, Fast] ID3D11Buffer** ppBuffer)</unmanaged>
        /// <unmanaged-short>ID3D11Device::CreateBuffer</unmanaged-short>
        public static Buffer<T> New<T>(GraphicsDevice device, ref T value, BufferUsageFlags bufferFlags, MemoryPropertyFlags memoryFlags = MemoryPropertyFlags.DeviceLocal) where T : struct
        {
            GCHandle handle = GCHandle.Alloc(value, GCHandleType.Pinned);
            try
            {
                int bufferSize = Utilities.SizeOf<T>();
                int elementSize = Utilities.SizeOf<T>();
                var data = new DataPointer(handle.AddrOfPinnedObject(), bufferSize, (uint)(bufferSize / elementSize));

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
            return New(device, initialValue, bufferFlags, memoryFlags, SharingMode.Exclusive);
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
                var data = new DataPointer(handle.AddrOfPinnedObject(), bufferSize, (uint)initialValue.Length);

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
                var data = new DataPointer(handle.AddrOfPinnedObject(), bufferSize, (uint)initialValue.Length);

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
        public T[] GetData()
        {
            return GetData<T>();
        }

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
        public void SetData(ref T fromData, uint offsetInBytes = 0)
        {
            base.SetData(ref fromData, offsetInBytes);
        }

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
        public void SetData(T[] fromData, uint startIndex = 0, uint elementCount = 0, uint offsetInBytes = 0)
        {
            base.SetData(fromData, startIndex, elementCount, offsetInBytes);
        }

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
        public void SetData(GraphicsDevice device, ref T fromData, uint offsetInBytes = 0)
        {
            base.SetData(device, ref fromData, offsetInBytes);
        }

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
        public void SetData(GraphicsDevice device, T[] fromData, uint startIndex = 0, uint elementCount = 0, uint offsetInBytes = 0)
        {
            base.SetData(device, fromData, startIndex, elementCount, offsetInBytes);
        }
    }
}
