using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public struct Command
    {
        public OperatorsType @operator;
        public List<double> operands;

        public override string ToString()
        {
            return $"{@operator} {string.Join(" , ", operands.ToArray())}";
        }
    }
}
