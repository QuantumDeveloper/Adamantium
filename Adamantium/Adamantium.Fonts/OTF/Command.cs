using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public struct Command
    {
        public OperatorsType @operator;
        public OperatorsType? additionalOperator;
        public List<double> operands;

        public override string ToString()
        {
            return $"{additionalOperator} {@operator} {string.Join(" , ", operands.ToArray())}";
        }
    }
}
