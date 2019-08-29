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
        public uint Width { get; set; }

        public uint Height { get; set; }

        public SurfaceFormat Format { get; set; }

        public IntPtr NativePointer { get; }

        public GraphicsDevice GraphicsDevice { get; private set; }

        protected VulkanImage Image { get; set; }
        protected DeviceMemory ImageMemory { get; set; }
        protected ImageView ImageView { get; set; }

        protected Texture(GraphicsDevice device, TextureDescription description)
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
        }

        protected void CreateImageandView()
        {
            var device = (Device) GraphicsDevice;
            if (device.CreateImage(imageInfo, null, out image) != Result.Success)
            {
                throw new Exception("failed to create image!");
            }

            device.GetImageMemoryRequirements(image, out var memRequirements);

            MemoryAllocateInfo allocInfo = new MemoryAllocateInfo();
            allocInfo.AllocationSize = memRequirements.Size;
            allocInfo.MemoryTypeIndex = FindMemoryType(memRequirements.MemoryTypeBits, properties);

            if (device.AllocateMemory(allocInfo, null, out imageMemory) != Result.Success)
            {
                throw new Exception("failed to allocate image memory!");
            }

            device.BindImageMemory(image, imageMemory, 0);
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
                switch (description.Dimension)
                {
                    case TextureDimension.Texture1D:
                        return RenderTarget1D.New(graphicsDevice, description);
                    case TextureDimension.Texture2D:
                        return RenderTarget2D.New(graphicsDevice, description);
                    case TextureDimension.Texture3D:
                        return RenderTarget3D.New(graphicsDevice, description);
                    case TextureDimension.TextureCube:
                        return RenderTargetCube.New(graphicsDevice, description);
                }
            }
            else if (description.Usage.HasFlag(ImageUsageFlagBits.DepthStencilAttachmentBit))
            {
                return new DepthBuffer(graphicsDevice, description);
            }
            else
            {
                return new Texture(graphicsDevice, description);
                //switch (description.Dimension)
                //{

                //    case TextureDimension.Texture1D:
                //        return Texture1D.New(graphicsDevice, description);
                //    case TextureDimension.Texture2D:
                //        return Texture2D.New(graphicsDevice, description);
                //    case TextureDimension.Texture3D:
                //        return Texture3D.New(graphicsDevice, description);
                //    case TextureDimension.TextureCube:
                //        return TextureCube.New(graphicsDevice, description);
                //}
            }
            return null;
        }

        private static TextureDimension FindCorrespondingDimension(TextureDimension dimension)
        {
            switch (dimension)
            {
                case TextureDimension.Texture1D:
                    return TextureDimension.Texture1D;
                case TextureDimension.Texture2D:
                    return TextureDimension.Texture2D;
                case TextureDimension.Texture3D:
                    return TextureDimension.Texture3D;
                case TextureDimension.TextureCube:
                    return TextureDimension.TextureCube;
            }
            return TextureDimension.Undefined;
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

            var dimension = FindCorrespondingDimension(image.Description.Dimension);

            switch (dimension)
            {
                case TextureDimension.Texture1D:
                    return Texture1D.New(device, image, flags, usage);
                case TextureDimension.Texture2D:
                    return Texture2D.New(device, image, flags, usage);
                case TextureDimension.Texture3D:
                    return Texture3D.New(device, image, flags, usage);
                case TextureDimension.TextureCube:
                    return TextureCube.New(device, image, flags, usage);
                default:
                    throw new InvalidOperationException("Dimension not supported");
            }
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
                    images[i] = Adamantium.Imaging.Image .Load(filePath[i]);
                }
                return TextureCube.New(device, images, flags, usage);
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

        protected override void Dispose(bool disposeManaged)
        {

        }
    }
}
