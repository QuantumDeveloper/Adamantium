// Clustered forward shading. The view is cut into ClusterX x ClusterY tiles on screen and ClusterZ slices in depth,
// spaced logarithmically. AssignLights lists, per cluster, the point and spot lights that reach it; the lit passes shade
// a pixel with its own cluster's list. Directional lights reach everything and are never listed.

static const uint ClusterX = 16;
static const uint ClusterY = 9;
static const uint ClusterZ = 24;
static const uint ClusterCount = ClusterX * ClusterY * ClusterZ;
static const uint MaxLightsPerCluster = 128;

static const uint SpotLight = 1;

// A light in view space. Directional lights come first in the list.
struct ClusterLight
{
    float4 positionRange;       // xyz position, w range
    float4 colorCosOuter;       // rgb color times intensity, w cosine of the outer half-angle
    float4 directionCosInner;   // xyz where it shines, w cosine of the inner half-angle
    uint4 kind;                 // x: 0 point, 1 spot, 2 directional
};

uint64_t LightsAddress;
uint64_t ClusterCountsAddress;
uint64_t ClusterLightsAddress;
uint LightCount;
uint DirectionalCount;
// x near, y far, z slices per unit of log depth, w log(near).
float4 ClusterDepth;
// The inverse of the projection's x and y scales.
float2 InverseScale;
// The viewport in framebuffer pixels: x, y, width, height.
float4 ViewportRect;
float3 Ambient;

float4x4 world;
float4x4 view;
float4x4 viewProjection;
float3 albedo;
sampler sampleType;
Texture2D shaderTexture;

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

struct LIT_PIXEL
{
    float4 position : SV_POSITION;
    float3 viewPosition : TEXCOORD0;
    float3 viewNormal : NORMAL;
    float2 uv : TEXCOORD1;
};

float SliceDepth(uint slice)
{
    return exp(slice / ClusterDepth.z + ClusterDepth.w);
}

[shader("compute")]
[numthreads(64, 1, 1)]
void AssignLightsCS(uint3 tid : SV_DispatchThreadID)
{
    uint cluster = tid.x;
    if (cluster >= ClusterCount)
    {
        return;
    }

    uint x = cluster % ClusterX;
    uint y = (cluster / ClusterX) % ClusterY;
    uint z = cluster / (ClusterX * ClusterY);

    float zNear = SliceDepth(z);
    float zFar = SliceDepth(z + 1);
    float2 a = (float2(x, y) / float2(ClusterX, ClusterY) * 2.0f - 1.0f) * InverseScale;
    float2 b = (float2(x + 1, y + 1) / float2(ClusterX, ClusterY) * 2.0f - 1.0f) * InverseScale;
    float3 boxMin = float3(min(min(a * zNear, a * zFar), min(b * zNear, b * zFar)), zNear);
    float3 boxMax = float3(max(max(a * zNear, a * zFar), max(b * zNear, b * zFar)), zFar);

    ClusterLight* lights = (ClusterLight*)LightsAddress;
    uint* list = (uint*)ClusterLightsAddress;
    uint first = cluster * MaxLightsPerCluster;
    uint count = 0;
    for (uint i = DirectionalCount; i < LightCount; i++)
    {
        float4 sphere = lights[i].positionRange;
        float3 gap = clamp(sphere.xyz, boxMin, boxMax) - sphere.xyz;
        if (dot(gap, gap) <= sphere.w * sphere.w && count < MaxLightsPerCluster)
        {
            list[first + count] = i;
            count++;
        }
    }

    uint* counts = (uint*)ClusterCountsAddress;
    counts[cluster] = count;
}

uint ClusterAt(float2 pixel, float depth)
{
    float2 place = saturate((pixel - ViewportRect.xy) / ViewportRect.zw);
    uint x = min((uint)(place.x * ClusterX), ClusterX - 1);
    uint y = min((uint)(place.y * ClusterY), ClusterY - 1);
    float slice = (log(max(depth, ClusterDepth.x)) - ClusterDepth.w) * ClusterDepth.z;
    uint z = min((uint)max(slice, 0.0f), ClusterZ - 1);
    return x + y * ClusterX + z * ClusterX * ClusterY;
}

float3 BlinnPhong(float3 surface, float3 normal, float3 toLight, float3 toEye)
{
    float diffuse = saturate(dot(normal, toLight));
    float specular = diffuse > 0.0f ? pow(saturate(dot(normal, normalize(toLight + toEye))), 32.0f) * 0.25f : 0.0f;
    return surface * diffuse + specular;
}

float3 Shade(float3 surface, float3 position, float3 normal, float2 pixel)
{
    float3 toEye = normalize(-position);
    float3 n = dot(normal, normal) > 1e-8f ? normalize(normal) : toEye;
    if (dot(n, toEye) < 0.0f)
    {
        n = -n;
    }

    ClusterLight* lights = (ClusterLight*)LightsAddress;
    float3 lit = surface * Ambient;
    for (uint i = 0; i < DirectionalCount; i++)
    {
        lit += BlinnPhong(surface, n, -lights[i].directionCosInner.xyz, toEye) * lights[i].colorCosOuter.rgb;
    }

    uint cluster = ClusterAt(pixel, position.z);
    uint* counts = (uint*)ClusterCountsAddress;
    uint* list = (uint*)ClusterLightsAddress;
    uint count = counts[cluster];
    for (uint k = 0; k < count; k++)
    {
        ClusterLight light = lights[list[cluster * MaxLightsPerCluster + k]];
        float3 toLight = light.positionRange.xyz - position;
        float distance = length(toLight);
        float3 l = toLight / max(distance, 1e-4f);
        float reach = saturate(1.0f - (distance * distance) / (light.positionRange.w * light.positionRange.w));
        float fade = reach * reach;
        if (light.kind.x == SpotLight)
        {
            fade *= smoothstep(light.colorCosOuter.w, light.directionCosInner.w, dot(-l, light.directionCosInner.xyz));
        }

        lit += BlinnPhong(surface, n, l, toEye) * light.colorCosOuter.rgb * fade;
    }

    return lit;
}

LIT_PIXEL Lit_VS(MESH_VERTEX input)
{
    LIT_PIXEL output;
    float4 placed = mul(float4(input.position.xyz, 1.0f), world);
    output.position = mul(placed, viewProjection);
    output.viewPosition = mul(placed, view).xyz;
    output.viewNormal = mul(mul(input.normal, (float3x3)world), (float3x3)view);
    output.uv = input.uv0;
    return output;
}

float4 LitColored_PS(LIT_PIXEL input) : SV_TARGET
{
    return float4(Shade(albedo, input.viewPosition, input.viewNormal, input.position.xy), 1.0f);
}

float4 LitTextured_PS(LIT_PIXEL input) : SV_TARGET
{
    float4 surface = shaderTexture.Sample(sampleType, input.uv);
    return float4(Shade(surface.rgb, input.viewPosition, input.viewNormal, input.position.xy), surface.a);
}

technique ForwardPlus
{
    pass AssignLights
    {
        ComputeShader = AssignLightsCS;
    }

    pass Colored
    {
        VertexShader = Lit_VS;
        PixelShader = LitColored_PS;
    }

    pass Textured
    {
        VertexShader = Lit_VS;
        PixelShader = LitTextured_PS;
    }
}
