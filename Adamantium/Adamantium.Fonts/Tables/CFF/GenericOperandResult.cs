using System;
using System.Collections.Generic;

namespace Adamantium.Fonts.Tables.CFF
{
    public struct GenericOperandResult
    {
        public List<double> result;

        public int AsInt()
        {
            return (int)result[0];
        }
        
        public uint AsUInt()
        {
            return (uint)result[0];
        }

        public double AsDouble()
        {
            return (double)result[0];
        }

        public bool AsBool()
        {
            return Convert.ToBoolean(result[0]);
        }

        public ushort AsUShort()
        {
            return (ushort)result[0];
        }

        public List<double> AsList()
        {
            return result;
        }

        public (double Number1, double Number2) AsNumberNumber()
        {
            return (Number1: result[0], Number2: result[1]);
        }

        public (ushort Sid1, ushort Sid2, double Number) AsSidSidNumber()
        {
            return (Sid1: (ushort)result[0], Sid2: (ushort)result[1], Number: result[2]);
        }

        public static implicit operator GenericOperandResult(int value)
        {
            return new GenericOperandResult() { result = new List<double>() { value } };
        }

        public static implicit operator GenericOperandResult(double value)
        {
            return new GenericOperandResult() { result = new List<double>() { value } };
        }

        public static implicit operator GenericOperandResult(bool value)
        {
            return new GenericOperandResult() { result = new List<double>() { Convert.ToDouble(value) } };
        }

        public static implicit operator GenericOperandResult(ushort value)
        {
            return new GenericOperandResult() { result = new List<double>() { value } };
        }

        public static implicit operator GenericOperandResult(List<double> value)
        {
            return new GenericOperandResult() { result = value };
        }

        public static implicit operator GenericOperandResult((double Number1, double Number2) value)
        {
            return new GenericOperandResult() { result = new List<double>() { value.Number1, value.Number2 } };
        }

        public static implicit operator GenericOperandResult((ushort Sid1, ushort Sid2, double Number) value)
        {
            return new GenericOperandResult() { result = new List<double>() { value.Sid1, value.Sid2, value.Number } };
        }
    }
}
