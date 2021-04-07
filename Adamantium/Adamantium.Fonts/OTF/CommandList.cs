using System;
using System.Collections.Generic;
using System.Linq;

namespace Adamantium.Fonts.OTF
{
    internal class CommandList : OperandParser
    {
        private static Dictionary<ushort, OperatorsType> bytesToOperatorMap;
        private ICFFParser cffParser;
        internal List<Command> commands;

        internal CommandList(ICFFParser cffParser)
        {
            this.cffParser = cffParser;
            commands = new List<Command>();
        }

        static CommandList()
        {
            bytesToOperatorMap = new Dictionary<ushort, OperatorsType>
            {
                { (ushort)OperatorsType.hstem               , OperatorsType.hstem        },
                { (ushort)OperatorsType.vstem               , OperatorsType.vstem        },
                { (ushort)OperatorsType.vmoveto             , OperatorsType.vmoveto      },
                { (ushort)OperatorsType.rlineto             , OperatorsType.rlineto      },
                { (ushort)OperatorsType.hlineto             , OperatorsType.hlineto      },
                { (ushort)OperatorsType.vlineto             , OperatorsType.vlineto      },
                { (ushort)OperatorsType.rrcurveto           , OperatorsType.rrcurveto    },
                { (ushort)OperatorsType.callsubr            , OperatorsType.callsubr     },
                { (ushort)OperatorsType.@return             , OperatorsType.@return      },
                { (ushort)OperatorsType.and                 , OperatorsType.and          },
                { (ushort)OperatorsType.or                  , OperatorsType.or           },
                { (ushort)OperatorsType.not                 , OperatorsType.not          },
                { (ushort)OperatorsType.abs                 , OperatorsType.abs          },
                { (ushort)OperatorsType.add                 , OperatorsType.add          },
                { (ushort)OperatorsType.sub                 , OperatorsType.sub          },
                { (ushort)OperatorsType.div                 , OperatorsType.div          },
                { (ushort)OperatorsType.neg                 , OperatorsType.neg          },
                { (ushort)OperatorsType.eq                  , OperatorsType.eq           },
                { (ushort)OperatorsType.drop                , OperatorsType.drop         },
                { (ushort)OperatorsType.put                 , OperatorsType.put          },
                { (ushort)OperatorsType.get                 , OperatorsType.get          },
                { (ushort)OperatorsType.ifelse              , OperatorsType.ifelse       },
                { (ushort)OperatorsType.random              , OperatorsType.random       },
                { (ushort)OperatorsType.mul                 , OperatorsType.mul          },
                { (ushort)OperatorsType.sqrt                , OperatorsType.sqrt         },
                { (ushort)OperatorsType.dup                 , OperatorsType.dup          },
                { (ushort)OperatorsType.exch                , OperatorsType.exch         },
                { (ushort)OperatorsType.index               , OperatorsType.index        },
                { (ushort)OperatorsType.roll                , OperatorsType.roll         },
                { (ushort)OperatorsType.hflex               , OperatorsType.hflex        },
                { (ushort)OperatorsType.flex                , OperatorsType.flex         },
                { (ushort)OperatorsType.hflex1              , OperatorsType.hflex1       },
                { (ushort)OperatorsType.flex1               , OperatorsType.flex1        },
                { (ushort)OperatorsType.endchar             , OperatorsType.endchar      },
                { (ushort)OperatorsType.hstemhm             , OperatorsType.hstemhm      },
                { (ushort)OperatorsType.hintmask            , OperatorsType.hintmask     },
                { (ushort)OperatorsType.cntrmask            , OperatorsType.cntrmask     },
                { (ushort)OperatorsType.rmoveto             , OperatorsType.rmoveto      },
                { (ushort)OperatorsType.hmoveto             , OperatorsType.hmoveto      },
                { (ushort)OperatorsType.vstemhm             , OperatorsType.vstemhm      },
                { (ushort)OperatorsType.rcurveline          , OperatorsType.rcurveline   },
                { (ushort)OperatorsType.rlinecurve          , OperatorsType.rlinecurve   },
                { (ushort)OperatorsType.vvcurveto           , OperatorsType.vvcurveto    },
                { (ushort)OperatorsType.hhcurveto           , OperatorsType.hhcurveto    },
                { (ushort)OperatorsType.callgsubr           , OperatorsType.callgsubr    },
                { (ushort)OperatorsType.vhcurveto           , OperatorsType.vhcurveto    },
                { (ushort)OperatorsType.hvcurveto           , OperatorsType.hvcurveto    },
                { (ushort)OperatorsType.vsindex             , OperatorsType.vsindex      },
                { (ushort)OperatorsType.blend               , OperatorsType.blend        },
            };
        }

        public void Fill(CFFFont font, Stack<byte> mainStack, FontDict fontDict, int index = 0)
        {
            List<double> operands = new List<double>();
            ushort token;
            bool clearOperands;
            int stemCount = 0;
            OperatorsType? additionalOperator = null;

            var fontDictBias = 0;
            if (fontDict?.LocalSubr != null)
            {
                fontDictBias = cffParser.CalculateSubrBias((uint)fontDict.LocalSubr.Count);
            }

            while (mainStack.Count > 0)
            {
                token = mainStack.Pop();

                if (token == 12) // escape byte found, it is a 2-byte operator
                {
                    token = (ushort)((12 << 8) | mainStack.Pop());
                }

                if (bytesToOperatorMap.ContainsKey(token)) // this is operator
                {
                    clearOperands = true;

                    // TO DO: add mechanism to process operators like 'add' - not resulting in new Command, but instead pushes its result to the top of the stack
                    switch (token)
                    {
                        case (ushort)OperatorsType.and:
                            mainStack.Push(Convert.ToByte(Convert.ToBoolean(operands[0]) && Convert.ToBoolean(operands[1])));
                            break;
                        case (ushort)OperatorsType.or:
                            mainStack.Push(Convert.ToByte(Convert.ToBoolean(operands[0]) || Convert.ToBoolean(operands[1])));
                            break;
                        case (ushort)OperatorsType.not:
                            mainStack.Push(Convert.ToByte(!Convert.ToBoolean(operands[0])));
                            break;
                        case (ushort)OperatorsType.eq:
                            mainStack.Push(Convert.ToByte(Convert.ToBoolean(operands[0]) == Convert.ToBoolean(operands[1])));
                            break;
                        case (ushort)OperatorsType.drop:
                            operands.RemoveAt(0);
                            break;
                        case (ushort)OperatorsType.ifelse:
                            clearOperands = false;

                            var res = (operands[2] <= operands[3]) ? operands[0] : operands[1];
                            operands.Clear();
                            operands.Add(res);
                            break;
                        case (ushort)OperatorsType.random:
                            clearOperands = false;

                            var random = new Random();

                            double rand = -(random.NextDouble() - 1);
                            operands.Clear();
                            operands.Add(rand);
                            break;
                        case (ushort)OperatorsType.abs:
                            clearOperands = false;

                            operands[0] = Math.Abs(operands[0]);
                            break;
                        case (ushort)OperatorsType.add:
                            clearOperands = false;

                            var sum = operands[0] + operands[1];
                            operands.Clear();
                            operands.Add(sum);
                            break;
                        case (ushort)OperatorsType.sub:
                            clearOperands = false;

                            var diff = operands[0] - operands[1];
                            operands.Clear();
                            operands.Add(diff);
                            break;
                        case (ushort)OperatorsType.div:
                            clearOperands = false;

                            var quo = operands[0] / operands[1];
                            operands.Clear();
                            operands.Add(quo);
                            break;
                        case (ushort)OperatorsType.mul:
                            clearOperands = false;

                            var prod = operands[0] * operands[1];
                            operands.Clear();
                            operands.Add(prod);
                            break;
                        case (ushort)OperatorsType.sqrt:
                            clearOperands = false;

                            operands[0] = Math.Sqrt(operands[0]);
                            break;
                        case (ushort)OperatorsType.dup:
                            clearOperands = false;

                            operands.Add(operands[0]);
                            break;
                        case (ushort)OperatorsType.exch:
                            clearOperands = false;

                            var val = operands[0];
                            operands[0] = operands[1];
                            operands[1] = val;
                            break;
                        case (ushort)OperatorsType.neg:
                            clearOperands = false;

                            operands[0] = -operands[0];
                            break;
                        case (ushort)OperatorsType.blend:
                            clearOperands = false;
                            // TODO: think how to correctly integrate blend operators in existing structure (commands, interpreter, dict parser)
                            additionalOperator = OperatorsType.blend;
                            break;
                        case (ushort)OperatorsType.hintmask:
                        case (ushort)OperatorsType.cntrmask:
                            stemCount += operands.Count / 2;
                            
                            if (stemCount == 0) stemCount = 1;
                            var bytesToRead = Math.Ceiling((double) stemCount / 8);
                            for (int i = 0; i < bytesToRead; ++i)
                            {
                                var maskByte = mainStack.Pop();
                            }
                            break;
                        case (ushort)OperatorsType.callgsubr:
                            if (operands.Count == 0)
                            {
                                throw new ArgumentException($"callgsubr operand count == 0!");
                            }

                            var subrIndex = (int) operands.Last() + cffParser.GlobalSubrBias;
                            var subrData = cffParser.GlobalSubroutineIndex.DataByOffset[subrIndex];
                            cffParser.UnpackSubrToStack(subrData, mainStack);
                            operands.RemoveAt(operands.Count - 1);
                            clearOperands = false;
                            break;
                        case (ushort)OperatorsType.callsubr:
                            if (operands.Count == 0)
                            {
                                throw new ArgumentException($"callsubr operand count == 0!");
                            }

                            if (font.IsLocalSubroutineAvailable)
                            {
                                var localSubrIndex = (int) operands.Last() + font.LocalSubrBias;
                                var localSubrData = font.LocalSubroutineIndex.DataByOffset[localSubrIndex];
                                cffParser.UnpackSubrToStack(localSubrData, mainStack);
                            }
                            else if (fontDict != null) // get local subr from private dict
                            {
                                var localSubrIndex = (int) operands.Last() + fontDictBias;
                                var localSubrData = fontDict.LocalSubr[localSubrIndex];
                                cffParser.UnpackSubrToStack(localSubrData, mainStack);
                            }

                            operands.RemoveAt(operands.Count - 1);
                            clearOperands = false;
                            break;
                        case (ushort)OperatorsType.@return:
                            clearOperands = false;
                            break;
                        default:
                            switch (token)
                            {
                                case (ushort)OperatorsType.vstem:
                                case (ushort)OperatorsType.hstem:
                                case (ushort)OperatorsType.hstemhm:
                                case (ushort)OperatorsType.vstemhm:
                                    stemCount+=operands.Count/2;
                                    break;
                            }

                            var command = new Command();

                            command.@operator = bytesToOperatorMap[token];
                            command.additionalOperator = additionalOperator;
                            command.operands = operands;

                            additionalOperator = null;

                            commands.Add(command);
                            break;
                    }

                    if (clearOperands)
                    {
                        operands = new List<double>();
                    }
                }
                else // this is operand
                {
                    operands.Add(Number((byte)token, mainStack).AsDouble());
                }
            }
        }

        public void Clear()
        {
            commands?.Clear();
        }
    }
}
