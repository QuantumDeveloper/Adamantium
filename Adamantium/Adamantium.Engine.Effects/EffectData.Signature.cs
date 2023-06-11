﻿using System;
using MessagePack;

namespace Adamantium.Engine.Effects
{
   public sealed partial class EffectData
   {
      /// <summary>	
      /// <p>Describes a shader signature.</p>	
      /// </summary>	
      /// <remarks>	
      /// Describes an input or output signature, composed of <see cref="Semantic"/> descriptions.
      /// </remarks>
      [MessagePackObject]
      public sealed class Signature : IEquatable<Signature>
      {
         /// <summary>
         /// Gets or sets the semantics
         /// </summary>
         [Key(0)]
         public Semantic[] Semantics;

         /// <summary>
         /// Gets the bytecode of this signature. This field is only valid for Input Vertex Shader.
         /// </summary>
         [Key(1)]
         public byte[] Bytecode;

         /// <summary>
         /// Gets the hashcode associated with the signature bytecode.
         /// </summary>
         [Key(2)]
         public int Hashcode;

         public bool Equals(Signature other)
         {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            if (Semantics.Length != other.Semantics.Length)
               return false;

            for (int i = 0; i < Semantics.Length; i++)
               if (Semantics[i] != other.Semantics[i])
                  return false;

            return true;
         }

         public override bool Equals(object obj)
         {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Signature)obj);
         }

         public override int GetHashCode()
         {
            return Semantics.Length;
         }

         public static bool operator ==(Signature left, Signature right)
         {
            return Equals(left, right);
         }

         public static bool operator !=(Signature left, Signature right)
         {
            return !Equals(left, right);
         }
      }
   }
}
