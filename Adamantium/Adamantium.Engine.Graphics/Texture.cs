using System;
using System.Collections.Generic;
using System.Text;
using Adamantium.Imaging;
using Adamantium.Core;
using System.IO;
using Adamantium.Win32;
using VulkanImage = AdamantiumVulkan.Core.Image;
using AdamantiumVulkan.Core;
using Image = Adamantium.Imaging.Image;

namespace Adamantium.Engine.Graphics
{
    public class Texture : DisposableBase
    {
        private VulkanImage image;
        private DeviceMemory imageMemory;
        private TextureDescription description;

        public uint Width => description.Width;

        public uint Height => description.Height;

        public SurfaceFormat SurfaceFormat => description.Format;

        public ImageLayout ImageLayout { get; private set; }

        public IntPtr NativePointer { get; }

        public GraphicsDevice GraphicsDevice { get; private set; }

        protected VulkanImage Image { get => image; set => image = value; }
        protected DeviceMemory ImageMemory { get => imageMemory; set => imageMemory = value; }
        protected ImageView ImageView { get; set; }

        protected Texture(GraphicsDevice device, TextureDescription description)
        {
            GraphicsDevice = device;
            this.description = description;
            Initialize();
            TransitionImageLayout(description.DesiredImageLayout);
        }

        protected Texture(GraphicsDevice device, Image img, ImageUsageFlagBits usage, ImageLayout desiredLayout)
        {
            GraphicsDevice = device;

        }

        protected Texture(GraphicsDevice device, Image[] img, ImageUsageFlagBits usage, ImageLayout desiredLayout)
        {
            GraphicsDevice = device;

        }

        protected void Initialize()
        {
            CreateImage(description);
            CreateImageView(description);
        }

        protected void CreateImage(TextureDescription description)
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
                    GraphicsDevice.VulkanInstance.CurrentDevice.FindCorrespondingMemoryType(memRequirements.MemoryTypeBits,
                        MemoryPropertyFlagBits.DeviceLocalBit)
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
            createInfo.Image = Image;
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

        public void TransitionImageLayout(ImageLayout newLayout)
        {
            var commandBuffer = GraphicsDevice.BeginSingleTimeCommands();

            var barrier = new ImageMemoryBarrier
            {
                OldLayout = ImageLayout,
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

            if ((ImageLayout == ImageLayout.Undefined || ImageLayout == ImageLayout.Preinitialized) &&
                newLayout == ImageLayout.TransferDstOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = (uint)AccessFlagBits.TransferWriteBit;

                sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                destinationStage = PipelineStageFlagBits.TransferBit;
            }
            else if (ImageLayout == ImageLayout.TransferSrcOptimal && newLayout == ImageLayout.ShaderReadOnlyOptimal)
            {
                barrier.SrcAccessMask = (uint)AccessFlagBits.TransferWriteBit;
                barrier.DstAccessMask = (uint) AccessFlagBits.ShaderReadBit;

                sourceStage = PipelineStageFlagBits.TransferBit;
                destinationStage = PipelineStageFlagBits.FragmentShaderBit;
            }
            else if (ImageLayout == ImageLayout.Undefined && newLayout == ImageLayout.DepthStencilAttachmentOptimal)
            {
                barrier.SrcAccessMask = 0;
                barrier.DstAccessMask = (uint)(AccessFlagBits.DepthStencilAttachmentReadBit |
                                        AccessFlagBits.DepthStencilAttachmentWriteBit);

                sourceStage = PipelineStageFlagBits.TopOfPipeBit;
                destinationStage = PipelineStageFlagBits.EarlyFragmentTestsBit;
            }
            else
            {
                throw new ImageLayoutTransitionException(
                    $"Transition from {ImageLayout} to {newLayout} is not supported");
            }

            commandBuffer.CmdPipelineBarrier(
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
            return texture.Image;
        }

        public static implicit operator ImageView(Texture texture)
        {
            return texture.ImageView;
        }

        protected override void Dispose(bool disposeManaged)
        {
            Console.WriteLine("Called Dispose on Texture");
            Image?.Destroy(GraphicsDevice);
            ImageView?.Destroy(GraphicsDevice);
            ImageMemory?.FreeMemory(GraphicsDevice);
        }
    }
}
