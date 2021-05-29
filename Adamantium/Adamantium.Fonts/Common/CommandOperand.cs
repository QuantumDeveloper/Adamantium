using System;

namespace Adamantium.Fonts.Common
{
    internal class CommandOperand
    {
        public double Value { get; set; }
        public RegionData BlendData { get; set; }

        public CommandOperand(double value)
        {
            Value = value;
        }

        public static CommandOperand operator +(CommandOperand left, CommandOperand right)
        {
            return new (left.Value + right.Value);
        }
        
        public static CommandOperand operator -(CommandOperand left, CommandOperand right)
        {
            return new (left.Value - right.Value);
        }
        
        public static CommandOperand operator *(CommandOperand left, CommandOperand right)
        {
            return new (left.Value * right.Value);
        }
        
        public static CommandOperand operator /(CommandOperand left, CommandOperand right)
        {
            return new (left.Value / right.Value);
        }
        
        public static CommandOperand operator -(CommandOperand value)
        {
            return new (-value.Value);
        }
        
        public static bool operator ==(CommandOperand left, CommandOperand right)
        {
            if (ReferenceEquals(left, null) ||
                ReferenceEquals(right, null))
            {
                return false;
            }
            
            return left.Value == right.Value;
        }
        
        public static bool operator !=(CommandOperand left, CommandOperand right)
        {
            return !(left == right);
        }

        public static bool operator >(CommandOperand left, CommandOperand right)
        {
            if (ReferenceEquals(left, null) ||
                ReferenceEquals(right, null))
            {
                return false;
            }
            
            return left.Value > right.Value;
        }
        
        public static bool operator <(CommandOperand left, CommandOperand right)
        {
            if (ReferenceEquals(left, null) ||
                ReferenceEquals(right, null))
            {
                return false;
            }
            
            return left.Value < right.Value;
        }

        public static bool operator >=(CommandOperand left, CommandOperand right)
        {
            if (ReferenceEquals(left, null) ||
                ReferenceEquals(right, null))
            {
                return false;
            }
            
            return left.Value >= right.Value;
        }
        
        public static bool operator <=(CommandOperand left, CommandOperand right)
        {
            if (ReferenceEquals(left, null) ||
                ReferenceEquals(right, null))
            {
                return false;
            }
            
            return left.Value <= right.Value;
        }

        public override string ToString()
        {
            string blendDataString = String.Empty;

            if (BlendData != null)
            {
                blendDataString = $", BlendData: {string.Join(',', BlendData.Data)}";
            }

            return $"{Value}" + blendDataString;
        }
    }
}