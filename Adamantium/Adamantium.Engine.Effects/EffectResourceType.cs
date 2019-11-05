using Adamantium.Engine.Core.Effects;

namespace Adamantium.Engine.Effects
{
   /// <summary>
   /// Type of a resource for an <see cref="EffectParameter" />.
   /// </summary>
   public enum EffectResourceType
   {
      /// <summary>
      /// This is not a resource.
      /// </summary>
      None = 0,

      /// <summary>
      /// A Constant Buffer.
      /// </summary>
      ConstantBuffer = 1,

      /// <summary>
      /// A <see cref="ShaderResourceView"/>.
      /// </summary>
      ShaderResourceView = 2,

      /// <summary>
      /// A <see cref="SamplerState"/>.
      /// </summary>
      SamplerState = 3,

      /// <summary>
      /// An <see cref="UnorderedAccessView"/>.
      /// </summary>
      UnorderedAccessView = 4,
   }

   /// <summary>
   /// Parameter type to resource type converter.
   /// </summary>
   internal static class EffectResourceTypeHelper
   {
      private static readonly EffectResourceType[] ParameterTypeToResourceType =
         new EffectResourceType[]
         {
            EffectResourceType.None, // D3D_SVT_VOID = 0
            EffectResourceType.None, // D3D_SVT_BOOL = 1
            EffectResourceType.None, // D3D_SVT_INT = 2
            EffectResourceType.None, // D3D_SVT_FLOAT = 3
            EffectResourceType.None, // D3D_SVT_STRING = 4
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE = 5
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE1D = 6
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE2D = 7
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE3D = 8
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURECUBE = 9
            EffectResourceType.SamplerState, // D3D_SVT_SAMPLER = 10
            EffectResourceType.SamplerState, // D3D_SVT_SAMPLER1D = 11
            EffectResourceType.SamplerState, // D3D_SVT_SAMPLER2D = 12
            EffectResourceType.SamplerState, // D3D_SVT_SAMPLER3D = 13
            EffectResourceType.SamplerState, // D3D_SVT_SAMPLERCUBE = 14
            EffectResourceType.None, // D3D_SVT_PIXELSHADER = 15
            EffectResourceType.None, // D3D_SVT_VERTEXSHADER = 16
            EffectResourceType.None, // D3D_SVT_PIXELFRAGMENT = 17
            EffectResourceType.None, // D3D_SVT_VERTEXFRAGMENT = 18
            EffectResourceType.None, // D3D_SVT_UINT = 19
            EffectResourceType.None, // D3D_SVT_UINT8 = 20
            EffectResourceType.None, // D3D_SVT_GEOMETRYSHADER = 21
            EffectResourceType.None, // D3D_SVT_RASTERIZER = 22
            EffectResourceType.None, // D3D_SVT_DEPTHSTENCIL = 23
            EffectResourceType.None, // D3D_SVT_BLEND = 24
            EffectResourceType.ShaderResourceView, // D3D_SVT_BUFFER = 25
            EffectResourceType.ConstantBuffer, // D3D_SVT_CBUFFER = 26
            EffectResourceType.ConstantBuffer, // D3D_SVT_TBUFFER = 27
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE1DARRAY = 28
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE2DARRAY = 29
            EffectResourceType.None, // D3D_SVT_RENDERTARGETVIEW = 30
            EffectResourceType.None, // D3D_SVT_DEPTHSTENCILVIEW = 31
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE2DMS = 32
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURE2DMSARRAY = 33
            EffectResourceType.ShaderResourceView, // D3D_SVT_TEXTURECUBEARRAY = 34
            EffectResourceType.None, // D3D_SVT_HULLSHADER = 35
            EffectResourceType.None, // D3D_SVT_DOMAINSHADER = 36
            EffectResourceType.None, // D3D_SVT_INTERFACE_POINTER = 37
            EffectResourceType.None, // D3D_SVT_COMPUTESHADER = 38
            EffectResourceType.None, // D3D_SVT_DOUBLE = 39
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWTEXTURE1D = 40
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWTEXTURE1DARRAY = 41
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWTEXTURE2D = 42
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWTEXTURE2DARRAY = 43
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWTEXTURE3D = 44
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWBUFFER = 45
            EffectResourceType.ShaderResourceView, // D3D_SVT_BYTEADDRESS_BUFFER = 46
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWBYTEADDRESS_BUFFER = 47
            EffectResourceType.ShaderResourceView, // D3D_SVT_STRUCTURED_BUFFER = 48
            EffectResourceType.UnorderedAccessView, // D3D_SVT_RWSTRUCTURED_BUFFER = 49
            EffectResourceType.UnorderedAccessView, // D3D_SVT_APPEND_STRUCTURED_BUFFER = 50
            EffectResourceType.UnorderedAccessView, // D3D_SVT_CONSUME_STRUCTURED_BUFFER = 51
         };

      /// <summary>
      /// Converts a <see cref="EffectParameterType"/> to an <see cref="EffectResourceType"/>.
      /// </summary>
      /// <param name="type">The type.</param>
      /// <returns>A resource type.</returns>
      public static EffectResourceType ConvertFromParameterType(EffectParameterType type)
      {
         // For efficiency, don't check for invalid type values.
         return ParameterTypeToResourceType[(int) type];
      }
   }
}
