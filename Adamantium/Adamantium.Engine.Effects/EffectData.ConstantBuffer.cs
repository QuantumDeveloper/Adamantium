﻿using System;
using System.Collections.Generic;
using Adamantium.Core;
using MessagePack;

namespace Adamantium.Engine.Effects
{
    public partial class EffectData
    {
        [MessagePackObject]
        public sealed class ConstantBuffer : IEquatable<ConstantBuffer>
        {
            /// <summary>
            /// Name of this constant buffer.
            /// </summary>
            [Key(0)]
            public string Name;

            /// <summary>
            /// Size in bytes of this constant buffer.
            /// </summary>
            [Key(1)]
            public int Size;

            /// <summary>
            /// Binding index
            /// </summary>
            [Key(2)]
            public uint Slot;

            /// <summary>
            /// List of parameters in this constant buffer.
            /// </summary>
            [Key(3)]
            public List<ValueTypeParameter> Parameters;

            public bool Equals(ConstantBuffer other)
            {
                if (ReferenceEquals(null, other)) return false;
                if (ReferenceEquals(this, other)) return true;
                return string.Equals(Name, other.Name) && Size == other.Size && Slot == other.Slot &&
                       Utilities.Compare(Parameters, other.Parameters);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj is ConstantBuffer && Equals((ConstantBuffer)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = Name.GetHashCode();
                    hashCode = (hashCode * 397) ^ Size;
                    hashCode = (hashCode * 397) ^ (int)Slot;
                    hashCode = (hashCode * 397) ^ Parameters.Count;
                    return hashCode;
                }
            }

            public static bool operator ==(ConstantBuffer left, ConstantBuffer right)
            {
                return Equals(left, right);
            }

            public static bool operator !=(ConstantBuffer left, ConstantBuffer right)
            {
                return !Equals(left, right);
            }
        }
    }
}