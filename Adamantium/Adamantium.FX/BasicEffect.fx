float4x4 wvp;
// Rotation for NORMALS. wvp bakes the projection in, and a normal put through that is no longer a direction - lighting
// has to be done in a space the light is fixed in, and for a head-lit gizmo that space is the view.
float4x4 world;
float3 meshColor;
float transparency;

// One draw, many copies of the SAME mesh: each copy's own world matrix and colour, with the projection shared. The
// gizmo's seven balls are one sphere seven times over, its three arms one cylinder, its four arrows one triangle.
// The length is the cap on a single instanced draw - see InstanceCapacity.
float4x4 instanceWorld[64];
float4 instanceColor[64];
float4x4 viewProjection;
// The selection outline's width as a clip-space offset per unit of w, along x and along y.
float2 outlineStep;
sampler sampleType;
Texture2D shaderTexture;
float4 foregroundColor;
float gamma;
float4 backgroundColor;
uint atlasSize;

struct TexturedVertexInputType
{
	float4 position : POSITION;
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
    float4 position : POSITION;
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
    float3 normal : NORMAL;
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
    output.normal = input.normal;
    return output;
}

PS_OUTPUT_BASIC BasicLit_VS(MESH_VERTEX input)
{
    PS_OUTPUT_BASIC output;

    input.position.w = 1.0f;
    output.position = mul(input.position, wvp);
    output.uv = input.uv0;
    output.color = float4(meshColor, transparency);
    output.normal = mul(input.normal, (float3x3)world);
    return output;
}

// The instanced twin: the placement and the colour come from the tables above, everything else is identical. The
// normal rides the copy's OWN matrix, so copies of one mesh may be turned any way and still light correctly.
PS_OUTPUT_BASIC BasicLitInstanced_VS(MESH_VERTEX input, uint instanceId : SV_InstanceID)
{
    PS_OUTPUT_BASIC output;

    float4x4 placement = instanceWorld[instanceId];

    input.position.w = 1.0f;
    output.position = mul(mul(input.position, placement), viewProjection);
    output.uv = input.uv0;
    output.color = instanceColor[instanceId];
    output.normal = mul(input.normal, (float3x3)placement);
    return output;
}

// Flat colour gives a shape no form at all - every face of it reads as one silhouette. Lit from just off the viewer's
// shoulder, so a gizmo shades the same however the view turns: a diffuse term for the body, a tight specular so a ball
// reads as round, and a rim that keeps a dark one off a dark background without an outline pass.
float4 BasicLit_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float3 normal = normalize(input.normal);
    float3 eye = float3(0, 0, -1);
    float3 key = normalize(float3(-0.35f, -0.55f, -1.0f));

    float lambert = saturate(dot(normal, key));
    float3 halfway = normalize(key + eye);
    float specular = pow(saturate(dot(normal, halfway)), 40.0f) * 0.45f;
    float rim = pow(1.0f - saturate(dot(normal, eye)), 3.0f) * 0.35f;

    float3 shade = input.color.rgb * (0.42f + 0.58f * lambert + rim) + specular;
    return float4(saturate(shade), input.color.a);
}

struct PS_OUTPUT_ORBIT
{
    float4 position : SV_POSITION;
    float4 color : COLOR0;
    float3 fromCenter : TEXCOORD0;
    float3 toCenter : TEXCOORD1;
};

// A ring of the rotation gizmo: its copy's center rides the translation row of the copy's matrix, and the eye sits at
// the origin of the camera-relative space the placement is in.
PS_OUTPUT_ORBIT OrbitInstanced_VS(MESH_VERTEX input, uint instanceId : SV_InstanceID)
{
    PS_OUTPUT_ORBIT output;

    float4x4 placement = instanceWorld[instanceId];
    float4 world = mul(float4(input.position.xyz, 1.0f), placement);
    output.position = mul(world, viewProjection);
    output.color = instanceColor[instanceId];
    output.fromCenter = world.xyz - placement[3].xyz;
    output.toCenter = placement[3].xyz;
    return output;
}

// Only the half of the ring on the eye's side of the ball: split across the sight to the center, not the exact
// silhouette, which would leave less than half of a tilted ring and nothing of one that faces the eye.
float4 Orbit_PS(PS_OUTPUT_ORBIT input) : SV_TARGET
{
    if (dot(input.fromCenter, input.toCenter) > 0.0f)
    {
        discard;
    }

    return input.color;
}

// The selection outline: every copy drawn once per direction, shifted that way by the outline's width on screen, and
// the stencil keeps only what lands outside the selection itself. Copy k's placement and colour sit at k / directions.
static const uint outlineDirections = 8;

PS_OUTPUT_BASIC OutlineInstanced_VS(MESH_VERTEX input, uint instanceId : SV_InstanceID)
{
    PS_OUTPUT_BASIC output;

    uint copy = instanceId / outlineDirections;
    float angle = (instanceId % outlineDirections) * (6.2831853f / outlineDirections);
    float4 position = mul(mul(float4(input.position.xyz, 1.0f), instanceWorld[copy]), viewProjection);
    position.xy += float2(cos(angle), sin(angle)) * outlineStep * position.w;

    output.position = position;
    output.uv = input.uv0;
    output.color = instanceColor[copy];
    output.normal = input.normal;
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

float4 SmallGlyph_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float dist = shaderTexture.Sample(sampleType, input.uv).a;

    float blendedAlpha = dist * foregroundColor.a;

    float4 color = float4(foregroundColor.r, foregroundColor.g, foregroundColor.b, blendedAlpha);
    
    return color;
}

float4 LargeGlyph_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 sample = shaderTexture.Sample(sampleType, input.uv).rgba;
    int2 sz;
    shaderTexture.GetDimensions(sz.x, sz.y);
    float dx = ddx( input.uv.x ) * sz.x;
    float dy = ddy( input.uv.y ) * sz.y;
    float toPixels = 5.0 * rsqrt( dx * dx + dy * dy );
    float sigDist = median( sample.r, sample.g, sample.b ) - 0.5;
    float opacity = clamp( sigDist * toPixels + 0.5, 0.0, 1.0 );

    //float4 color = float4(foregroundColor.r, foregroundColor.g, foregroundColor.b, opacity);

    float4 color;
    if (sample.a >= 0.55)
    {
        color = foregroundColor;
    }
    else
    {
        color = float4(foregroundColor.r, foregroundColor.g, foregroundColor.b, opacity);
    }

//float3 sample = shaderTexture.Sample(sampleType, input.uv).rgb;
//    float sigDist = median( sample.r, sample.g, sample.b );
//    
//    float4 color;
//    
//    if (sigDist >= 0.5)
//    {
//        color = foregroundColor;
//    }
//    else
//    {
//        color = float4(0,0,0,0);
//    }
    
    return color;
}

float4 BasicTextured_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float4 color = shaderTexture.Sample(sampleType, input.uv);
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

float GetRebalancedSubpixel(float mostLeft, float leastLeft, float current, float leastRight, float mostRight)
{
    return (mostLeft / 9.0) +
           ((leastLeft * 2.0) / 9.0) +
           ((current * 3.0) / 9.0) +
           ((leastRight * 2.0) / 9.0) +
           (mostRight / 9.0);
}

float4 Subpixel_PS(PS_OUTPUT_BASIC input) : SV_TARGET
{
    float pixelStep = 1.0 / atlasSize;
    float2 leftPos = float2(input.uv.x - pixelStep, input.uv.y);
    float2 rightPos = float2(input.uv.x + pixelStep, input.uv.y);

    float4 leftPixel = shaderTexture.Sample(sampleType, leftPos);
    float4 currentPixel = shaderTexture.Sample(sampleType, input.uv);
    float4 rightPixel = shaderTexture.Sample(sampleType, rightPos);

    leftPixel = EncodedToBrightness(leftPixel);
    currentPixel = EncodedToBrightness(currentPixel);
    rightPixel = EncodedToBrightness(rightPixel);    

    float redDist = GetRebalancedSubpixel(leftPixel.g, leftPixel.b, currentPixel.r, currentPixel.g, currentPixel.b);
    float greenDist = GetRebalancedSubpixel(leftPixel.b, currentPixel.r, currentPixel.g, currentPixel.b, rightPixel.r);
    float blueDist = GetRebalancedSubpixel(currentPixel.r, currentPixel.g, currentPixel.b, rightPixel.r, rightPixel.g);

    float4 linearForegroundColor = EncodedToBrightness(foregroundColor);
    float4 linearBackgroundColor = EncodedToBrightness(backgroundColor);    
    
    float blendedRed   = redDist * linearForegroundColor.r + (1.0 - redDist) * linearBackgroundColor.r;
    float blendedGreen = greenDist * linearForegroundColor.g + (1.0 - greenDist) * linearBackgroundColor.g;
    float blendedBlue  = blueDist * linearForegroundColor.b + (1.0 - blueDist) * linearBackgroundColor.b;
    //float blendedAlpha = currentPixel.a * linearForegroundColor.a + (1.0 - currentPixel.a) * linearBackgroundColor.a;
    
    float4 color = BrightnessToEncoded(float4(blendedRed, blendedGreen, blendedBlue, currentPixel.a));
    
    return color;
}

technique Render
{
	pass Textured
	{
		VertexShader = TexturedVertexShader;
		PixelShader = TexturedPixelShader;
	}
}

technique Basic
{
    pass Default
    {
        VertexShader = Basic_VS;
        PixelShader = BasicColored_PS;
    }
    
    pass Textured
    {
        VertexShader = Basic_VS;
        PixelShader = BasicTextured_PS;
    }
        
    pass Colored
    {
        VertexShader = Basic_VS;
        PixelShader = BasicColored_PS;
    }
        
    pass VertexColored
    {
        VertexShader = Basic_VS;
        PixelShader = BasicVertexColored_PS;
    }

    pass Lit
    {
        VertexShader = BasicLit_VS;
        PixelShader = BasicLit_PS;
    }

    pass LitInstanced
    {
        VertexShader = BasicLitInstanced_VS;
        PixelShader = BasicLit_PS;
    }

    // Lines have no normals to light: flat colour, placed the same way.
    pass FlatInstanced
    {
        VertexShader = BasicLitInstanced_VS;
        PixelShader = BasicVertexColored_PS;
    }

    pass OrbitInstanced
    {
        VertexShader = OrbitInstanced_VS;
        PixelShader = Orbit_PS;
    }

    pass OutlineInstanced
    {
        VertexShader = OutlineInstanced_VS;
        PixelShader = BasicVertexColored_PS;
    }
    
    pass SmallGlyph
    {
        VertexShader = Basic_VS;
        PixelShader = SmallGlyph_PS;
    }
    
    pass LargeGlyph
    {
        VertexShader = Basic_VS;
        PixelShader = LargeGlyph_PS;
    }
}