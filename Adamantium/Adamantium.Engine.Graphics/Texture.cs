using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Imaging;
using Adamantium.Core;
using System.IO;
using System.Runtime.InteropServices;
using Adamantium.Win32;
using VulkanImage = AdamantiumVulkan.Core.Image;
using AdamantiumVulkan.Core;
using Image = Adamantium.Imaging.Image;

namespace Adamantium.Engine.Graphics
{
    public class Texture : DisposableBase
    {
        private VulkanImage vulkanImage;
        private DeviceMemory vulkanImageMemory;
        
        public TextureDescription Description;

        public uint Width => Description.Width;

        public uint Height => Description.Height;

        public SurfaceFormat SurfaceFormat => Description.Format;

        public ImageLayout ImageLayout { get; private set; }

        public IntPtr NativePointer { get; }

        public GraphicsDevice GraphicsDevice { get; private set; }

        protected VulkanImage VulkanImage { get => vulkanImage; set => vulkanImage = value; }
        protected DeviceMemory ImageMemory { get => vulkanImageMemory; set => vulkanImageMemory = value; }
        protected ImageView ImageView { get; set; }

        protected Texture(GraphicsDevice device, TextureDescription description)
        {
            GraphicsDevice = device;
            Description = description;
            Initialize();
            TransitionImageLayout(VulkanImage, description.InitialLayout, description.DesiredImageLayout);
        }

        protected Texture(GraphicsDevice device, Image img, ImageUsageFlagBits usage, ImageLayout desiredLayout)
        {
            //var formats = GraphicsDevice.VulkanInstance.CurrentDevice.GetPhysicalDeviceFormatProperties()
            
            GraphicsDevice = device;
            Description = img.Description;
            Description.Usage |= ImageUsageFlagBits.TransferDstBit | usage;
            Description.DesiredImageLayout = desiredLayout;

            var stagingDescription = Description;
            stagingDescription.Usage = ImageUsageFlagBits.TransferSrcBit;
            stagingDescription.ImageTiling = ImageTiling.Linear;
            var stagingMemoryFlags = GetStagingMemoryFlags();

            CreateImage(stagingDescription, stagingMemoryFlags, out var stagingImage, out var stagingMemory);
            UpdateImageContent(stagingMemory, img.DataPointer, img.TotalSizeInBytes);
            CreateImage(Description, MemoryPropertyFlags.DeviceLocal, out vulkanImage, out vulkanImageMemory);
            
            TransitionImageLayout(stagingImage, ImageLayout.Preinitialized,ImageLayout.TransferSrcOptimal);
            TransitionImageLayout(vulkanImage, ImageLayout.Undefined,ImageLayout.TransferDstOptimal);
            CopyImageToImage(stagingImage, vulkanImage, Description);
            TransitionImageLayout(vulkanImage, ImageLayout.TransferDstOptimal,Description.DesiredImageLayout);
            CreateImageView(Description);
            
            stagingImage.Destroy(GraphicsDevice);
            stagingMemory.FreeMemory(GraphicsDevice);
        }
        
        protected Texture(GraphicsDevice device, Image[] img, ImageUsageFlagBits usage, ImageLayout desiredLayout)
        {
            GraphicsDevice = device;

        }

        private void UpdateImageContent(DeviceMemory imgMemory, IntPtr source, long size)
        {
            var data = GraphicsDevice.MapMemory(imgMemory, 0, (ulong)size, 0);
            Utilities.CopyMemory(data, source, size);
            GraphicsDevice.UnmapMemory(imgMemory);
        }

        private void CopyImageToImage(VulkanImage source, VulkanImage destination, TextureDescription description)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();
            ImageCopy region = new ImageCopy();
            region.SrcOffset = new Offset3D();
            region.DstOffset = new Offset3D();
            region.SrcSubresource = new ImageSubresourceLayers();
            region.DstSubresource = new ImageSubresourceLayers();
            region.SrcSubresource.LayerCount = 1;
            region.SrcSubresource.AspectMask = (uint)ImageAspectFlagBits.ColorBit;
            region.DstSubresource.LayerCount = 1;
            region.DstSubresource.AspectMask = (uint)ImageAspectFlagBits.ColorBit;
            region.Extent = new Extent3D() { Width = description.Width, Height = description.Height, Depth = description.Depth };
            commandBuffer.CopyImage(source, ImageLayout.TransferSrcOptimal, destination, ImageLayout.TransferDstOptimal, 1, region);
            GraphicsDevice.EndSingleTimeCommands(commandBuffer);  
        }

        private MemoryPropertyFlags GetStagingMemoryFlags()
        {
            var stagingMemoryFlags = MemoryPropertyFlags.HostVisible | MemoryPropertyFlags.HostCoherent; // Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                stagingMemoryFlags = MemoryPropertyFlags.DeviceLocal | 
                                     MemoryPropertyFlags.HostVisible | 
                                     MemoryPropertyFlags.HostCoherent | 
                                     MemoryPropertyFlags.HostCached; // MacOS
            }

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
                AspectMask = (uint) description.ImageAspect,
                BaseMipLevel = 0,
                LevelCount = 1,
                BaseArrayLayer = 0,
                LayerCount = 1
            };
            createInfo.SubresourceRange = subresourceRange;

            ImageView = GraphicsDevice.LogicalDevice.CreateImageView(createInfo);
        }

        public void TransitionImageLayout(VulkanImage image, ImageLayout oldLayout, ImageLayout newLayout)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();

            var barrier = new ImageMemoryBarrier
            {
                OldLayout = oldLayout,
                NewLayout = newLayout,
                SrcQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
                DstQueueFamilyIndex = Constants.VK_QUEUE_FAMILY_IGNORED,
                Image = image,
                SubresourceRange = new ImageSubresourceRange()
            };
            
            if (newLayout == ImageLayout.DepthStencilAttachmentOptimal)
            {
                barrier.SubresourceRange.AspectMask = (uint)ImageAspectFlagBits.DepthBit;

                if (SurfaceFormat.HasStencilFormat())
                {
                    barrier.SubresourceRange.AspectMask |= (uint) ImageAspectFlagBits.StencilBit;
                }
            }
            else
            {
                barrier.SubresourceRange.AspectMask = (uint)ImageAspectFlagBits.ColorBit;
            }

            barrier.SubresourceRange.BaseMipLevel = 0;
            barrier.SubresourceRange.LevelCount = 1;
            barrier.SubresourceRange.BaseArrayLayer = 0;
            barrier.SubresourceRange.LayerCount = 1;

            PipelineStageFlagBits sourceStage;
            PipelineStageFlagBits destinationStage;

            if ((oldLayout == ImageLayout.Undefined || oldLayout == ImageLayout.Preinitialized) &&
                (newLayout == ImageLayout.TransferDstOptimal || newLayout == ImageLayout.TransferSrcOptimal))
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = (uint)AccessFlagBits.TransferWriteBit;

                sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                destinationStage = PipelineStageFlagBits.TransferBit;
            }
            else if ((oldLayout == ImageLayout.TransferSrcOptimal || oldLayout == ImageLayout.TransferDstOptimal) 
                     && newLayout == ImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.SrcAccessMask = (uint)AccessFlagBits.TransferWriteBit;
                barrier.DstAccessMask = (uint) AccessFlagBits.ShaderReadBit;

                sourceStage = PipelineStageFlagBits.TransferBit;
                destinationStage = PipelineStageFlagBits.FragmentShaderBit;
            }
            else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = (uint)(AccessFlagBits.DepthStencilAttachmentReadBit |
                                        AccessFlagBits.DepthStencilAttachmentWriteBit);

                sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                destinationStage = PipelineStageFlagBits.EarlyFragmentTestsBit;
            }
            else if (oldLayout == ImageLayout.Undefined && newLayout == ImageLayout.ColorAttachmentOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = (uint)(AccessFlagBits.ColorAttachmentReadBit| AccessFlagBits.ColorAttachmentWriteBit);
                sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                destinationStage = PipelineStageFlagBits.ColorAttachmentOutputBit;
            }
            else
            {
                throw new ImageLayoutTransitionException(
                    $"Transition from {ImageLayout} to {newLayout} is not supported");
            }

            commandBuffer.PipelineBarrier(
                (uint) sourceStage, 
                (uint) destinationStage, 
                0, 
                0, 
                null, 
                0, 
                null, 
                1,
                barrier);

            ImageLayout = newLayout;

            GraphicsDevice.EndSingleTimeCommands(commandBuffer);
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

            if (description.Usage.HasFlag(ImageUsageFlagBits.ColorAttachmentBit))
            {
                //switch (description.Dimension)
                //{
                //    case TextureDimension.Texture1D:
                //        return RenderTarget1D.New(graphicsDevice, description);
                //    case TextureDimension.Texture2D:
                //        return RenderTarget2D.New(graphicsDevice, description);
                //    case TextureDimension.Texture3D:
                //        return RenderTarget3D.New(graphicsDevice, description);
                //    case TextureDimension.TextureCube:
                //        return RenderTargetCube.New(graphicsDevice, description);
                //}
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

        /// <summary>
        /// Loads a texture from a stream.
        /// </summary>
        /// <param name="device">The <see cref="D3DGraphicsDevice"/>.</param>
        /// <param name="stream">The stream to load the texture from.</param>
        /// <param name="flags">Texture flags</param>
        /// <param name="usage">Resource usage</param>
        /// <returns>A <see cref="Texture"/></returns>
        public static Texture Load(GraphicsDevice device, Stream stream, ImageUsageFlagBits flags = ImageUsageFlagBits.SampledBit, ImageLayout usage = ImageLayout.ShaderReadOnlyOptimal, ImageFileType fileType = ImageFileType.Unknown)
        {
            var image = Adamantium.Imaging.Image.Load(stream, fileType);
            try
            {
                return GetTextureFromImage(image, device, flags, usage);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace + exception.TargetSite);
            }
            finally
            {
                image?.Dispose();
            }

            throw new InvalidOperationException("Dimension not supported");
        }

        private static Texture GetTextureFromImage(Image image, GraphicsDevice device, ImageUsageFlagBits flags, ImageLayout usage)
        {
            if (image == null)
            {
                return null;
            }

            return new Texture(device, image, flags, usage);
        }

        /// <summary>
        /// Loads a texture from a file.
        /// </summary>
        /// <param name="device">Specify the <see cref="D3DGraphicsDevice"/> used to load and create a texture from a file.</param>
        /// <param name="filePath">The file to load the texture from.</param>
        /// <param name="flags">Texture flags</param>
        /// <param name="usage">Resource usage</param>
        /// <returns>A <see cref="Texture"/></returns>
        public static Texture Load(GraphicsDevice device, String filePath, ImageUsageFlagBits flags = ImageUsageFlagBits.SampledBit, ImageLayout layout = ImageLayout.ShaderReadOnlyOptimal)
        {
            var image = Adamantium.Imaging.Image.Load(filePath);
            try
            {
                return GetTextureFromImage(image, device, flags, layout);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace + exception.TargetSite);
            }
            finally
            {
                image?.Dispose();
            }

            throw new InvalidOperationException("Dimension not supported");
        }

        /// <summary>
        /// Loads a texture from array of strings
        /// </summary>
        /// <param name="device">Specify the <see cref="D3DGraphicsDevice"/> used to load and create a texture from a file.</param>
        /// <param name="filePath">Array of pathes to 6 textures, which will form a <see cref="TextureCube"/></param>
        /// <param name="flags">Texture flags</param>
        /// <param name="usage">Resource usage</param>
        /// <returns>A <see cref="Texture"/></returns>
        /// <remarks>This method is used only for loading <see cref="TextureCube"/> texture. Number of strings in array must be equals to 6</remarks>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        public static Texture Load(GraphicsDevice device, String[] filePath, ImageUsageFlagBits flags = ImageUsageFlagBits.SampledBit, ImageLayout usage = ImageLayout.ShaderReadOnlyOptimal)
        {
            if (filePath?.Length != 6)
            {
                throw new ArgumentException("File pathes array must contain exactly 6 textures");
            }
            Image[] images = new Image[6];
            try
            {
                for (int i = 0; i < filePath.Length; i++)
                {
                    images[i] = Adamantium.Imaging.Image.Load(filePath[i]);
                }
                return new Texture(device, images, flags, usage);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message + exception.StackTrace + exception.TargetSite);
            }
            finally
            {
                for (int i = 0; i < images.Length; i++)
                {
                    images[i]?.Dispose();
                }
            }

            throw new InvalidOperationException("Dimension not supported");
        }

        public void Save(string path, ImageFileType fileType)
        {

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
