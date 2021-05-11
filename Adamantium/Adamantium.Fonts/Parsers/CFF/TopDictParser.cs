using System.Collections.Generic;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    public class TopDictParser : DictOperandParser
    {
        public TopDictParser(byte[] rawData) : base(rawData)
        {
        }

        protected override void FillDefaultValues()
        {
            var isFixedPitchOperand = new List<GenericOperandResult>();
            isFixedPitchOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.isFixedPitch] = isFixedPitchOperand;
            
            var italicAngleOperand = new List<GenericOperandResult>();
            italicAngleOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.ItalicAngle] = italicAngleOperand;
            
            var underlinePositionOperand = new List<GenericOperandResult>();
            underlinePositionOperand.Add(-100);
            OperatorsRawValues[DictOperatorsType.UnderlinePosition] = underlinePositionOperand;
            
            var underlineThicknessOperand = new List<GenericOperandResult>();
            underlineThicknessOperand.Add(50);
            OperatorsRawValues[DictOperatorsType.UnderlineThickness] = underlineThicknessOperand;
            
            var paintTypeOperand = new List<GenericOperandResult>();
            paintTypeOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.PaintType] = paintTypeOperand;
            
            var charStringTypeOperand = new List<GenericOperandResult>();
            charStringTypeOperand.Add(2);
            OperatorsRawValues[DictOperatorsType.CharStringType] = charStringTypeOperand;
            
            var fontMatrixOperand = new List<GenericOperandResult>();
            fontMatrixOperand.Add(0.001);
            fontMatrixOperand.Add(0);
            fontMatrixOperand.Add(0);
            fontMatrixOperand.Add(0.001);
            fontMatrixOperand.Add(0);
            fontMatrixOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.FontMatrix] = fontMatrixOperand;
            
            var fontBBoxOperand = new List<GenericOperandResult>();
            fontBBoxOperand.Add(0);
            fontBBoxOperand.Add(0);
            fontBBoxOperand.Add(0);
            fontBBoxOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.FontBBox] = fontBBoxOperand;
            
            var charsetOperand = new List<GenericOperandResult>();
            charsetOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.charset] = charsetOperand;
            
            var encodingOperand = new List<GenericOperandResult>();
            encodingOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.Encoding] = encodingOperand;
            
            var privateOperand = new List<GenericOperandResult>();
            privateOperand.Add(0);
            privateOperand.Add(0);
            OperatorsRawValues[DictOperatorsType.Private] = privateOperand;
        }

        
    }
}
