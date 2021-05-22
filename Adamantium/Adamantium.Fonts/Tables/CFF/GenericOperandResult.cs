using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CFF
{
    internal struct GenericOperandResult
    {
        public List<double> Result;
        
        // deltas for each variation region applicable for current operand
        public List<double> Deltas;

        public double GetDeltaForRegion(int index)
        {
            return Deltas[index];
        }
        
        public int AsInt()
        {
            return (int)Result[0];
        }
        
        public uint AsUInt()
        {
            return (uint)Result[0];
        }

        public double AsDouble()
        {
            return Result[0];
        }

        public bool AsBool()
        {
            return Convert.ToBoolean(Result[0]);
        }

        public ushort AsUShort()
        {
            return (ushort)Result[0];
        }

        public List<double> AsList()
        {
            return Result;
        }

        public (double Number1, double Number2) AsNumberNumber()
        {
            return (Number1: Result[0], Number2: Result[1]);
        }

        public (ushort Sid1, ushort Sid2, double Number) AsSidSidNumber()
        {
            return (Sid1: (ushort)Result[0], Sid2: (ushort)Result[1], Number: Result[2]);
        }

        public static implicit operator GenericOperandResult(int value)
        {
            return new GenericOperandResult() { Result = new List<double>() { value } };
        }

        public static implicit operator GenericOperandResult(double value)
        {
            return new GenericOperandResult() { Result = new List<double>() { value } };
        }

        public static implicit operator GenericOperandResult(bool value)
        {
            return new GenericOperandResult() { Result = new List<double>() { Convert.ToDouble(value) } };
        }

        public static implicit operator GenericOperandResult(ushort value)
        {
            return new GenericOperandResult() { Result = new List<double>() { value } };
        }

        public static implicit operator GenericOperandResult(List<double> value)
        {
            return new GenericOperandResult() { Result = value };
        }

        public static implicit operator GenericOperandResult((double Number1, double Number2) value)
        {
            return new GenericOperandResult() { Result = new List<double>() { value.Number1, value.Number2 } };
        }

        public static implicit operator GenericOperandResult((ushort Sid1, ushort Sid2, double Number) value)
        {
            return new GenericOperandResult() { Result = new List<double>() { value.Sid1, value.Sid2, value.Number } };
        }
    }
}
