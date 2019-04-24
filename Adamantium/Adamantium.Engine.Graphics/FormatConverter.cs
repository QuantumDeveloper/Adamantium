using AdamantiumVulkan.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace Adamantium.Engine.Graphics
{
    public static class FormatConverter
    {
        public static Format DXGIToVulkan(DXGIFormat format)
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
    }
}
