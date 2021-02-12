using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.OTF
{
    public class TopDictParser : DictOperandParser
    {
        private Dictionary<ushort, TopDictOperatorsType> byteToOperatorMap;
        private Dictionary<TopDictOperatorsType, Func<List<GenericOperandResult>, GenericOperandResult>> operatorsActions;
        private Dictionary<TopDictOperatorsType, List<GenericOperandResult>> operatorsRawValues;

        public TopDictParser(byte[] rawData)
        {
            operatorsRawValues = new Dictionary<TopDictOperatorsType, List<GenericOperandResult>>();

            byteToOperatorMap = new Dictionary<ushort, TopDictOperatorsType>
            {
                { (ushort)TopDictOperatorsType.version              , TopDictOperatorsType.version              },
                { (ushort)TopDictOperatorsType.Notice               , TopDictOperatorsType.Notice               },
                { (ushort)TopDictOperatorsType.Copyright            , TopDictOperatorsType.Copyright            },
                { (ushort)TopDictOperatorsType.FullName             , TopDictOperatorsType.FullName             },
                { (ushort)TopDictOperatorsType.FamilyName           , TopDictOperatorsType.FamilyName           },
                { (ushort)TopDictOperatorsType.Weight               , TopDictOperatorsType.Weight               },
                { (ushort)TopDictOperatorsType.isFixedPitch         , TopDictOperatorsType.isFixedPitch         },
                { (ushort)TopDictOperatorsType.ItalicAngle          , TopDictOperatorsType.ItalicAngle          },
                { (ushort)TopDictOperatorsType.UnderlinePosition    , TopDictOperatorsType.UnderlinePosition    },
                { (ushort)TopDictOperatorsType.UnderlineThickness   , TopDictOperatorsType.UnderlineThickness   },
                { (ushort)TopDictOperatorsType.PaintType            , TopDictOperatorsType.PaintType            },
                { (ushort)TopDictOperatorsType.CharStringType       , TopDictOperatorsType.CharStringType       },
                { (ushort)TopDictOperatorsType.FontMatrix           , TopDictOperatorsType.FontMatrix           },
                { (ushort)TopDictOperatorsType.UniqueID             , TopDictOperatorsType.UniqueID             },
                { (ushort)TopDictOperatorsType.FontBBox             , TopDictOperatorsType.FontBBox             },
                { (ushort)TopDictOperatorsType.StrokeWidth          , TopDictOperatorsType.StrokeWidth          },
                { (ushort)TopDictOperatorsType.XUID                 , TopDictOperatorsType.XUID                 },
                { (ushort)TopDictOperatorsType.charset              , TopDictOperatorsType.charset              },
                { (ushort)TopDictOperatorsType.Encoding             , TopDictOperatorsType.Encoding             },
                { (ushort)TopDictOperatorsType.CharStrings          , TopDictOperatorsType.CharStrings          },
                { (ushort)TopDictOperatorsType.Private              , TopDictOperatorsType.Private              },
                { (ushort)TopDictOperatorsType.SyntheticBase        , TopDictOperatorsType.SyntheticBase        },
                { (ushort)TopDictOperatorsType.PostScript           , TopDictOperatorsType.PostScript           },
                { (ushort)TopDictOperatorsType.BaseFontName         , TopDictOperatorsType.BaseFontName         },
                { (ushort)TopDictOperatorsType.BaseFontBlend        , TopDictOperatorsType.BaseFontBlend        },
                { (ushort)TopDictOperatorsType.ROS                  , TopDictOperatorsType.ROS                  },
                { (ushort)TopDictOperatorsType.CIDFontVersion       , TopDictOperatorsType.CIDFontVersion       },
                { (ushort)TopDictOperatorsType.CIDFontRevision      , TopDictOperatorsType.CIDFontRevision      },
                { (ushort)TopDictOperatorsType.CIDFontType          , TopDictOperatorsType.CIDFontType          },
                { (ushort)TopDictOperatorsType.CIDCount             , TopDictOperatorsType.CIDCount             },
                { (ushort)TopDictOperatorsType.UIDBase              , TopDictOperatorsType.UIDBase              },
                { (ushort)TopDictOperatorsType.FDArray              , TopDictOperatorsType.FDArray              },
                { (ushort)TopDictOperatorsType.FDSelect             , TopDictOperatorsType.FDSelect             },
                { (ushort)TopDictOperatorsType.FontName             , TopDictOperatorsType.FontName             }
            };

            operatorsActions = new Dictionary<TopDictOperatorsType, Func<List<GenericOperandResult>, GenericOperandResult>>
            {
               { TopDictOperatorsType.version                , Sid },
               { TopDictOperatorsType.Notice                 , Sid },
               { TopDictOperatorsType.Copyright              , Sid },
               { TopDictOperatorsType.FullName               , Sid },
               { TopDictOperatorsType.FamilyName             , Sid },
               { TopDictOperatorsType.Weight                 , Sid },
               { TopDictOperatorsType.isFixedPitch           , Boolean },
               { TopDictOperatorsType.ItalicAngle            , Number },
               { TopDictOperatorsType.UnderlinePosition      , Number },
               { TopDictOperatorsType.UnderlineThickness     , Number },
               { TopDictOperatorsType.PaintType              , Number },
               { TopDictOperatorsType.CharStringType         , Number },
               { TopDictOperatorsType.FontMatrix             , NumberArray },
               { TopDictOperatorsType.UniqueID               , Number },
               { TopDictOperatorsType.FontBBox               , NumberArray },
               { TopDictOperatorsType.StrokeWidth            , Number },
               { TopDictOperatorsType.XUID                   , NumberArray },
               { TopDictOperatorsType.charset                , Number },
               { TopDictOperatorsType.Encoding               , Number },
               { TopDictOperatorsType.CharStrings            , Number },
               { TopDictOperatorsType.Private                , NumberNumber },
               { TopDictOperatorsType.SyntheticBase          , Number },
               { TopDictOperatorsType.PostScript             , Sid },
               { TopDictOperatorsType.BaseFontName           , Sid },
               { TopDictOperatorsType.BaseFontBlend          , Delta },
               { TopDictOperatorsType.ROS                    , SidSidNumber },
               { TopDictOperatorsType.CIDFontVersion         , Number },
               { TopDictOperatorsType.CIDFontRevision        , Number },
               { TopDictOperatorsType.CIDFontType            , Number },
               { TopDictOperatorsType.CIDCount               , Number },
               { TopDictOperatorsType.UIDBase                , Number },
               { TopDictOperatorsType.FDArray                , Number },
               { TopDictOperatorsType.FDSelect               , Number },
               { TopDictOperatorsType.FontName               , Sid }
            };

            FillOperatorsRawValues(rawData);
        }

        public GenericOperandResult GetOperatorValue(TopDictOperatorsType opType)
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

                    operatorsRawValues[(TopDictOperatorsType)token] = rawOperands;
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
