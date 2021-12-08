using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Adamantium.Fonts.Tables.CFF
{
    internal class GenericOperandResultSet
    {
        public IReadOnlyDictionary<DictOperatorsType, GenericOperandResult> Results { get; }

        public GenericOperandResultSet(IDictionary<DictOperatorsType, GenericOperandResult> results)
        {
            Results = new ReadOnlyDictionary<DictOperatorsType, GenericOperandResult>(results);
        }
    }
}