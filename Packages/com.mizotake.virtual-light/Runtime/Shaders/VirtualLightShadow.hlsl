#ifndef MIZOT_VIRTUAL_LIGHT_SHADOW_INCLUDED
#define MIZOT_VIRTUAL_LIGHT_SHADOW_INCLUDED

TEXTURE2D_ARRAY(_VirtualLightShadowMaps);
SAMPLER(sampler_VirtualLightShadowMaps);
StructuredBuffer<float4x4> _VirtualLightShadowMatrices;
StructuredBuffer<float4> _VirtualLightShadowLightParams;
StructuredBuffer<float4> _VirtualLightShadowDirections;
uint _VirtualLightShadowCount;
float4 _VirtualLightShadowSamplingParams;

int MizotGetPointShadowFace(float3 directionFromLight)
{
    float3 absoluteDirection = abs(directionFromLight);
    if (absoluteDirection.x >= absoluteDirection.y && absoluteDirection.x >= absoluteDirection.z) return directionFromLight.x >= 0.0 ? 0 : 1;
    if (absoluteDirection.y >= absoluteDirection.z) return directionFromLight.y >= 0.0 ? 2 : 3;
    return directionFromLight.z >= 0.0 ? 4 : 5;
}

float MizotSampleVirtualLightShadow(float shadowSlice, float lightType, float3 lightForwardWS, float3 positionWS, half3 normalWS, half3 lightDirectionWS, bool volumeSample)
{
    int baseSlice = (int)round(shadowSlice);
    if (baseSlice < 0 || baseSlice >= (int)_VirtualLightShadowCount) return 1.0;
    int type = (int)round(lightType);
    float3 lightPosition = _VirtualLightShadowLightParams[baseSlice].xyz;
    int slice = baseSlice;
    if (type == 0) slice += MizotGetPointShadowFace(positionWS - lightPosition);
    else if (type == 2 && dot(positionWS - lightPosition, lightForwardWS) < 0.0) slice++;
    if (slice < 0 || slice >= (int)_VirtualLightShadowCount) return 1.0;
    float4 clip = mul(_VirtualLightShadowMatrices[slice], float4(positionWS, 1.0));
    if (clip.w <= 0.0) return 1.0;
    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
    if (any(uv <= 0.0) || any(uv >= 1.0)) return 1.0;
    float4 lightParams = _VirtualLightShadowLightParams[slice];
    float receiverDepth = type == 2 || type == 3 ? dot(positionWS - lightParams.xyz, _VirtualLightShadowDirections[slice].xyz) * lightParams.w : distance(positionWS, lightParams.xyz) * lightParams.w;
    if (receiverDepth <= 0.0 || receiverDepth >= 1.0) return 1.0;
    float normalFactor = volumeSample ? 0.0 : 1.0 - saturate(dot(normalWS, lightDirectionWS));
    float bias = _VirtualLightShadowSamplingParams.z + _VirtualLightShadowSamplingParams.w * normalFactor;
    if (volumeSample)
    {
        float volumeVisibility = 0.0;
        [unroll]
        for (int volumeY = 0; volumeY < 2; volumeY++)
        {
            [unroll]
            for (int volumeX = 0; volumeX < 2; volumeX++)
            {
                float2 offset = (float2(volumeX, volumeY) - 0.5) * _VirtualLightShadowSamplingParams.xy;
                float2 sampleUV = clamp(uv + offset, _VirtualLightShadowSamplingParams.xy, 1.0 - _VirtualLightShadowSamplingParams.xy);
                float storedDepth = SAMPLE_TEXTURE2D_ARRAY_LOD(_VirtualLightShadowMaps, sampler_VirtualLightShadowMaps, sampleUV, slice, 0).r;
                volumeVisibility += smoothstep(receiverDepth - bias - _VirtualLightShadowSamplingParams.z * 0.5, receiverDepth - bias + _VirtualLightShadowSamplingParams.z * 0.5, storedDepth);
            }
        }
        return volumeVisibility * 0.25;
    }
    float visibility = 0.0;
    [unroll]
    for (int y = -1; y <= 1; y++)
    {
        [unroll]
        for (int x = -1; x <= 1; x++)
        {
            float2 sampleUV = clamp(uv + float2(x, y) * _VirtualLightShadowSamplingParams.xy, _VirtualLightShadowSamplingParams.xy * 1.5, 1.0 - _VirtualLightShadowSamplingParams.xy * 1.5);
            float storedDepth = SAMPLE_TEXTURE2D_ARRAY_LOD(_VirtualLightShadowMaps, sampler_VirtualLightShadowMaps, sampleUV, slice, 0).r;
            float weight = (x == 0 ? 2.0 : 1.0) * (y == 0 ? 2.0 : 1.0);
            visibility += weight * step(receiverDepth - bias, storedDepth);
        }
    }
    return visibility * 0.0625;
}

float MizotSampleVirtualLightShadow(float shadowSlice, float3 positionWS, half3 normalWS, half3 lightDirectionWS, bool volumeSample)
{
    return MizotSampleVirtualLightShadow(shadowSlice, 1.0, float3(0.0, 0.0, 1.0), positionWS, normalWS, lightDirectionWS, volumeSample);
}

#endif
