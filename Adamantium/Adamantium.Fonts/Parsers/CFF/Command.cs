using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class Command
    {
        public OperatorsType Operator;
        public List<double> Operands;
        // if 2 blend operators were consecutive, then we must apply first blend deltas on first operator set
        // and then pass these modified operators to second blend
        public List<BlendData> BlendData;

        public bool IsBlendPresent => BlendData.Count > 0;

        public Command()
        {
            BlendData = new List<BlendData>();
        }

        public void ApplyBlend(/*point on variation grid*/)
        {
            // @TODO Modify operands 
        }
        
        public override string ToString()
        {
            return $"IsBlendPresent: {IsBlendPresent}; {Operator} {string.Join(" , ", Operands.ToArray())}";
        }
    }
}
