float4x4 wvp;
//[[vk::binding(1)]] 
sampler sampleType;
//[[vk::binding(2)]] 
Texture2D shaderTexture;

struct TexturedVertexInputType
{
	float4 position : SV_POSITION;
	float4 color: COLOR;
	float2 texcoord: TEXCOORD;
};

struct TexturedPixelInputType
{
	float4 position : SV_POSITION;
	float2 texcoord: TEXCOORD;
	float4 color: COLOR;
};

TexturedPixelInputType TexturedVertexShader(TexturedVertexInputType input)
{
    TexturedPixelInputType output;
    // Change the position vector to be 4 units for proper matrix calculations.
    output.position = float4(input.position.xyz, 1);
    // Calculate the position of the vertex against the world, view, and projection matrices.
	output.position = mul(output.position, wvp);
	output.texcoord = input.texcoord;
	output.color = input.color;

	return output;
}

float4 TexturedPixelShader(TexturedPixelInputType input) : SV_TARGET
{
   float4 color = shaderTexture.Sample(sampleType, input.texcoord);
   return color;
}

technique10 Render
{
	pass Textured
	{
		Profile = 5.1;
		VertexShader = TexturedVertexShader;
		PixelShader = TexturedPixelShader;
	}
}