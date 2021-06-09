using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class PrivateDictParser : DictOperandParser
    {
        public PrivateDictParser(byte[] rawData, CFFFont font) : base(rawData, font)
        {
        }

        protected override void FillDefaultValues()
        {
            var blueScaleOperand = new List<GenericOperandResult>();
            blueScaleOperand.Add(0.039625);
            OperatorsRawValues[DictOperatorsType.BlueScale] = blueScaleOperand;
            
            var blueShiftOperand = new List<GenericOperandResult>();
            blueShiftOperand.Add(7);
            OperatorsRawValues[DictOperatorsType.BlueShift] = blueShiftOperand;
            
            var blueFuzzOperand = new List<GenericOperandResult>();
            blueFuzzOperand.Add(1);
            OperatorsRawValues[DictOperatorsType.BlueFuzz] = blueFuzzOperand;
            
            var forceBoldOperand = new List<GenericOperandResult>();
            forceBoldOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.ForceBold] = forceBoldOperand;
            
            var languageGroupOperand = new List<GenericOperandResult>();
            languageGroupOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.LanguageGroup] = languageGroupOperand;
            
            var expansionFactorOperand = new List<GenericOperandResult>();
            expansionFactorOperand.Add(0.06);
            OperatorsRawValues[DictOperatorsType.ExpansionFactor] = expansionFactorOperand;
            
            var initialRandomSeedOperand = new List<GenericOperandResult>();
            initialRandomSeedOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.InitialRandomSeed] = initialRandomSeedOperand;
            
            var defaultWidthXOperand = new List<GenericOperandResult>();
            defaultWidthXOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.DefaultWidthX] = defaultWidthXOperand;
            
            var nominalWidthXOperand = new List<GenericOperandResult>();
            nominalWidthXOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.NominalWidthX] = nominalWidthXOperand;
        }
    }
}
