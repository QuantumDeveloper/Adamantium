using AdamantiumVulkan.Core;
using System;
using Adamantium.Imaging.Dds;

namespace Adamantium.Imaging
{
    internal static class FormatConverter
    {
        internal static Format DXGIToVulkan(DXGIFormat format)
        {
            switch (format)
            {
                case DXGIFormat.BC1_UNorm:
                    return Format.BC1_RGB_UNORM_BLOCK;
                case DXGIFormat.BC2_UNorm:
                    return Format.BC2_UNORM_BLOCK;
                case DXGIFormat.BC3_UNorm:
                    return Format.BC3_UNORM_BLOCK;
                case DXGIFormat.BC4_UNorm:
                    return Format.BC4_UNORM_BLOCK;
                case DXGIFormat.BC4_SNorm:
                    return Format.BC4_SNORM_BLOCK;
                case DXGIFormat.BC5_UNorm:
                    return Format.BC5_UNORM_BLOCK;
                case DXGIFormat.BC5_SNorm:
                    return Format.BC5_SNORM_BLOCK;
                case DXGIFormat.B8G8R8A8_UNorm:
                    return Format.B8G8R8A8_UNORM;
                case DXGIFormat.R8G8B8A8_UNorm:
                    return Format.R8G8B8A8_UNORM;
                case DXGIFormat.R16G16_UNorm:
                    return Format.R16G16_UNORM;
                case DXGIFormat.R10G10B10A2_UNorm:
                    return Format.A2R10G10B10_UNORM_PACK32;
                case DXGIFormat.B5G6R5_UNorm:
                    return Format.B5G6R5_UNORM_PACK16;
                case DXGIFormat.B5G5R5A1_UNorm:
                    return Format.B5G5R5A1_UNORM_PACK16;
                case DXGIFormat.R8_UNorm:
                    return Format.R8_UNORM;
                case DXGIFormat.R16_UNorm:
                    return Format.R16_UNORM;
                case DXGIFormat.R16_Float:
                    return Format.R16_SFLOAT;
                case DXGIFormat.R16G16_Float:
                    return Format.R16G16_SFLOAT;
                case DXGIFormat.R8G8_UNorm:
                    return Format.R8G8_UNORM;
                case DXGIFormat.R16G16B16A16_UNorm:
                    return Format.R16G16B16A16_UNORM;
                case DXGIFormat.R16G16B16A16_SNorm:
                    return Format.R16G16B16A16_SNORM;
                case DXGIFormat.R16G16B16A16_Float:
                    return Format.R16G16B16A16_SFLOAT;
                case DXGIFormat.R32_Float:
                    return Format.R32_SFLOAT;
                case DXGIFormat.R32G32_Float:
                    return Format.R32G32_SFLOAT;
                case DXGIFormat.R32G32B32A32_Float:
                    return Format.R32G32B32A32_SFLOAT;
            }

            throw new ArgumentOutOfRangeException($"Cannot convert DXGI {format} to Vulkan format");
        }

        internal static DXGIFormat VulkanToDXGI(Format format)
        {
            switch (format)
            {
                case Format.BC1_RGB_UNORM_BLOCK:
                    return DXGIFormat.BC1_UNorm;
                case Format.BC2_UNORM_BLOCK:
                    return DXGIFormat.BC2_UNorm;
                case Format.BC3_UNORM_BLOCK:
                    return DXGIFormat.BC3_UNorm;
                case Format.BC4_UNORM_BLOCK:
                    return DXGIFormat.BC4_UNorm;
                case Format.BC4_SNORM_BLOCK:
                    return DXGIFormat.BC4_SNorm;
                case Format.BC5_UNORM_BLOCK:
                    return DXGIFormat.BC5_UNorm;
                case Format.BC5_SNORM_BLOCK:
                    return DXGIFormat.BC5_SNorm;
                case Format.B8G8R8A8_UNORM:
                    return DXGIFormat.B8G8R8A8_UNorm;
                case Format.R8G8B8A8_UNORM:
                    return DXGIFormat.R8G8B8A8_UNorm;
                case Format.R16G16_UNORM:
                    return DXGIFormat.R16G16_UNorm;
                case Format.A2R10G10B10_UNORM_PACK32:
                    return DXGIFormat.R10G10B10A2_UNorm;
                case Format.B5G6R5_UNORM_PACK16:
                    return DXGIFormat.B5G6R5_UNorm;
                case Format.B5G5R5A1_UNORM_PACK16:
                    return DXGIFormat.B5G5R5A1_UNorm;
                case Format.R8_UNORM:
                    return DXGIFormat.R8_UNorm;
                case Format.R16_UNORM:
                    return DXGIFormat.R16_UNorm;
                case Format.R16_SFLOAT:
                    return DXGIFormat.R16_Float;
                case Format.R16G16_SFLOAT:
                    return DXGIFormat.R16G16_Float;
                case Format.R8G8_UNORM:
                    return DXGIFormat.R8G8_UNorm;
                case Format.R16G16B16A16_UNORM:
                    return DXGIFormat.R16G16B16A16_UNorm;
                case Format.R16G16B16A16_SNORM:
                    return DXGIFormat.R16G16B16A16_SNorm;
                case Format.R16G16B16A16_SFLOAT:
                    return DXGIFormat.R16G16B16A16_Float;
                case Format.R32_SFLOAT:
                    return DXGIFormat.R32_Float;
                case Format.R32G32_SFLOAT:
                    return DXGIFormat.R32G32_Float;
                case Format.R32G32B32A32_SFLOAT:
                    return DXGIFormat.R32G32B32A32_Float;
            }

            throw new ArgumentOutOfRangeException($"Cannot convert Vulkan {format} to DXGI format");
        }
    }
}
