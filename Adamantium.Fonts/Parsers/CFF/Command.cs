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
                if (blendedOperand.BlendData.Data.Count == 0)
                {
                    continue;
                }

                double netAdjustment = 0; /* initialize the accumulated adjustment to zero */

                for (var r = 0; r < regionList.VariationRegions.Length; ++r) /* For each region, calculate a scalar */
                {
                    double overallScalar = 1; /* initialize the overall scalar for the region to one */

                    /* for each axis, calculate a per-axis scalar */
                    for (var a = 0; a < regionList.AxisCount; a++)
                    {
                        double perAxisScalar = 0;
                        
                        var startCoord = regionList.VariationRegions[r].RegionAxes[a].StartCoord;
                        var peakCoord = regionList.VariationRegions[r].RegionAxes[a].PeakCoord;
                        var endCoord = regionList.VariationRegions[r].RegionAxes[a].EndCoord;
                        // An axis is ignored when the region is invalid for it: the peak must lie between start and end,
                        // and those may differ in sign only when the peak is zero.

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
                        /* Note: for remaining cases, start, peak and end will all be <= 0 or
                        will all be >= 0, or else peak will be == 0. */
                        /* If the peak is zero for some axis, then ignore the axis. */
                        else if (peakCoord == 0)
                        {
                            perAxisScalar = 1;
                        }
                        /* If the instance coordinate is out of range for some axis, then the
                        region and its associated deltas are not applicable. */
                        else if (variationPoint[a] < startCoord
                                 || variationPoint[a] > endCoord)
                        {
                            perAxisScalar = 0;
                        }
                        /* The region is applicable: calculate a per-axis scalar as a proportion
                        of the proximity of the instance to the peak within the region. */
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
                            else /* variationPoint[i] > region.RegionAxes[i].PeakCoord */
                            {
                                perAxisScalar = (endCoord - variationPoint[a]) / (endCoord - peakCoord);
                            }
                        }
                        
                        /* The overall scalar is the product of all per-axis scalars.
                        Note: the axis scalar and the overall scalar will always be
                        >= 0 and <= 1. */
                        overallScalar *= perAxisScalar;
                    } /* per-axis loop */
                    
                    /* get the scaled delta for this region */
                    var scaledDelta = overallScalar * blendedOperand.BlendData.Data[r];

                    /* accumulate the adjustments from each region */
                    netAdjustment += scaledDelta;
                } /* per-region loop */

                /* apply the accumulated adjustment to the default to derive the interpolated value */
                blendedOperand.Value += netAdjustment;
            } /* per-operand loop */
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
