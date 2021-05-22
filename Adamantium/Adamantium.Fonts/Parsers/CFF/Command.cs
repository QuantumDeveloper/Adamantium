using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal struct Command
    {
        public OperatorsType Operator;
        public List<double> Operands;
        
        // Outer list is operand list, inner list - deltas for current operand for each region
        /*
            Regular: 100 200 rmoveto
            Light: 100 150 rmoveto
            Bold: 100 300 rmoveto
            Condensed: 50 100 rmoveto
            
            (100 200) (0 0 -50) (-50 100 -100) 2 blend rmoveto
            
            https://docs.microsoft.com/en-us/typography/opentype/spec/cff2charstr#section4.5
         */
        public List<List<double>> BlendDeltas;

        public bool IsBlendPresent => BlendDeltas != null;

        public override string ToString()
        {
            return $"IsBlendPresent: {IsBlendPresent}; {Operator} {string.Join(" , ", Operands.ToArray())}";
        }
    }
}
