matrix MatrixTransform;
float4 Color;

Texture2D Texture;
SamplerState TextureSampler: register(s0);

void VS(in float4 inputPosition : POSITION, out float4 position : SV_POSITION, inout float2 texCoord : TEXCOORD0)
{
   position = inputPosition;
   position.w = 1;
   position = mul(position, MatrixTransform);
}

float4 PS(in float4 position : SV_POSITION, in float2 texCoord : TEXCOORD0) : SV_Target0
{
   return Texture.Sample(TextureSampler, texCoord) * Color;
}

technique ScreenQuad
{
   pass Quad
   {
      Profile = 5.1;
      VertexShader = VS;
   }

   pass QuadPS
   {
      Profile = 5.1;
      VertexShader = VS;
      PixelShader = PS;
   }
}
