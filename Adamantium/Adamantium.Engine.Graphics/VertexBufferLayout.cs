﻿using AdamantiumVulkan.Core;
using System;
using System.Collections.ObjectModel;

namespace Adamantium.Engine.Core
{
   /// <summary>	
   /// A description of a vertex elements for particular slot for the input-assembler stage. 
   /// This structure is related to <see cref="SharpDX.Direct3D11.InputElement"/>.
   /// </summary>	
   /// <remarks>	
   /// Because <see cref="SharpDX.Direct3D11.InputElement"/> requires to have the same <see cref="SlotIndex"/>, <see cref="InstanceCount"/>,
   /// this <see cref="VertexBufferLayout"/> structure encapsulates a set of <see cref="VertexInputElement"/> for a particular slot, instance count.
   /// </remarks>	
   /// <seealso cref="VertexInputElement"/>
   /// <msdn-id>ff476180</msdn-id>	
   /// <unmanaged>D3D11_INPUT_ELEMENT_DESC</unmanaged>	
   /// <unmanaged-short>D3D11_INPUT_ELEMENT_DESC</unmanaged-short>
   public class VertexBufferLayout : IEquatable<VertexBufferLayout>
   {
      /// <summary>
      /// Vertex buffer slot index.
      /// </summary>
      public readonly int SlotIndex;

      /// <summary>	
      /// The number of instances to draw using the same per-instance data before advancing in the buffer by one element. This value must be 0 for an  element that contains per-vertex data (the slot class is set to <see cref="SharpDX.Direct3D11.InputClassification.PerVertexData"/>).
      /// </summary>	
      /// <msdn-id>ff476180</msdn-id>	
      /// <unmanaged>unsigned int InstanceDataStepRate</unmanaged>	
      /// <unmanaged-short>unsigned int InstanceDataStepRate</unmanaged-short>	
      public readonly int InstanceCount;

      /// <summary>
      /// Vertex elements describing this declaration.
      /// </summary>
      public readonly ReadOnlyCollection<VertexInputElement> VertexElements;

      /// <summary>
      /// Precalculate hashcode for faster comparison.
      /// </summary>
      private int hashCode;

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexBufferLayout" /> struct.
      /// </summary>
      /// <param name="slot">The slot to bind this vertex buffer to. </param>
      /// <param name="elements">The elements.</param>
      /// <param name="instanceCount">The instance data step rate.</param>
      private VertexBufferLayout(int slot, VertexInputElement[] elements, int instanceCount)
      {
         if (elements == null)
            throw new ArgumentNullException(nameof(elements));

         if (elements.Length == 0)
            throw new ArgumentException("Vertex elements cannot have zero elements.", nameof(elements));

         // Make a copy of the elements.
         var copyElements = new VertexInputElement[elements.Length];
         Array.Copy(elements, copyElements, elements.Length);

         SlotIndex = slot;
         VertexElements = new ReadOnlyCollection<VertexInputElement>(CalculateStaticOffsets(copyElements));
         InstanceCount = instanceCount;

         ComputeHashcode();
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexBufferLayout" /> struct.
      /// </summary>
      /// <param name="slot">The slot to bind this vertex buffer to.</param>
      /// <param name="structType">Type of a structure that is using <see cref="VertexInputElementAttribute" />.</param>
      /// <param name="instanceCount">Specify the instancing count. Set to 0 for no instancing.</param>
      /// <returns>A new instance of <see cref="VertexBufferLayout"/>.</returns>
      public static VertexBufferLayout New(int slot, Type structType, int instanceCount = 0)
      {
         var vertexElements = VertexInputElement.FromType(structType);
         if (vertexElements == null)
         {
            throw new ArgumentException(
               $"Unable to calculate VertexElements from Type [{structType.Name}]. This type is not using VertexInputElementAttribute.", nameof(structType));
         }

         return New(slot, vertexElements, instanceCount);
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexBufferLayout" /> struct.
      /// </summary>
      /// <typeparam name="T">Type of a structure that is using <see cref="VertexInputElementAttribute" />.</typeparam>
      /// <param name="slot">The slot to bind this vertex buffer to.</param>
      /// <param name="instanceCount">Specify the instancing count. Set to 0 for no instancing.</param>
      /// <returns>A new instance of <see cref="VertexBufferLayout"/>.</returns>
      public static VertexBufferLayout New<T>(int slot, int instanceCount = 0) where T : struct
      {
         return New(slot, typeof(T), instanceCount);
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexBufferLayout" /> struct.
      /// </summary>
      /// <param name="slot">The slot to bind this vertex buffer to.</param>
      /// <param name="elements">The elements.</param>
      /// <returns>A new instance of <see cref="VertexBufferLayout"/>.</returns>
      public static VertexBufferLayout New(int slot, params VertexInputElement[] elements)
      {
         return New(slot, elements, 0);
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexBufferLayout" /> struct with instantiated data.
      /// </summary>
      /// <param name="slot">The slot to bind this vertex buffer to.</param>
      /// <param name="elements">The elements.</param>
      /// <param name="instanceCount">Specify the instancing count. Set to 0 for no instancing.</param>
      /// <returns>A new instance of <see cref="VertexBufferLayout"/>.</returns>
      public static VertexBufferLayout New(int slot, VertexInputElement[] elements, int instanceCount)
      {
         return new VertexBufferLayout(slot, elements, instanceCount);
      }

      private static VertexInputElement[] CalculateStaticOffsets(VertexInputElement[] vertexElements)
      {
         int offset = 0;
         for (int i = 0; i < vertexElements.Length; i++)
         {
            // If offset is not specified, use the current offset
            if (vertexElements[i].AlignedByteOffset == -1)
               vertexElements[i].AlignedByteOffset = offset;
            else
               offset = vertexElements[i].AlignedByteOffset;

            // Move to the next field.
            offset += FormatHelper.SizeOfInBytes(vertexElements[i].Format);
         }
         return vertexElements;
      }

      public bool Equals(VertexBufferLayout other)
      {
         if (ReferenceEquals(null, other)) return false;
         if (ReferenceEquals(this, other)) return true;
         return hashCode == other.hashCode && VertexElements.Equals(other.VertexElements) && InstanceCount == other.InstanceCount;
      }

      public override bool Equals(object obj)
      {
         if (ReferenceEquals(null, obj)) return false;
         if (ReferenceEquals(this, obj)) return true;
         if (obj.GetType() != this.GetType()) return false;
         return Equals((VertexBufferLayout)obj);
      }

      public override int GetHashCode()
      {
         return hashCode;
      }

      private void ComputeHashcode()
      {
         // precalculate the hashcode for this instance
         hashCode = InstanceCount;
         hashCode = (hashCode * 397) ^ VertexElements.GetHashCode();
      }

      public static bool operator ==(VertexBufferLayout left, VertexBufferLayout right)
      {
         return Equals(left, right);
      }

      public static bool operator !=(VertexBufferLayout left, VertexBufferLayout right)
      {
         return !Equals(left, right);
      }
   }
}
