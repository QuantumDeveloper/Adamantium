using System;
using System.Collections.Generic;
using System.Globalization;

namespace Adamantium.Fonts.OTF
{
    public abstract class DictOperandParser
    {
        // convert raw byte stream to GenericOperandResult (will always produce Number)
        protected GenericOperandResult Number(List<byte> rawData)
        {
            if (rawData[0] == 30) // it's double
            {
                return Double(rawData);
            }
            else // it's integer
            {
                return Integer(rawData);
            }
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
    }   
}
