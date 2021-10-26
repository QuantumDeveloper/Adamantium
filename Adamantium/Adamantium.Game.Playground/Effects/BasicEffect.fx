float4x4 wvp;
float3 meshColor;
float transparency;
//[[vk::binding(1)]] 
sampler sampleType;
//[[vk::binding(2)]] 
Texture2D shaderTexture;
float gamma;
float4 foregroundColor;
float4 backgroundColor;

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

struct MESH_VERTEX
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
    float3 normal : NORMAL;
    float2 uv0 : TEXCOORD0;
    float2 uv1 : TEXCOORD1;
    float2 uv2 : TEXCOORD2;
    float2 uv3 : TEXCOORD3;
    float4 tan : TANGENT;
    float3 biTangent : BINORMAL0;
};

struct PS_OUTPUT_BASIC
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
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


PS_OUTPUT_BASIC Basic_VS(MESH_VERTEX input)
{
    PS_OUTPUT_BASIC output;
   
    input.position.w = 1.0f;
    output.position = mul(input.position, wvp);
    output.uv = input.uv0;
    output.color = input.color;
    return output;
}

float4 BasicColored_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 color = float4(meshColor, 1);
    color.a = transparency;
    return color;
}

float4 BasicVertexColored_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    return input.color;
}

float median(float a, float b, float c)
{
    return max(min(a,b), min(max(a,b), c));
}

float4 MSDF_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float3 sample = shaderTexture.Sample(sampleType, input.uv).rgb;
    int2 sz;
    shaderTexture.GetDimensions(sz.x, sz.y);
    float dx = ddx( input.uv.x ) * sz.x;
    float dy = ddy( input.uv.y ) * sz.y;
    float toPixels = 8.0 * rsqrt( dx * dx + dy * dy );
    float sigDist = median( sample.r, sample.g, sample.b ) - 0.5;
    float opacity = clamp( sigDist * toPixels + 0.5, 0.0, 1.0 );
    
    float4 color = float4(0, 0, 0, opacity);
    
    return color;
}

float4 EncodedToBrightness(float4 encoded)
{
    return pow(encoded, gamma);
}
        
float4 BrightnessToEncoded(float4 brightness)
{
    return pow(brightness, 1.0 / gamma);
}

float4 Subpixel_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 sample = shaderTexture.Sample(sampleType, input.uv);
    
    float4 linearSample = EncodedToBrightness(sample);
    float4 linearForegroundColor = EncodedToBrightness(foregroundColor);
    float4 linearBackgroundColor = EncodedToBrightness(backgroundColor);
    
    float blendedRed   = linearSample.r * linearForegroundColor.r + (1.0 - linearSample.r) * linearBackgroundColor.r;
    float blendedGreen = linearSample.g * linearForegroundColor.g + (1.0 - linearSample.g) * linearBackgroundColor.g;
    float blendedBlue  = linearSample.b * linearForegroundColor.b + (1.0 - linearSample.b) * linearBackgroundColor.b;
    float blendedAlpha = linearSample.a * linearForegroundColor.a + (1.0 - linearSample.a) * linearBackgroundColor.a;
    
    float4 color = BrightnessToEncoded(float4(blendedRed, blendedGreen, blendedBlue, blendedAlpha));
    
    return color;
}

float4 BasicTextured_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 color = shaderTexture.Sample(sampleType, input.uv);
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

technique10 Basic
{
    pass Default
    {
        Profile = 5.1;
        VertexShader = Basic_VS;
        PixelShader = BasicColored_PS;
    }
    
    pass Textured
        {
            Profile = 5.1;
            VertexShader = Basic_VS;
            PixelShader = BasicTextured_PS;
        }
        
    pass Colored
        {
            Profile = 5.1;
            VertexShader = Basic_VS;
            PixelShader = BasicColored_PS;
        }
        
    pass VertexColored
    {
        Profile = 5.1;
        VertexShader = Basic_VS;
        PixelShader = BasicVertexColored_PS;
    }

    pass MSDF
    {
        Profile = 5.1;
        VertexShader = Basic_VS;
        PixelShader = MSDF_PS;
    }
    
    pass Subpixel
    {
        Profile = 5.1;
        VertexShader = Basic_VS;
        PixelShader = Subpixel_PS;
    }
}