using System;
using Adamantium.Imaging;
using Adamantium.Core;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Engine.Core.Content;
using Adamantium.Win32;
using VulkanImage = AdamantiumVulkan.Core.Image;
using AdamantiumVulkan.Core;
using Image = Adamantium.Imaging.Image;
using VkBuffer = AdamantiumVulkan.Core.Buffer;

namespace Adamantium.Engine.Graphics
{
    [ContentReader(typeof(TextureContentReader))]
    public unsafe class Texture : GraphicsResource
    {
        private VulkanImage vulkanImage;
        private DeviceMemory vulkanImageMemory;
        
        public TextureDescription Description;

        public uint Width => Description.Width;

        public uint Height => Description.Height;

        public SurfaceFormat SurfaceFormat => Description.Format;

        public ImageLayout ImageLayout { get; internal set; }
        
        public ulong TotalSizeInBytes { get; private set; }

        public void* NativePointer => VulkanImage.NativePointer;
        
        protected VulkanImage VulkanImage => vulkanImage;
        protected DeviceMemory ImageMemory => vulkanImageMemory;
        protected ImageView ImageView { get; set; }

        protected Texture(GraphicsDevice device, TextureDescription description) : base(device)
        {
            Description = description;
            ImageLayout = description.InitialLayout;
            Initialize();
            this.TransitionImageLayout(description.DesiredImageLayout);
        }

        protected Texture(GraphicsDevice device, Image img, ImageUsageFlagBits usage, ImageLayout desiredLayout) : 
            base(device)
        {
            Description = img.Description;
            Description.Usage |= ImageUsageFlagBits.TransferDstBit | usage;
            Description.DesiredImageLayout = desiredLayout;
            TotalSizeInBytes = img.TotalSizeInBytes;

            CopyDataToImage(TotalSizeInBytes, img.DataPointer);
        }
        
        protected Texture(GraphicsDevice device, IRawBitmap bitmap, ImageUsageFlagBits usage, ImageLayout desiredLayout) : 
            base(device)
        {
            Description = bitmap.GetImageDescription();
            Description.Usage |= ImageUsageFlagBits.TransferDstBit | usage;
            Description.DesiredImageLayout = desiredLayout;
            TotalSizeInBytes = bitmap.TotalSizeInBytes;

            var pixelData = bitmap.GetRawPixels(0);
            var handle = GCHandle.Alloc(pixelData, GCHandleType.Pinned);

            CopyDataToImage(TotalSizeInBytes, handle.AddrOfPinnedObject());
            
            handle.Free();
        }
        
        protected Texture(GraphicsDevice device, ImageDescription description, byte[] data, ImageUsageFlagBits usage, ImageLayout desiredLayout) : 
            base(device)
        {
            Description = description;
            Description.Usage |= ImageUsageFlagBits.TransferDstBit | usage;
            Description.DesiredImageLayout = desiredLayout;
            TotalSizeInBytes = (ulong)data.Length;

            var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            CopyDataToImage(TotalSizeInBytes, handle.AddrOfPinnedObject());
            handle.Free();
        }

        private void CopyDataToImage(ulong totalSizeInBytes, IntPtr pointerToPixelData)
        {
            CreateBuffer(TotalSizeInBytes, 
                BufferUsageFlagBits.TransferSrcBit,
                MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent, 
                out var stagingBuffer,
                out var stagingBufferMemory);
            
            var data = GraphicsDevice.MapMemory(stagingBufferMemory, 0, totalSizeInBytes, 0);
            System.Buffer.MemoryCopy(pointerToPixelData.ToPointer(), data, totalSizeInBytes, totalSizeInBytes);
            GraphicsDevice.UnmapMemory(stagingBufferMemory);
            
            CreateImage(Description, MemoryPropertyFlags.DeviceLocal, out vulkanImage, out vulkanImageMemory);
            this.TransitionImageLayout(ImageLayout.TransferDstOptimal);
            CopyBufferToImage(stagingBuffer, vulkanImage, Description);
            this.TransitionImageLayout(Description.DesiredImageLayout);
            CreateImageView(Description);
            
            stagingBuffer.Destroy(GraphicsDevice);
            stagingBufferMemory.FreeMemory(GraphicsDevice);
        }
        
        private void CreateBuffer(ulong size, BufferUsageFlagBits usage, MemoryPropertyFlags memoryProperties, out VkBuffer buffer, out DeviceMemory bufferMemory)
        {
            var device = (Device)GraphicsDevice;
            
            BufferCreateInfo bufferInfo = new BufferCreateInfo();
            bufferInfo.Size = size;
            bufferInfo.Usage = usage;
            bufferInfo.SharingMode = SharingMode.Exclusive;

            buffer = GraphicsDevice.LogicalDevice.CreateBuffer(bufferInfo);

            MemoryRequirements memoryRequirements = GraphicsDevice.LogicalDevice.GetBufferMemoryRequirements(buffer);

            var allocInfo = new MemoryAllocateInfo
            {
                AllocationSize = memoryRequirements.Size,
                MemoryTypeIndex =
                    GraphicsDevice.VulkanInstance.CurrentDevice.FindMemoryIndex(memoryRequirements.MemoryTypeBits, memoryProperties)
            };

            if (device.AllocateMemory(allocInfo, null, out bufferMemory) != Result.Success)
            {
                throw new Exception("failed to allocate buffer memory!");
            }

            if (GraphicsDevice.LogicalDevice.BindBufferMemory(buffer, bufferMemory, 0) != Result.Success)
            {
                throw new Exception("failed to bind buffer memory!");
            }
        }

        private void CopyBufferToImage(VkBuffer buffer, VulkanImage image, TextureDescription description)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();

            BufferImageCopy region = new BufferImageCopy();
            region.BufferOffset = 0;
            region.BufferRowLength = 0;
            region.BufferImageHeight = 0;
            region.ImageSubresource = new ImageSubresourceLayers();
            region.ImageSubresource.AspectMask = ImageAspectFlagBits.ColorBit;
            region.ImageSubresource.MipLevel = 0;
            region.ImageSubresource.BaseArrayLayer = 0;
            region.ImageSubresource.LayerCount = 1;
            region.ImageOffset = new Offset3D() { X = 0, Y = 0, Z = 0};
            region.ImageExtent = new Extent3D() {Width = description.Width, Height = Description.Height, Depth = 1}; 
            
            commandBuffer.CopyBufferToImage(buffer, image, ImageLayout.TransferDstOptimal, 1, region);
            GraphicsDevice.EndSingleTimeCommands(commandBuffer);
        }
        
        private void CopyImageToBuffer(VkBuffer buffer, VulkanImage image, TextureDescription description)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();

            BufferImageCopy region = new BufferImageCopy();
            region.BufferOffset = 0;
            region.BufferRowLength = 0;
            region.BufferImageHeight = 0;
            region.ImageSubresource = new ImageSubresourceLayers();
            region.ImageSubresource.AspectMask = ImageAspectFlagBits.ColorBit;
            region.ImageSubresource.MipLevel = 0;
            region.ImageSubresource.BaseArrayLayer = 0;
            region.ImageSubresource.LayerCount = 1;
            region.ImageOffset = new Offset3D() { X = 0, Y = 0, Z = 0};
            region.ImageExtent = new Extent3D() {Width = description.Width, Height = description.Height, Depth = 1}; 
            
            commandBuffer.CopyImageToBuffer(image, ImageLayout.TransferDstOptimal, buffer, 1, region);
            GraphicsDevice.EndSingleTimeCommands(commandBuffer);
        }
        
        private MemoryPropertyFlags GetStagingMemoryFlags()
        {
            var stagingMemoryFlags = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent; // Windows
            // if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            // {
            //     stagingMemoryFlags = MemoryPropertyFlags.DeviceLocal | 
            //                          MemoryPropertyFlags.HostVisible | 
            //                          MemoryPropertyFlags.HostCoherent | 
            //                          MemoryPropertyFlags.HostCached; // MacOS
            // }

            return stagingMemoryFlags;
        }

        protected void Initialize()
        {
            CreateImage(Description, MemoryPropertyFlags.DeviceLocal, out vulkanImage, out vulkanImageMemory);
            CreateImageView(Description);
        }

        protected void CreateImage(TextureDescription description, MemoryPropertyFlags memoryProperties, out VulkanImage image, out DeviceMemory imageMemory)
        {
            var device = (Device)GraphicsDevice;
            var imageInfo = description.ToImageCreateInfo();
            if (device.CreateImage(imageInfo, null, out image) != Result.Success)
            {
                throw new Exception("failed to create image!");
            }

            device.GetImageMemoryRequirements(image, out var memRequirements);
            TotalSizeInBytes = memRequirements.Size;

            var allocInfo = new MemoryAllocateInfo
            {
                AllocationSize = memRequirements.Size,
                MemoryTypeIndex =
                    GraphicsDevice.VulkanInstance.CurrentDevice.FindMemoryIndex(memRequirements.MemoryTypeBits, memoryProperties)
            };

            if (device.AllocateMemory(allocInfo, null, out imageMemory) != Result.Success)
            {
                throw new Exception("failed to allocate image memory!");
            }

            device.BindImageMemory(image, imageMemory, 0);
            ImageLayout = imageInfo.InitialLayout;
        }

        protected void CreateImageView(TextureDescription description)
        {
            var createInfo = new ImageViewCreateInfo();
            createInfo.Image = VulkanImage;
            createInfo.ViewType = (ImageViewType)description.Dimension;
            createInfo.Format = SurfaceFormat;
            ComponentMapping componentMapping = new ComponentMapping
            {
                R = ComponentSwizzle.Identity,
                G = ComponentSwizzle.Identity,
                B = ComponentSwizzle.Identity,
                A = ComponentSwizzle.Identity
            };
            createInfo.Components = componentMapping;
            ImageSubresourceRange subresourceRange = new ImageSubresourceRange
            {
                AspectMask = description.ImageAspect,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            };
            createInfo.SubresourceRange = subresourceRange;

            ImageView = GraphicsDevice.LogicalDevice.CreateImageView(createInfo);
        }
        
        public static uint CalculateMipLevels(int width, int height, MipMapCount mipLevels)
        {
            return CalculateMipLevels(width, height, 1, mipLevels);
        }

        public static uint CalculateMipLevels(int width, int height, int depth, MipMapCount mipLevels)
        {
            var maxMipLevels = CountMipLevels(width, height, depth);
            if (mipLevels > 1 && maxMipLevels > mipLevels)
            {
                throw new InvalidOperationException($"MipLevels must be <= {maxMipLevels}");
            }
            return mipLevels;
        }

        private static int CountMipLevels(int width, int height, int depth)
        {
            /*
             * Math.Max function selects the largest dimension. 
             * Math.Log2 function calculates how many times that dimension can be divided by 2.
             * Math.Floor function handles cases where the largest dimension is not a power of 2.
             * 1 is added so that the original image has a mip level.
             */
            var max = Math.Max(Math.Max(width, height), depth);
            var levels = (int)(Math.Floor(Math.Log2(max)) + 1);
            return levels;
        }

        /// <summary>
        /// Creates a new texture with the specified generic texture description.
        /// </summary>
        /// <param name="graphicsDevice">The graphics device.</param>
        /// <param name="description">The description.</param>
        /// <returns>A Texture instance, either a RenderTarget or DepthStencilBuffer or Texture, depending on Binding flags.</returns>
        /// <exception cref="ArgumentNullException"></exception>
        public static Texture New(GraphicsDevice graphicsDevice, TextureDescription description)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            // TODO: check how this could be implemented
            if (description.Usage.HasFlag(ImageUsageFlagBits.ColorAttachmentBit))
            {
                return new RenderTarget(graphicsDevice, description);
            }
            else if (description.Usage.HasFlag(ImageUsageFlagBits.DepthStencilAttachmentBit))
            {
                return new DepthStencilBuffer(graphicsDevice, description);
            }
            else
            {
                return new Texture(graphicsDevice, description);
            }
            return null;
        }
        
        public static Texture CreateFrom(
            GraphicsDevice graphicsDevice, 
            TextureDescription description, 
            byte[] pixelData,
            ImageUsageFlagBits usage = ImageUsageFlagBits.SampledBit, 
            ImageLayout desiredLayout = ImageLayout.ShaderReadOnlyOptimal)
        {
            if (graphicsDevice == null)
            {
                throw new ArgumentNullException(nameof(graphicsDevice));
            }

            return new Texture(graphicsDevice, description, pixelData, usage, desiredLayout);
        }

        /// <summary>
        /// Loads a texture from a stream.
        /// </summary>
        /// <param name="device">The <see cref="GraphicsDevice"/>.</param>
        /// <param name="stream">The stream to load the texture from.</param>
        /// <param name="sage">Texture flags</param>
        /// <param name="initialLayout">Resource usage</param>
        /// <returns>A <see cref="Texture"/></returns>
        public static Texture Load(GraphicsDevice device, Stream stream, ImageUsageFlagBits sage = ImageUsageFlagBits.SampledBit, ImageLayout initialLayout = ImageLayout.ShaderReadOnlyOptimal)
        {
            var image = BitmapLoader.Load(stream);
            try
            {
                return new Texture(device, image, sage, initialLayout);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace + exception.TargetSite);
            }

            throw new InvalidOperationException("Dimension not supported");
        }

        /// <summary>
        /// Loads a texture from a file.
        /// </summary>
        /// <param name="device">Specify the <see cref="GraphicsDevice"/> used to load and create a texture from a file.</param>
        /// <param name="filePath">The file to load the texture from.</param>
        /// <param name="sage">Texture flags</param>
        /// <param name="usage">Resource usage</param>
        /// <returns>A <see cref="Texture"/></returns>
        public static Texture Load(GraphicsDevice device, String filePath, ImageUsageFlagBits sage = ImageUsageFlagBits.SampledBit, ImageLayout layout = ImageLayout.ShaderReadOnlyOptimal)
        {
            var rawBitmap = BitmapLoader.Load(filePath);
            try
            {
                return new Texture(device, rawBitmap, sage, layout);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
                throw;
            }
        }

        public void Save(string path, ImageFileType fileType)
        {
           var img = Image.New2D(Width, Height, 1, Description.Format);
            
            VkBuffer stagingBuffer;
            DeviceMemory stagingBufferMemory;
            CreateBuffer((ulong)TotalSizeInBytes, BufferUsageFlagBits.TransferSrcBit, MemoryPropertyFlags.HostVisible| MemoryPropertyFlags.HostCoherent, out stagingBuffer, out stagingBufferMemory);
            CopyImageToBuffer(stagingBuffer, vulkanImage, Description);
            unsafe
            {
                var data = GraphicsDevice.MapMemory(stagingBufferMemory, 0, TotalSizeInBytes, 0);
                System.Buffer.MemoryCopy(data, img.DataPointer.ToPointer(), TotalSizeInBytes, TotalSizeInBytes);
                GraphicsDevice.UnmapMemory(stagingBufferMemory);
            }

            img.Save(path, fileType);
           
            stagingBuffer.Destroy(GraphicsDevice);
            stagingBufferMemory.FreeMemory(GraphicsDevice);
        }

        public static implicit operator VulkanImage (Texture texture)
        {
            return texture.VulkanImage;
        }

        public static implicit operator ImageView(Texture texture)
        {
            return texture.ImageView;
        }

        protected override void Dispose(bool disposeManaged)
        {
            VulkanImage?.Destroy(GraphicsDevice);
            ImageView?.Destroy(GraphicsDevice);
            ImageMemory?.FreeMemory(GraphicsDevice);
        }
    }
}
