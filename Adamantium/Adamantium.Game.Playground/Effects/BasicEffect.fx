float4x4 wvp;
float3 meshColor;
float transparency;
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

float4 BasicColored2_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    return input.color;
}

float median(float a, float b, float c)
{
    return max(min(a,b), min(max(a,b), c));
}

float contour(in float d, in float w) {
    // smoothstep(lower edge0, upper edge1, x)
    return smoothstep(0.5 - w, 0.5 + w, d);
}

float samp(in float2 uv, float w) {
    return contour(shaderTexture.Sample(sampleType, uv).r, w);
}

float4 SDF_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 color;
    float upperPointCutOff = 0.5f;
    float midpointCutOff = 0.49f;
    //float dist = upperPointCutOff - shaderTexture.Sample(sampleType, input.uv).r;
    float dist = shaderTexture.Sample(sampleType, input.uv).a;

    if (dist > upperPointCutOff)
    {
        color = float4(0, 0, 0, 1);
    }
    else if (dist > midpointCutOff)
    {
        float smooth = smoothstep(midpointCutOff, upperPointCutOff, dist);
        color = float4(0, 0, 0, smooth);
    }
    else
    {
        color = float4(0, 0, 0, 0);
    }

    return color;
}

float4 MSDF_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 dist = shaderTexture.Sample(sampleType, input.uv).rgba;
    
    float d = median(dist.r, dist.g, dist.b) - 0.5;

    float w = clamp(d/fwidth(d) + 0.5, 0.0, 1.0);
    
    float4 outside = float4(0, 0, 0, 0);
    float4 inside = float4(0, 0, 0, 1);
    float4 mainColor = lerp(outside, inside, w);    
    //float4 alphaColor = float4(0, 0, 0, dist.a);
    //float4 color = lerp(alphaColor, mainColor, w);
    
    return mainColor;
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
        
    pass Colored2
    {
        Profile = 5.1;
        VertexShader = Basic_VS;
        PixelShader = BasicColored2_PS;
    }
        
    pass SDF
            {
                Profile = 5.1;
                VertexShader = Basic_VS;
                PixelShader = SDF_PS;
            }
    pass MSDF
    {
        Profile = 5.1;
        VertexShader = Basic_VS;
        PixelShader = MSDF_PS;
    }
}