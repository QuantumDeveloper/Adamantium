using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Adamantium.Fonts.OTF
{
    public class GenericOperandResultSet
    {
        public IReadOnlyDictionary<DictOperatorsType, GenericOperandResult> Results { get; }

        public GenericOperandResultSet(IDictionary<DictOperatorsType, GenericOperandResult> results)
        {
            Results = new ReadOnlyDictionary<DictOperatorsType, GenericOperandResult>(results);
        }
    }
}