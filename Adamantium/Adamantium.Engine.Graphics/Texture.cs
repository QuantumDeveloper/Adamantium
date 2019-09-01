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

        public uint Width { get; set; }

        public uint Height { get; set; }

        public SurfaceFormat Format => description.Format;

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
        }

        protected Texture(GraphicsDevice device, Image img, ImageUsageFlagBits flags, ImageLayout usage)
        {
            GraphicsDevice = device;

        }

        protected Texture(GraphicsDevice device, Image[] img, ImageUsageFlagBits flags, ImageLayout usage)
        {
            GraphicsDevice = device;

        }

        private ImageCreateInfo TextureDescriptionToImageInfo(TextureDescription description)
        {
            ImageCreateInfo imageInfo = new ImageCreateInfo();
            imageInfo.ImageType = description.ImageType;
            imageInfo.Extent = new Extent3D();
            imageInfo.Extent.Width = description.Width;
            imageInfo.Extent.Height = description.Height;
            imageInfo.Extent.Depth = description.Depth;
            imageInfo.MipLevels = description.MipLevels;
            imageInfo.ArrayLayers = description.ArrayLayers;
            imageInfo.Format = description.Format;
            imageInfo.Tiling = description.ImageTiling;
            imageInfo.InitialLayout = description.ImageLayout;
            imageInfo.Usage = (uint)description.Usage;
            imageInfo.Samples = description.Samples;
            imageInfo.SharingMode = description.SharingMode;

            return imageInfo;
        }

        protected void Initialize()
        {
            CreateImage(description);
            CreateImageView();
        }

        protected void CreateImage(TextureDescription description)
        {
            var device = (Device)GraphicsDevice;
            var imageInfo = TextureDescriptionToImageInfo(description);
            if (device.CreateImage(imageInfo, null, out image) != Result.Success)
            {
                throw new Exception("failed to create image!");
            }

            device.GetImageMemoryRequirements(image, out var memRequirements);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo();
            allocInfo.AllocationSize = memRequirements.Size;
            allocInfo.MemoryTypeIndex = GraphicsDevice.Instance.CurrentDevice.FindCorrespondingMemoryType(memRequirements.MemoryTypeBits, MemoryPropertyFlagBits.DeviceLocalBit);

            if (device.AllocateMemory(allocInfo, null, out imageMemory) != Result.Success)
            {
                throw new Exception("failed to allocate image memory!");
            }

            device.BindImageMemory(image, imageMemory, 0);
        }

        protected void CreateImageView()
        {
            var createInfo = new ImageViewCreateInfo();
            createInfo.Image = Image;
            createInfo.ViewType = ImageViewType._2d;
            createInfo.Format = Format;
            ComponentMapping componentMapping = new ComponentMapping();
            componentMapping.R = ComponentSwizzle.Identity;
            componentMapping.G = ComponentSwizzle.Identity;
            componentMapping.B = ComponentSwizzle.Identity;
            componentMapping.A = ComponentSwizzle.Identity;
            createInfo.Components = componentMapping;
            ImageSubresourceRange subresourceRange = new ImageSubresourceRange();
            subresourceRange.AspectMask = (uint)ImageAspectFlagBits.ColorBit;
            subresourceRange.BaseMipLevel = 0;
            subresourceRange.LevelCount = 1;
            subresourceRange.BaseArrayLayer = 0;
            subresourceRange.LayerCount = 1;
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
                return new DepthBuffer(graphicsDevice, description);
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
            Image?.Destroy(GraphicsDevice);
            ImageView?.Destroy(GraphicsDevice);
            ImageMemory?.FreeMemory(GraphicsDevice);
        }
    }
}
