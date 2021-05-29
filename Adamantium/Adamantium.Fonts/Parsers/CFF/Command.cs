using System.Collections.Generic;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class Command
    {
        public OperatorsType Operator;
        public List<CommandOperand> Operands;
        public bool IsBlendPresent { get; set; }

        public void ApplyBlend(/*point on variation grid*/)
        {
            // @TODO Modify operands 
        }
        
        public override string ToString()
        {
            return $"IsBlendPresent: {IsBlendPresent}; {Operator} {string.Join(" , ", Operands)}";
        }
    }
}
