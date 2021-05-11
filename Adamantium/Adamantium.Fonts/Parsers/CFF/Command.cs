using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
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
