using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public class PrivateDictParser : DictOperandParser
    {
        private Dictionary<ushort, PrivateDictOperatorsType> byteToOperatorMap;
        private Dictionary<PrivateDictOperatorsType, Func<List<GenericOperandResult>, GenericOperandResult>> operatorsActions;
        private Dictionary<PrivateDictOperatorsType, List<GenericOperandResult>> operatorsRawValues;

        public PrivateDictParser(byte[] rawData)
        {
            operatorsRawValues = new Dictionary<PrivateDictOperatorsType, List<GenericOperandResult>>();

            byteToOperatorMap = new Dictionary<ushort, PrivateDictOperatorsType>
            {
                { (ushort)PrivateDictOperatorsType.BlueValues          , PrivateDictOperatorsType.BlueValues          },
                { (ushort)PrivateDictOperatorsType.OtherBlues          , PrivateDictOperatorsType.OtherBlues          },
                { (ushort)PrivateDictOperatorsType.FamilyBlues         , PrivateDictOperatorsType.FamilyBlues         },
                { (ushort)PrivateDictOperatorsType.FamilyOtherBlues    , PrivateDictOperatorsType.FamilyOtherBlues    },
                { (ushort)PrivateDictOperatorsType.BlueScale           , PrivateDictOperatorsType.BlueScale           },
                { (ushort)PrivateDictOperatorsType.BlueShift           , PrivateDictOperatorsType.BlueShift           },
                { (ushort)PrivateDictOperatorsType.BlueFuzz            , PrivateDictOperatorsType.BlueFuzz            },
                { (ushort)PrivateDictOperatorsType.StdHW               , PrivateDictOperatorsType.StdHW               },
                { (ushort)PrivateDictOperatorsType.StdVW               , PrivateDictOperatorsType.StdVW               },
                { (ushort)PrivateDictOperatorsType.StemSnapH           , PrivateDictOperatorsType.StemSnapH           },
                { (ushort)PrivateDictOperatorsType.StemSnapV           , PrivateDictOperatorsType.StemSnapV           },
                { (ushort)PrivateDictOperatorsType.ForceBold           , PrivateDictOperatorsType.ForceBold           },
                { (ushort)PrivateDictOperatorsType.LanguageGroupe      , PrivateDictOperatorsType.LanguageGroupe      },
                { (ushort)PrivateDictOperatorsType.ExpansionFactor     , PrivateDictOperatorsType.ExpansionFactor     },
                { (ushort)PrivateDictOperatorsType.InitialRandomSeed   , PrivateDictOperatorsType.InitialRandomSeed   },
                { (ushort)PrivateDictOperatorsType.Subrs               , PrivateDictOperatorsType.Subrs               },
                { (ushort)PrivateDictOperatorsType.DefaultWidthX       , PrivateDictOperatorsType.DefaultWidthX       },
                { (ushort)PrivateDictOperatorsType.NominalWidthX       , PrivateDictOperatorsType.NominalWidthX       }
            };

            operatorsActions = new Dictionary<PrivateDictOperatorsType, Func<List<GenericOperandResult>, GenericOperandResult>>
            {
               { PrivateDictOperatorsType.BlueValues            , Delta },
               { PrivateDictOperatorsType.OtherBlues            , Delta },
               { PrivateDictOperatorsType.FamilyBlues           , Delta },
               { PrivateDictOperatorsType.FamilyOtherBlues      , Delta },
               { PrivateDictOperatorsType.BlueScale             , Number },
               { PrivateDictOperatorsType.BlueShift             , Number },
               { PrivateDictOperatorsType.BlueFuzz              , Number },
               { PrivateDictOperatorsType.StdHW                 , Number },
               { PrivateDictOperatorsType.StdVW                 , Number },
               { PrivateDictOperatorsType.StemSnapH             , Delta },
               { PrivateDictOperatorsType.StemSnapV             , Delta },
               { PrivateDictOperatorsType.ForceBold             , Boolean },
               { PrivateDictOperatorsType.LanguageGroupe        , Number },
               { PrivateDictOperatorsType.ExpansionFactor       , Number },
               { PrivateDictOperatorsType.InitialRandomSeed     , Number },
               { PrivateDictOperatorsType.Subrs                 , Number },
               { PrivateDictOperatorsType.DefaultWidthX         , Number },
               { PrivateDictOperatorsType.NominalWidthX         , Number }
            };

            FillOperatorsRawValues(rawData);
        }

        public GenericOperandResult GetOperatorValue(PrivateDictOperatorsType opType)
        {
            return operatorsActions[opType].Invoke(operatorsRawValues[opType]);
        }

        private void FillOperatorsRawValues(byte[] rawData)
        {
            List<byte> byteArray = new List<byte>(rawData);
            List<GenericOperandResult> rawOperands = new List<GenericOperandResult>();
            ushort token;

            while (byteArray.Count > 0)
            {
                token = byteArray[0];

                if (token == 12)
                {
                    token = (ushort)((12 << 8) | byteArray[1]);

                    // remove additional byte beside from generic token case byte removal (see below)
                    GetFirstByteAndRemove(byteArray);
                }

                if (byteToOperatorMap.ContainsKey(token))
                {
                    // this is token - remove this byte from byte stream
                    GetFirstByteAndRemove(byteArray);

                    operatorsRawValues[(PrivateDictOperatorsType)token] = rawOperands;
                    rawOperands = new List<GenericOperandResult>();
                }
                else
                {
                    rawOperands.Add(Number(byteArray));
                }
            }
        }
    }
}
