matrix wvp;

float4 fillColor;
sampler sampleType;
Texture2D shaderTexture;

float zNear;
float zFar;
float transparency = 1;

struct PixelInputType
{
	float4 position : SV_POSITION;
	float4 color: COLOR;
};

struct VertexInputType
{
	float4 position : SV_POSITION;
};

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
};

////////////////////////////////////////////////////////////////////////////////
// Vertex Shader
////////////////////////////////////////////////////////////////////////////////
PixelInputType LightVertexShader(VertexInputType input)
{
	PixelInputType output;
	output.color = float4(0, 0, 0, 0);
	output.position = float4(0, 0, 0, 0);


	// Change the position vector to be 4 units for proper matrix calculations.
	input.position.w = 1.0f;

	// Calculate the position of the vertex against the world, view, and projection matrices.

	output.position.xyz = input.position.xyz;
	//output.position = mul(input.position, wvp);

	//output.position.z = log(zNear*output.position.z + 1) / log(zNear*zFar + 1) * output.position.w;

	//output.color = input.color;

	return output;
}

TexturedPixelInputType TexturedVertexShader(TexturedVertexInputType input)
{
	TexturedPixelInputType output;
	output.position = float4(0, 0, 0, 0);


	// Change the position vector to be 4 units for proper matrix calculations.
	input.position.w = 1.0f;

	// Calculate the position of the vertex against the world, view, and projection matrices.

	output.position = mul(input.position, wvp);

	output.texcoord = input.texcoord;
	return output;
}

float4 LightPixelShader(PixelInputType input) : SV_TARGET
{
   input.color.a = transparency;
   return input.color;
}

float4 SolidColorPixelShader(TexturedPixelInputType input) : SV_TARGET
{
   float4 result = fillColor;
   result.a *= transparency;
   return result;
}

float4 TexturedPixelShader(TexturedPixelInputType input) : SV_TARGET
{
   float4 result = fillColor;
   result.a *= transparency;
   float4 color = shaderTexture.Sample(sampleType, input.texcoord) * result;
   return color;
}


technique10 Render
{
	pass Debug
	{
		Profile = 5.1;
		VertexShader = LightVertexShader;
		PixelShader = LightPixelShader;
	}

	pass SolidColor
	{
		Profile = 5.1;
		VertexShader = TexturedVertexShader;
		PixelShader = SolidColorPixelShader;
	}

	pass Textured
	{
		Profile = 5.1;
		VertexShader = TexturedVertexShader;
		PixelShader = TexturedPixelShader;
	}
}