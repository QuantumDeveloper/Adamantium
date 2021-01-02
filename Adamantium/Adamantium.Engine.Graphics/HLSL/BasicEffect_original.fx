Texture2D shaderTexture;
SamplerState sampleType;

cbuffer Matrices : register(b0)
{
    float4x4 worldMatrix;
    float4x4 viewMatrix;
    float4x4 projectionMatrix;
    float4x4 worldInverseTransposeMatrix;
    float4x4 wvp;
    float4x4 Bones[80];
};

cbuffer Light0 : register(b1)
{
    float3 cameraPos;
    float4 globalAmbient;

    float3 LightPosition;
    float3 LightDirection;
    float4 LightColor;
    float4 LightAmbientColor; // intensity multiplier
    float4 LightDiffuseColor; // intensity multiplier
    float4 LightSpecularColor; // intensity multiplier
    float SpotInnerCone; // spot light inner cone (theta) angle
    float SpotOuterCone; // spot light outer cone (phi) angle
    float Radius; // applies to point and spot lights only

    float4 MaterialAmbientColor;
    float4 MaterialDiffuseColor;
    float4 MaterialSpecularColor;
    float4 MaterialEmissiveColor;
    float Shininess;
   
};

//float Transparency = 1.0;

cbuffer ColorBuffer0 : register(b2)
{
    float3 meshColor = float3(1, 1, 1);
    float4 highlightedColor;
    bool texturePresent;
    float transparency;
	float3 sphereCenter;
    float4x4 InverseViewProjection;
    float3 ViewDir;
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

struct SKINNED_MESH_VERTEX
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
    float4 jointIndices : BLENDINDICES0;
    float4 jointWeights : BLENDWEIGHT0;
};

struct VS_OUTPUT_DIR
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 halfVector : TEXCOORD1;
    float3 lightDir : TEXCOORD2;
    float3 normal : TEXCOORD3;
    float4 diffuse : COLOR0;
    float4 specular : COLOR1;
};

struct VS_OUTPUT_POINT
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 normal : TEXCOORD1;
    float3 viewDir : TEXCOORD2;
    float3 lightDir : TEXCOORD3;
    float4 diffuse : COLOR0;
    float4 specular : COLOR1;
    float4 color : COLOR2;
};

struct VS_OUTPUT_SPOT
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float3 viewDir : TEXCOORD1;
    float3 lightDir : TEXCOORD2;
    float3 spotDir : TEXCOORD3;
    float3 normal : TEXCOORD4;
    float4 diffuse : COLOR0;
    float4 specular : COLOR1;
};

struct PS_BASIC
{
    float4 position : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR0;
};

struct VertexPositionNormalTexture
{
    float4 position : SV_POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct VertexPositionColor
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

struct PixelCornerInputType
{
    float4 position : SV_POSITION;
    float4 color : COLOR;
};

float4 CalculatePointLight(VS_OUTPUT_POINT input)
{
    float atten = saturate(1.0f - dot(input.lightDir, input.lightDir));

    float3 n = normalize(LightPosition - input.position);
    float3 l = normalize(input.lightDir);
    float3 v = normalize(input.viewDir);
    float3 h = normalize(l + v);

    float nDotL = saturate(dot(n, l));
    float nDotH = saturate(dot(n, h));
    float power = (nDotL == 0.0f) ? 0.0f : pow(nDotH, Shininess);

    float4 color = (MaterialAmbientColor * (globalAmbient + (atten * LightAmbientColor))) +
      (input.diffuse * nDotL * atten) + (input.specular * power * atten);

    return color;
}

float4 CalculateDirectionalLight(VS_OUTPUT_DIR input)
{
    float3 n = normalize(input.normal);
    float3 l = normalize(input.lightDir);
    float3 h = normalize(input.halfVector);

    float nDotL = saturate(dot(n, l));
    float nDotH = saturate(dot(n, h));
    float power = (nDotL == 0.0) ? 0.0 : pow(nDotH, Shininess);

    float4 color = (MaterialAmbientColor * (globalAmbient + LightAmbientColor)) +
      (input.diffuse * nDotL) + (input.specular * power);

    return color;
}

float4 CalculateSpotLight(VS_OUTPUT_SPOT input)
{
    float atten = saturate(1.0f - dot(input.lightDir, input.lightDir));

    float3 l = normalize(input.lightDir);
    float2 cosAngles = cos(float2(SpotOuterCone, SpotInnerCone) * 0.5f);
    float spotDot = dot(-l, normalize(input.spotDir));
    float spotEffect = smoothstep(cosAngles[0], cosAngles[1], spotDot);

    atten *= spotEffect;

    float3 n = normalize(input.normal);
    float3 v = normalize(input.viewDir);
    float3 h = normalize(l + v);

    float nDotL = saturate(dot(n, l));
    float nDotH = saturate(dot(n, h));
    float power = (nDotL == 0.0) ? 0.0 : pow(nDotH, Shininess);

    float4 color = (MaterialAmbientColor * (globalAmbient + (atten * LightAmbientColor))) +
      (input.diffuse * nDotL * atten) + (input.specular * power * atten);

    return color;
}

////////////////////////////////////////////////////////////////////////////////
// Vertex Shader
////////////////////////////////////////////////////////////////////////////////
VS_OUTPUT_POINT LightVertexShader(SKINNED_MESH_VERTEX input)
{
    VS_OUTPUT_POINT output;
   
    float4 pos = float4(0, 0, 0, 0);
    input.position.w = 1.0f;
   //Bone1
    pos += mul(input.position, Bones[input.jointIndices.x]) * input.jointWeights.x;
   //Bone2
    pos += mul(input.position, Bones[input.jointIndices.y]) * input.jointWeights.y;
   //Bone3
    pos += mul(input.position, Bones[input.jointIndices.z]) * input.jointWeights.z;
   //Bone4
    pos += mul(input.position, Bones[input.jointIndices.w]) * input.jointWeights.w;

   // Change the position vector to be 4 units for proper matrix calculations.
   //input.position.w = 1.0f;
    if (pos.x == 0 && pos.y == 0 && pos.z == 0)
    {
        pos = input.position;
    }
    input.position = pos;

    float3 worldPos = mul(input.position, worldMatrix).xyz;
    output.position = mul(input.position, wvp);

   // Store the texture coordinates for the pixel shader.
    output.uv = input.uv0;
    output.viewDir = cameraPos - worldPos;
    output.lightDir = (LightPosition - worldPos) / Radius;
   // Calculate the normal vector against the inveresed transposed world matrix and normalize result.
    output.normal = mul(input.normal, (float3x3) worldInverseTransposeMatrix);
    output.diffuse = MaterialDiffuseColor * LightColor * LightDiffuseColor;
    output.specular = MaterialSpecularColor * LightColor * LightSpecularColor;
    output.color = input.color;

    return output;
}

VS_OUTPUT_DIR SkinnedMeshVertex(SKINNED_MESH_VERTEX input)
{
    VS_OUTPUT_DIR output;
   // Change the position vector to be 4 units for proper matrix calculations.
    input.position.w = 1.0f;
    float4 skinTransform = float4(0, 0, 0, 0);
   //Bone1
    skinTransform += mul(input.position, Bones[input.jointIndices.x]) * input.jointWeights.x;
   //Bone2
    skinTransform += mul(input.position, Bones[input.jointIndices.y]) * input.jointWeights.y;
   //Bone3
    skinTransform += mul(input.position, Bones[input.jointIndices.z]) * input.jointWeights.z;
   //Bone4
    skinTransform += mul(input.position, Bones[input.jointIndices.w]) * input.jointWeights.w;

    float3 worldPos = mul(skinTransform, worldMatrix).xyz;
    float3 viewDir = cameraPos - worldPos;
    output.position = mul(skinTransform, wvp);

   // Store the texture coordinates for the pixel shader.
    output.uv = input.uv0;
    output.lightDir = -LightDirection;
    output.halfVector = normalize(normalize(output.lightDir) + normalize(viewDir));
   // Calculate the normal vector against the inveresed transposed world matrix and normalize result.
    output.normal = mul(input.normal, (float3x3) worldInverseTransposeMatrix);
    output.diffuse = MaterialDiffuseColor * LightColor * LightDiffuseColor;
    output.specular = MaterialSpecularColor * LightColor * LightSpecularColor;

    return output;
}



VS_OUTPUT_DIR MeshVertexVS(MESH_VERTEX input)
{
    VS_OUTPUT_DIR output;

    input.position.w = 1.0f;
    
    float3 worldPos = mul(input.position, worldMatrix).xyz;
    float3 viewDir = cameraPos - worldPos;
    output.position = mul(input.position, wvp);

   // Store the texture coordinates for the pixel shader.
    output.uv = input.uv0;
    output.lightDir = -LightDirection;
    output.halfVector = normalize(normalize(output.lightDir) + normalize(viewDir));
   // Calculate the normal vector against the inveresed transposed world matrix and normalize result.
    output.normal = mul(input.normal, (float3x3) worldInverseTransposeMatrix);
    output.diffuse = MaterialDiffuseColor * LightColor * LightDiffuseColor;
    output.specular = MaterialSpecularColor * LightColor * LightSpecularColor;

    return output;
}

PS_BASIC VS_NoLight(MESH_VERTEX input)
{
    PS_BASIC output;
   
    input.position.w = 1.0f;
    output.position = mul(input.position, wvp);
    output.uv = input.uv0;
    output.color = input.color;
    return output;
}

float4 NoLightPS(PS_BASIC input) : SV_TARGET
{
    float4 color = float4(meshColor, 1);
    color.a = transparency;
    return color;
}

float4 RotationOrbitsPS(PS_BASIC input) : SV_TARGET
{
    float4 pos = mul(input.position, InverseViewProjection);
    float3 N = pos.xyz - sphereCenter;
	N = normalize(N);
    float result = dot(N, ViewDir);
	if (result < 0.0)
	{
		return 0;
	}
	float4 color = float4(meshColor, 1);
	color.a = transparency;
	return color;
}

VS_OUTPUT_SPOT VS_SpotLighting(SKINNED_MESH_VERTEX input)
{
    VS_OUTPUT_SPOT output;
   
    float4 pos = float4(0, 0, 0, 0);
    input.position.w = 1.0f;
   //Bone1
    pos += mul(input.position, Bones[input.jointIndices.x]) * input.jointWeights.x;
   //Bone2
    pos += mul(input.position, Bones[input.jointIndices.y]) * input.jointWeights.y;
   //Bone3
    pos += mul(input.position, Bones[input.jointIndices.z]) * input.jointWeights.z;
   //Bone4
    pos += mul(input.position, Bones[input.jointIndices.w]) * input.jointWeights.w;

   // Change the position vector to be 4 units for proper matrix calculations.
    if (pos.x == 0 && pos.y == 0 && pos.z == 0)
    {
        pos = input.position;
    }
    input.position = pos;

	//float angle = (TotalTime % 360) * 2;
	//float freqx = 0.4f + sin(TotalTime) * 1.0f;
	//float freqy = 1.0f + sin(TotalTime * 1.3f) * 2.0f;
	//float freqz = 1.1f + sin(TotalTime * 1.1f) * 3.0f;
	//float amp = 1.0f + sin(TotalTime * 1.4) * 10.0f;
    
	//float f = sin(input.normal.x * freqx + TotalTime) * sin(input.normal.y * freqy + TotalTime) * sin(input.normal.z * freqz + TotalTime);
	//input.position.z += input.normal.z * freqz * amp * f;
	//input.position.x += input.normal.x * freqx * amp * f;
	//input.position.y += input.normal.y * freqy * amp * f;

    float3 worldPos = mul(input.position, worldMatrix).xyz;
   
    output.position = mul(input.position, wvp);

    output.viewDir = cameraPos - worldPos;
   // Store the texture coordinates for the pixel shader.
    output.uv = input.uv0;
    output.lightDir = (LightPosition - worldPos) / Radius;
    output.spotDir = LightDirection;
   // Calculate the normal vector against the inveresed transposed world matrix and normalize result.
    output.normal = mul(input.normal, (float3x3) worldInverseTransposeMatrix);
    output.diffuse = MaterialDiffuseColor * LightColor * LightDiffuseColor;
    output.specular = MaterialSpecularColor * LightColor * LightSpecularColor;

    return output;
}

float4 TexturedPixelShader(VS_OUTPUT_POINT input) : SV_TARGET
{
    float4 color = CalculatePointLight(input);

    float4 texel = shaderTexture.Sample(sampleType, input.uv);
    color *= texel;
   //color.a = Transparency;
    return color;
}

float4 NonTexturedPointPS(VS_OUTPUT_POINT input) : SV_TARGET
{
    return CalculatePointLight(input);
}

float4 DirectionalPS(VS_OUTPUT_DIR input) : SV_TARGET
{
    float4 color = CalculateDirectionalLight(input);
    if (texturePresent)
    {
        float4 texel = shaderTexture.Sample(sampleType, input.uv);
        color *= texel;
    }
	color *= float4(meshColor, 1);
    color.a = transparency;
    
    return color;
}

float4 NonTexturedDirectionalPS(VS_OUTPUT_DIR input) : SV_TARGET
{
    return CalculateDirectionalLight(input);
}

float4 TexturedSpotPS(VS_OUTPUT_SPOT input) : SV_TARGET
{
    float4 color = CalculateSpotLight(input);

    float4 texel = shaderTexture.Sample(sampleType, input.uv);
    color *= texel;
   //color.a = Transparency;
    return color;
}

float4 NonTexturedSpotPS(VS_OUTPUT_SPOT input) : SV_TARGET
{
    return CalculateSpotLight(input);
}

technique10 PointLight
{
    pass TexturedMesh
    {
        Profile = 10;
        VertexShader = LightVertexShader;
        PixelShader = TexturedPixelShader;
    }

    pass NonTexturedMesh
    {
        Profile = 10;
        VertexShader = LightVertexShader;
        PixelShader = NonTexturedPointPS;
    }
}

technique10 DirectionalLight
{
    pass TexturedMesh
    {
        Profile = 10;
        VertexShader = SkinnedMeshVertex;
        PixelShader = DirectionalPS;
    }

    pass NonTexturedMesh
    {
        Profile = 10;
        VertexShader = SkinnedMeshVertex;
        PixelShader = NonTexturedDirectionalPS;
    }
};

technique10 SpotLight
{
    pass TexturedMesh
    {
        Profile = 10;
        VertexShader = VS_SpotLighting;
        PixelShader = TexturedSpotPS;
    }

    pass NonTexturedMesh
    {
        Profile = 10;
        VertexShader = VS_SpotLighting;
        PixelShader = NonTexturedSpotPS;
    }
};

technique10 MeshVertex
{
    pass DirectionalLight
    {
        Profile = 11;
        VertexShader = MeshVertexVS;
        PixelShader = DirectionalPS;
    }

    pass Skinned
    {
        Profile = 11;
        VertexShader = SkinnedMeshVertex;
        PixelShader = DirectionalPS;
    }

    pass NoLight
    {
        Profile = 11;
        VertexShader = VS_NoLight;
        PixelShader = NoLightPS;
    }

	pass RotationOrbits
	{
		Profile = 11;
		VertexShader = VS_NoLight;
		PixelShader = RotationOrbitsPS;
	}
};