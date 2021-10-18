using System;
using System.Collections.Generic;
using Adamantium.Mathematics;

namespace Adamantium.Fonts
{
    public class SubpixelRasterizer
    {
        public Color[] ProcessGlyphSubpixelSampling(bool[,] sampledData)
        {
            var width = sampledData.GetLength(0);
            var height = sampledData.GetLength(1);
            
            var subpixels = new byte[width, height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    subpixels[x, y] = (sampledData[x, y] ? (byte)0 : (byte)255);
                }
            }

            var rebalancedSubpixels = RebalanceEnergy(subpixels);

            return ConvertToPixels(rebalancedSubpixels);
        }

        private List<byte> RebalanceEnergy(byte[,] subpixels)
        {
            var width = subpixels.GetLength(0);
            var height = subpixels.GetLength(1);

            var rebalancedSubpixels = new List<byte>();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    byte mostLeftNeighborEnergy = 255;
                    byte leastLeftNeighborEnergy = 255;
                    byte currentSubpixelEnergy = subpixels[x, y];
                    byte leastRightNeighborEnergy = 255;
                    byte mostRightNeighborEnergy = 255;

                    if (x > 0)
                    {
                        leastLeftNeighborEnergy = subpixels[x - 1, y];
                    }
                    
                    if (x > 1)
                    {
                        mostLeftNeighborEnergy = subpixels[x - 2, y];
                    }

                    if (x < (width - 2))
                    {
                        mostRightNeighborEnergy = subpixels[x + 2, y];
                    }
                    
                    if (x < (width - 1))
                    {
                        leastRightNeighborEnergy = subpixels[x + 1, y];
                    }

                    var rebalancedSubpixelEnergy = (mostLeftNeighborEnergy / 9.0) + ((leastLeftNeighborEnergy * 2.0) / 9.0) + ((currentSubpixelEnergy * 3.0) / 9.0) + ((leastRightNeighborEnergy * 2.0) / 9.0) + (mostRightNeighborEnergy / 9.0);

                    rebalancedSubpixels.Add((byte)rebalancedSubpixelEnergy);
                }
            }
            
            return rebalancedSubpixels;
        }

        private Color[] ConvertToPixels(List<byte> subpixels)
        {
            var pixels = new List<Color>();
            
            for (var i = 2; i < subpixels.Count; i+=3)
            {
                byte red = subpixels[i - 2];
                byte green = subpixels[i - 1];
                byte blue = subpixels[i];
                byte alpha = (byte)(red == 255 && green == 255 && blue == 255 ? 0 : 255);

                pixels.Add(Color.FromRgba(red, green, blue, alpha));
            }

            return pixels.ToArray();
        }
    }
}