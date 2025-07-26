#include "Effects/CommonData.fxh"

float4x4 wvp;
float4x4 world;

float4 fillColor;
sampler sampleType;
Texture2D shaderTexture;

float zNear;
float zFar;
float opacity = 1;

VERTEX_OUTPUT UIVertexShader(UI_VERTEX input)
{
    VERTEX_OUTPUT output;
    output.position = float4(input.position.xyz, 1);
    output.position = mul(output.position, wvp);
	output.normal = normalize(mul(float4(input.normal, 0.0), world).xyz);
	output.uv0 = input.uv0;
	output.uv1 = input.uv1;

	return output;
}

float4 SolidColor_PS(VERTEX_OUTPUT input) : SV_TARGET
{
	float4 result = fillColor;
    result.a = opacity;
    return result;
}

float4 Textured_PS(VERTEX_OUTPUT input) : SV_TARGET
{
   //float4 color = shaderTexture.Sample(sampleType, input.uv0) * fillColor;
   float4 color = shaderTexture.Sample(sampleType, input.uv0);
   
   /*if (color.a < 0.01)
   {
        color = fillColor;
   }*/   
   
   return color;
}

technique Basic
{
	pass SolidColor
	{
		Profile = 5.1;
		VertexShader = UIVertexShader;
		PixelShader = SolidColor_PS;
	}

	pass Textured
	{
		Profile = 5.1;
		VertexShader = UIVertexShader;
		PixelShader = Textured_PS;
	}
}