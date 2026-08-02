#include "Effects/CommonData.fxh"

float4x4 wvp;
float4x4 world;

float4 fillColor;
sampler sampleType;
Texture2D shaderTexture;

// An animation's frames live as LAYERS of one texture, and playing it is choosing a layer - no upload, no allocation and
// no texture per frame. The layer arrives as a plain effect constant (one draw = one frame), which keeps this the
// SIMPLEST non-constant form there is: no VS->PS varying, no branching, a single sample. That matters because this
// driver's shader-object compiler is known to AV on richer Texture2DArray use - see the bisection note in FontEffect.fx,
// where the layer is per-GLYPH and therefore needs a varying.
Texture2DArray shaderTextureArray;
float textureLayer;

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
    result.a *= opacity;   // combine the colour's own alpha with the element/brush opacity (don't drop fillColor.a)
    return result;
}

float4 Textured_PS(VERTEX_OUTPUT input) : SV_TARGET
{
   //float4 color = shaderTexture.Sample(sampleType, input.uv0) * fillColor;
   float4 color = shaderTexture.Sample(sampleType, input.uv0);

   // Apply the control's opacity so a textured element (image / RenderTargetPanel) honours Opacity, like SolidColor_PS.
   color.a *= opacity;

   return color;
}

float4 TexturedArray_PS(VERTEX_OUTPUT input) : SV_TARGET
{
   float4 color = shaderTextureArray.Sample(sampleType, float3(input.uv0, textureLayer));

   color.a *= opacity;

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

	pass TexturedArray
	{
		Profile = 5.1;
		VertexShader = UIVertexShader;
		PixelShader = TexturedArray_PS;
	}
}