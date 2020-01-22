﻿using System;
using AdamantiumVulkan.Core;

namespace Adamantium.Engine.Graphics
{
   /// <summary>	
   /// An attribute to use on a field in a structure, to describe a single vertex element for the input-assembler stage.
   /// </summary>	
   /// <seealso cref="VertexInputElement"/>
   /// <msdn-id>ff476180</msdn-id>	
   /// <unmanaged>D3D11_INPUT_ELEMENT_DESC</unmanaged>	
   /// <unmanaged-short>D3D11_INPUT_ELEMENT_DESC</unmanaged-short>	
   [AttributeUsage(AttributeTargets.Field|AttributeTargets.Property)]
   public class VertexInputElementAttribute :Attribute
   {
      /// <summary>	
      /// <dd> <p>The HLSL semantic associated with this element in a shader input-signature.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff476180</msdn-id>	
      /// <unmanaged>const char* SemanticName</unmanaged>	
      /// <unmanaged-short>char SemanticName</unmanaged-short>	
      public String SemanticName { get; private set; }

      /// <summary>	
      /// <dd> <p>The semantic index for the element. A semantic index modifies a semantic, with an integer index number. A semantic index is only needed in a  case where there is more than one element with the same semantic. For example, a 4x4 matrix would have four components each with the semantic  name </p>  <pre><code>matrix</code></pre>  <p>, however each of the four component would have different semantic indices (0, 1, 2, and 3).</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff476180</msdn-id>	
      /// <unmanaged>unsigned int SemanticIndex</unmanaged>	
      /// <unmanaged-short>unsigned int SemanticIndex</unmanaged-short>	
      public Int32 SemanticIndex { get; private set; }

      /// <summary>	
      /// <dd> <p>The data type of the element data. See <strong><see cref="AdamantiumVulkan.Core"/></strong>.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff476180</msdn-id>	
      /// <unmanaged>Vulkan Format</unmanaged>	
      /// <unmanaged-short>Vulkan Format</unmanaged-short>	
      public Format Format { get; private set; }

      /// <summary>	
      /// <dd> <p>Optional. Offset (in bytes) between each element. Used for convenience to define the current element directly  after the previous one, including any packing if necessary.</p> </dd>	
      /// </summary>	
      /// <msdn-id>ff476180</msdn-id>	
      /// <unmanaged>unsigned int BytesOffset</unmanaged>	
      /// <unmanaged-short>unsigned int BytesOffset</unmanaged-short>	
      public Int32 BytesOffset { get; private set; }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexInputElement" /> struct.
      /// </summary>
      /// <param name="semanticName">Name of the semantic.</param>
      /// <remarks>
      /// If the semantic name contains a postfix number, this number will be used as a semantic index. 
      /// The <see cref="AdamantiumVulkan.Core"/> will be mapped from the field type.
      /// </remarks>
      public VertexInputElementAttribute(String semanticName)
      {
         ParseSemantic(semanticName.ToUpper(), 0);
         Format = Format.UNDEFINED;
         BytesOffset = -1;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexInputElement" /> struct.
      /// </summary>
      /// <param name="semanticName">Name of the semantic.</param>
      /// <param name="format">The format.</param>
      /// <remarks>
      /// If the semantic name contains a postfix number, this number will be used as a semantic index.
      /// </remarks>
      public VertexInputElementAttribute(String semanticName, Format format)
      {
         ParseSemantic(semanticName.ToUpper(), 0);
         Format = format;
         BytesOffset = -1;
      }

      /// <summary>
      /// Initializes a new instance of the <see cref="VertexInputElement" /> struct.
      /// </summary>
      /// <param name="semanticName">Name of the semantic.</param>
      /// <param name="semanticIndex">Index of the semantic.</param>
      /// <param name="format">The format.</param>
      /// <param name="bytesOffset">The aligned byte offset.</param>
      public VertexInputElementAttribute(String semanticName, Int32 semanticIndex, Format format, Int32 bytesOffset = -1)
      {
         ParseSemantic(semanticName.ToUpper(), semanticIndex);
         Format = format;
         BytesOffset = bytesOffset;
      }

      private void ParseSemantic(String semanticName, Int32 semanticIndex)
      {
         var match = VertexInputElement.MatchSemanticIndex.Match(semanticName);
         if (match.Success)
         {
            SemanticName = match.Groups[1].Value;
            SemanticIndex = Int32.Parse(match.Groups[2].Value);
         }
         else
         {
            SemanticName = semanticName.ToUpper();
            SemanticIndex = semanticIndex;
         }
      }
      
   }
}
