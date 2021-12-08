using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Adamantium.Fonts.Common;
using Adamantium.Fonts.Tables.CFF;

namespace Adamantium.Fonts.Parsers.CFF
{
    internal class DictOperandParser
    {
        private Dictionary<ushort, DictOperatorsType> byteToOperatorMap;
        private Dictionary<DictOperatorsType, Func<List<GenericOperandResult>, GenericOperandResult>> operatorsActions;
        protected Dictionary<DictOperatorsType, List<GenericOperandResult>> OperatorsRawValues;
        protected CFFFont Font { get; }

        public DictOperandParser(byte[] rawData, CFFFont font)
        {
            OperatorsRawValues = new Dictionary<DictOperatorsType, List<GenericOperandResult>>();
            Font = font;
            
            byteToOperatorMap = new Dictionary<ushort, DictOperatorsType>
            {
                { (ushort)DictOperatorsType.version              , DictOperatorsType.version              },
                { (ushort)DictOperatorsType.Notice               , DictOperatorsType.Notice               },
                { (ushort)DictOperatorsType.Copyright            , DictOperatorsType.Copyright            },
                { (ushort)DictOperatorsType.FullName             , DictOperatorsType.FullName             },
                { (ushort)DictOperatorsType.FamilyName           , DictOperatorsType.FamilyName           },
                { (ushort)DictOperatorsType.Weight               , DictOperatorsType.Weight               },
                { (ushort)DictOperatorsType.isFixedPitch         , DictOperatorsType.isFixedPitch         },
                { (ushort)DictOperatorsType.ItalicAngle          , DictOperatorsType.ItalicAngle          },
                { (ushort)DictOperatorsType.UnderlinePosition    , DictOperatorsType.UnderlinePosition    },
                { (ushort)DictOperatorsType.UnderlineThickness   , DictOperatorsType.UnderlineThickness   },
                { (ushort)DictOperatorsType.PaintType            , DictOperatorsType.PaintType            },
                { (ushort)DictOperatorsType.CharStringType       , DictOperatorsType.CharStringType       },
                { (ushort)DictOperatorsType.FontMatrix           , DictOperatorsType.FontMatrix           },
                { (ushort)DictOperatorsType.UniqueID             , DictOperatorsType.UniqueID             },
                { (ushort)DictOperatorsType.FontBBox             , DictOperatorsType.FontBBox             },
                { (ushort)DictOperatorsType.StrokeWidth          , DictOperatorsType.StrokeWidth          },
                { (ushort)DictOperatorsType.XUID                 , DictOperatorsType.XUID                 },
                { (ushort)DictOperatorsType.charset              , DictOperatorsType.charset              },
                { (ushort)DictOperatorsType.Encoding             , DictOperatorsType.Encoding             },
                { (ushort)DictOperatorsType.CharStrings          , DictOperatorsType.CharStrings          },
                { (ushort)DictOperatorsType.Private              , DictOperatorsType.Private              },
                { (ushort)DictOperatorsType.SyntheticBase        , DictOperatorsType.SyntheticBase        },
                { (ushort)DictOperatorsType.PostScript           , DictOperatorsType.PostScript           },
                { (ushort)DictOperatorsType.BaseFontName         , DictOperatorsType.BaseFontName         },
                { (ushort)DictOperatorsType.BaseFontBlend        , DictOperatorsType.BaseFontBlend        },
                { (ushort)DictOperatorsType.ROS                  , DictOperatorsType.ROS                  },
                { (ushort)DictOperatorsType.CIDFontVersion       , DictOperatorsType.CIDFontVersion       },
                { (ushort)DictOperatorsType.CIDFontRevision      , DictOperatorsType.CIDFontRevision      },
                { (ushort)DictOperatorsType.CIDFontType          , DictOperatorsType.CIDFontType          },
                { (ushort)DictOperatorsType.CIDCount             , DictOperatorsType.CIDCount             },
                { (ushort)DictOperatorsType.UIDBase              , DictOperatorsType.UIDBase              },
                { (ushort)DictOperatorsType.FDArray              , DictOperatorsType.FDArray              },
                { (ushort)DictOperatorsType.FDSelect             , DictOperatorsType.FDSelect             },
                { (ushort)DictOperatorsType.FontName             , DictOperatorsType.FontName             },
                
                // Private DICT
                { (ushort)DictOperatorsType.BlueValues           , DictOperatorsType.BlueValues          },
                { (ushort)DictOperatorsType.OtherBlues           , DictOperatorsType.OtherBlues          },
                { (ushort)DictOperatorsType.FamilyBlues          , DictOperatorsType.FamilyBlues         },
                { (ushort)DictOperatorsType.FamilyOtherBlues     , DictOperatorsType.FamilyOtherBlues    },
                { (ushort)DictOperatorsType.BlueScale            , DictOperatorsType.BlueScale           },
                { (ushort)DictOperatorsType.BlueShift            , DictOperatorsType.BlueShift           },
                { (ushort)DictOperatorsType.BlueFuzz             , DictOperatorsType.BlueFuzz            },
                { (ushort)DictOperatorsType.StdHW                , DictOperatorsType.StdHW               },
                { (ushort)DictOperatorsType.StdVW                , DictOperatorsType.StdVW               },
                { (ushort)DictOperatorsType.StemSnapH            , DictOperatorsType.StemSnapH           },
                { (ushort)DictOperatorsType.StemSnapV            , DictOperatorsType.StemSnapV           },
                { (ushort)DictOperatorsType.ForceBold            , DictOperatorsType.ForceBold           },
                { (ushort)DictOperatorsType.LanguageGroup        , DictOperatorsType.LanguageGroup       },
                { (ushort)DictOperatorsType.ExpansionFactor      , DictOperatorsType.ExpansionFactor     },
                { (ushort)DictOperatorsType.InitialRandomSeed    , DictOperatorsType.InitialRandomSeed   },
                { (ushort)DictOperatorsType.Subrs                , DictOperatorsType.Subrs               },
                { (ushort)DictOperatorsType.DefaultWidthX        , DictOperatorsType.DefaultWidthX       },
                { (ushort)DictOperatorsType.NominalWidthX        , DictOperatorsType.NominalWidthX       },
                
                // CFF 2
                { (ushort)DictOperatorsType.vsindex              , DictOperatorsType.vsindex             },
                { (ushort)DictOperatorsType.blend                , DictOperatorsType.blend               },
                { (ushort)DictOperatorsType.vstore               , DictOperatorsType.vstore              }
            };

            operatorsActions = new Dictionary<DictOperatorsType, Func<List<GenericOperandResult>, GenericOperandResult>>
            {
               { DictOperatorsType.version                , Sid },
               { DictOperatorsType.Notice                 , Sid },
               { DictOperatorsType.Copyright              , Sid },
               { DictOperatorsType.FullName               , Sid },
               { DictOperatorsType.FamilyName             , Sid },
               { DictOperatorsType.Weight                 , Sid },
               { DictOperatorsType.isFixedPitch           , Boolean },
               { DictOperatorsType.ItalicAngle            , Number },
               { DictOperatorsType.UnderlinePosition      , Number },
               { DictOperatorsType.UnderlineThickness     , Number },
               { DictOperatorsType.PaintType              , Number },
               { DictOperatorsType.CharStringType         , Number },
               { DictOperatorsType.FontMatrix             , NumberArray },
               { DictOperatorsType.UniqueID               , Number },
               { DictOperatorsType.FontBBox               , NumberArray },
               { DictOperatorsType.StrokeWidth            , Number },
               { DictOperatorsType.XUID                   , NumberArray },
               { DictOperatorsType.charset                , Number },
               { DictOperatorsType.Encoding               , Number },
               { DictOperatorsType.CharStrings            , Number },
               { DictOperatorsType.Private                , NumberNumber },
               { DictOperatorsType.SyntheticBase          , Number },
               { DictOperatorsType.PostScript             , Sid },
               { DictOperatorsType.BaseFontName           , Sid },
               { DictOperatorsType.BaseFontBlend          , Delta },
               { DictOperatorsType.ROS                    , SidSidNumber },
               { DictOperatorsType.CIDFontVersion         , Number },
               { DictOperatorsType.CIDFontRevision        , Number },
               { DictOperatorsType.CIDFontType            , Number },
               { DictOperatorsType.CIDCount               , Number },
               { DictOperatorsType.UIDBase                , Number },
               { DictOperatorsType.FDArray                , Number },
               { DictOperatorsType.FDSelect               , Number },
               { DictOperatorsType.FontName               , Sid },
               
               // Private DICT part
               { DictOperatorsType.BlueValues            , Delta },
               { DictOperatorsType.OtherBlues            , Delta },
               { DictOperatorsType.FamilyBlues           , Delta },
               { DictOperatorsType.FamilyOtherBlues      , Delta },
               { DictOperatorsType.BlueScale             , Number },
               { DictOperatorsType.BlueShift             , Number },
               { DictOperatorsType.BlueFuzz              , Number },
               { DictOperatorsType.StdHW                 , Number },
               { DictOperatorsType.StdVW                 , Number },
               { DictOperatorsType.StemSnapH             , Delta },
               { DictOperatorsType.StemSnapV             , Delta },
               { DictOperatorsType.ForceBold             , Boolean },
               { DictOperatorsType.LanguageGroup         , Number },
               { DictOperatorsType.ExpansionFactor       , Number },
               { DictOperatorsType.InitialRandomSeed     , Number },
               { DictOperatorsType.Subrs                 , Number },
               { DictOperatorsType.DefaultWidthX         , Number },
               { DictOperatorsType.NominalWidthX         , Number },
               
               // CFF 2
               { DictOperatorsType.vsindex               , Number },
               { DictOperatorsType.blend                 , Delta  },
               { DictOperatorsType.vstore                , Number }
            };

            FillDefaultValues();
            FillOperatorsRawValues(font, rawData);
        }
        
        // convert raw byte stream to GenericOperandResult (will always produce Number)
        protected GenericOperandResult Number(List<byte> rawData)
        {
            if (rawData[0] == 30) // it's double
            {
                return Double(rawData);
            }

            // it's integer
            return Integer(rawData);
        }

        protected GenericOperandResult Integer(List<byte> rawData) // store int as double for compatibility between integer and real numbers in CFF
        {
            var b0 = GetFirstByteAndRemove(rawData);

            if (b0 >= 32 && b0 <= 246)
            {
                return b0 - 139;
            }
            else if (b0 >= 247 && b0 <= 250)
            {
                var b1 = GetFirstByteAndRemove(rawData);

                return (b0 - 247) * 256 + b1 + 108;
            }
            else if (b0 >= 251 && b0 <= 254)
            {
                var b1 = GetFirstByteAndRemove(rawData);

                return -(b0 - 251) * 256 - b1 - 108;
            }
            else if (b0 == 28)
            {
                var b1 = GetFirstByteAndRemove(rawData);
                var b2 = GetFirstByteAndRemove(rawData);

                return (b1 << 8) | b2;
            }
            else if (b0 == 29)
            {
                var b1 = GetFirstByteAndRemove(rawData);
                var b2 = GetFirstByteAndRemove(rawData);
                var b3 = GetFirstByteAndRemove(rawData);
                var b4 = GetFirstByteAndRemove(rawData);

                return (b1 << 24) | (b2 << 16) | (b3 << 8) | b4;
            }
            else
            {
                throw new ArgumentException($"'b0' = {b0} is out of range!");
            }
        }

        protected GenericOperandResult Double(List<byte> rawData)
        {
            List<byte> nibbles = new List<byte>();
            string stringRes = String.Empty;

            GetFirstByteAndRemove(rawData); // rawData[0] is 30 -> pointing that it is a double, skip it and remove

            while(rawData.Count > 0)
            {
                bool done = false;
                var b = GetFirstByteAndRemove(rawData);
                var ns = ParseNibbles(b);

                nibbles.Add(ns.HiNibble);
                nibbles.Add(ns.LoNibble);

                foreach (var nibble in nibbles)
                {
                    if (GetTokenFromNibble(nibble, out var token))
                    {
                        stringRes += token;
                    }
                    else
                    {
                        done = true;
                        break;
                    }
                }

                if (done)
                {
                    break;
                }

                nibbles.Clear();
            }

            return Convert.ToDouble(stringRes, CultureInfo.InvariantCulture);
        }

        // convert raw Numbers to each operator's output type (e.g. calculate Deltas)
        protected GenericOperandResult Number(List<GenericOperandResult> rawOperands)
        {
            return rawOperands[0];
        }

        protected GenericOperandResult NumberNumber(List<GenericOperandResult> rawOperands)
        {
            var number1 = rawOperands[0];
            var number2 = rawOperands[1];

            return (Number1: number1.AsDouble(), Number2: number2.AsDouble());
        }

        protected GenericOperandResult Boolean(List<GenericOperandResult> rawOperands)
        {
            return rawOperands[0];
        }

        protected GenericOperandResult Sid(List<GenericOperandResult> rawOperands)
        {
            return rawOperands[0];
        }

        protected GenericOperandResult SidSidNumber(List<GenericOperandResult> rawOperands)
        {
            ushort sid1 = rawOperands[0].AsUShort();
            ushort sid2 = rawOperands[1].AsUShort();
            double number = rawOperands[2].AsDouble();

            return (Sid1: sid1, Sid2: sid2, Number: number);
        }
        protected GenericOperandResult NumberArray(List<GenericOperandResult> rawOperands)
        {
            List<double> numbers = new List<double>();

            foreach (var operand in rawOperands)
            {
                numbers.Add(operand.AsDouble());
            }

            return numbers;
        }
        protected GenericOperandResult Delta(List<GenericOperandResult> rawOperands)
        {
            List<double> deltas = new List<double>();

            List<double> numbers = NumberArray(rawOperands).AsList();

            deltas.Add(numbers[0]); // add first one, because there is at least one element in deltas 

            for (var i = 1; i < numbers.Count; ++i)
            {
                deltas.Add(numbers[i] - numbers[i - 1]);
            }

            return deltas;
        }

        protected (byte HiNibble, byte LoNibble) ParseNibbles(byte raw)
        {
            return ( HiNibble: (byte)(raw >> 4), LoNibble: (byte)(raw & 0x0F) );
        }

        // used for processing number byte-by-byte with deletion of processed byte from raw byte array
        protected byte GetFirstByteAndRemove(List<byte> rawData)
        {
            byte b = rawData[0];
            rawData.RemoveAt(0);
            return b;
        }

        protected bool GetTokenFromNibble(byte nibble, out string token)
        {
            token = String.Empty;

            if (nibble >= 0 && nibble <= 9)
            {
                token = nibble.ToString();
            }
            else if (nibble == 0xA)
            {
                token = ".";
            }
            else if (nibble == 0xB)
            {
                token = "E+";
            }
            else if (nibble == 0xC)
            {
                token = "E-";
            }
            else if (nibble == 0xD)
            {
                // not used
            }
            else if (nibble == 0xE)
            {
                token = "-";
            }
            else if (nibble == 0xF)
            {
                return false;
            }

            return true;
        }
        
        private GenericOperandResult GetOperatorValue(DictOperatorsType opType)
        {
            return operatorsActions[opType].Invoke(OperatorsRawValues[opType]);
        }

        public GenericOperandResultSet GetAllAvailableOperands()
        {
            var dict = new Dictionary<DictOperatorsType, GenericOperandResult>();
            
            foreach (var kvp in OperatorsRawValues)
            {
                dict[kvp.Key] = operatorsActions[kvp.Key].Invoke(kvp.Value);
            }

            return new GenericOperandResultSet(dict);
        }
        
        protected virtual void FillDefaultValues()
        {
        }
        
        private void FillOperatorsRawValues(CFFFont font, byte[] rawData)
        {
            var byteArray = new List<byte>(rawData);
            var rawOperands = new List<GenericOperandResult>();

            while (byteArray.Count > 0)
            {
                ushort token = byteArray[0];

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

                    if ((DictOperatorsType) token == DictOperatorsType.blend)
                    {
                        var blendedOperandsCount =  rawOperands[^1].AsInt();
                        var regionCount = font.VariationStore.VariationRegionList.RegionCount;
                        var overallBlendOperandsCount = blendedOperandsCount * (regionCount + 1) + 1;

                        var startIndexOfBlendOperands = rawOperands.Count - overallBlendOperandsCount;

                        var blendOperands = rawOperands.ToArray()[(int)startIndexOfBlendOperands..].ToList();

                        rawOperands = rawOperands.GetRange(0, (int)startIndexOfBlendOperands);

                        var blendedOperands = blendOperands.GetRange(0, (int) blendedOperandsCount);
                        var deltas = blendOperands.ToArray()[(int)(blendedOperandsCount)..^1];

                        for (var op = 0; op < blendedOperands.Count; ++op)
                        {
                            var blendData = new RegionData();

                            for (var region = 0; region < regionCount; ++region)
                            {
                                blendData.Data.Add(deltas[op * regionCount + region].AsDouble());
                            }

                            var genericResult = blendedOperands[op];
                            genericResult.BlendData = blendData;
                            blendedOperands[op] = genericResult;
                        }

                        rawOperands.AddRange(blendedOperands);
                    }
                    else
                    {
                        OperatorsRawValues[(DictOperatorsType)token] = rawOperands;
                        rawOperands = new List<GenericOperandResult>();
                    }
                }
                else
                {
                    rawOperands.Add(Number(byteArray));
                }
            }
        }
        
        public bool IsOperatorAvailable(DictOperatorsType opType)
        {
            return OperatorsRawValues.ContainsKey(opType);
        }

        public GenericOperandResult this[DictOperatorsType op] => GetOperatorValue(op);
    }   
}
