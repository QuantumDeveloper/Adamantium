using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class Command
    {
        public OperatorsType Operator;
        public List<CommandOperand> Operands;
        public List<CommandOperand> BlendedOperands;
        public bool IsBlendPresent { get; set; }

        public void ApplyBlend(VariationRegionList regionList, float[] variationPoint)
        {
            BlendedOperands = Operands;
            
            if (!IsBlendPresent ||
                regionList == null ||
                variationPoint == null ||
                variationPoint.Length < regionList.AxisCount)
            {
                return;
            }

            foreach (var blendedOperand in BlendedOperands)
            {
                var blendData = blendedOperand.BlendData;
                if (blendData == null || blendData.Data.Count == 0)
                {
                    continue;
                }

                double netAdjustment = 0;

                for (var r = 0; r < blendData.Data.Count; ++r)
                {
                    var region = regionList.VariationRegions[blendData.RegionIndices[r]];
                    double overallScalar = 1;

                    for (var a = 0; a < regionList.AxisCount; a++)
                    {
                        double perAxisScalar = 0;
                        
                        var startCoord = region.RegionAxes[a].StartCoord;
                        var peakCoord = region.RegionAxes[a].PeakCoord;
                        var endCoord = region.RegionAxes[a].EndCoord;

                        if (startCoord > peakCoord ||
                            peakCoord > endCoord)
                        {
                            perAxisScalar = 1;
                        }
                        else if (startCoord < 0 && endCoord > 0 &&
                                 peakCoord != 0)
                        {
                            perAxisScalar = 1;
                        }
                        else if (peakCoord == 0)
                        {
                            perAxisScalar = 1;
                        }
                        else if (variationPoint[a] < startCoord
                                 || variationPoint[a] > endCoord)
                        {
                            perAxisScalar = 0;
                        }
                        else
                        {
                            if (variationPoint[a] == peakCoord)
                            {
                                perAxisScalar = 1;
                            }
                            else if (variationPoint[a] < peakCoord)
                            {
                                perAxisScalar = (variationPoint[a] - startCoord) / (peakCoord - startCoord);
                            }
                            else
                            {
                                perAxisScalar = (endCoord - variationPoint[a]) / (endCoord - peakCoord);
                            }
                        }
                        
                        overallScalar *= perAxisScalar;
                    }
                    
                    netAdjustment += overallScalar * blendData.Data[r];
                }

                blendedOperand.Value += netAdjustment;
            }
        }
        
        internal bool IsNewOutline()
        {
            switch (Operator)
            {
                case OperatorsType.rmoveto:
                case OperatorsType.hmoveto:
                case OperatorsType.vmoveto:
                    return true;
                default:
                    return false;
            }
        }
        
        public override string ToString()
        {
            return $"IsBlendPresent: {IsBlendPresent}; {Operator} {string.Join(" , ", Operands)}";
        }
    }
}
