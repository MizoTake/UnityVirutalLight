#ifndef MIZOT_VIRTUAL_LIGHT_SHADOW_INCLUDED
#define MIZOT_VIRTUAL_LIGHT_SHADOW_INCLUDED

TEXTURE2D_ARRAY(_VirtualLightShadowMaps);
SAMPLER(sampler_VirtualLightShadowMaps);
StructuredBuffer<float4x4> _VirtualLightShadowMatrices;
StructuredBuffer<float4> _VirtualLightShadowLightParams;
uint _VirtualLightShadowCount;
float4 _VirtualLightShadowSamplingParams;

float MizotSampleVirtualLightShadow(float shadowSlice, float3 positionWS, half3 normalWS, half3 lightDirectionWS, bool volumeSample)
{
    int slice = (int)round(shadowSlice);
    if (slice < 0 || slice >= (int)_VirtualLightShadowCount) return 1.0;
    float4 clip = mul(_VirtualLightShadowMatrices[slice], float4(positionWS, 1.0));
    if (clip.w <= 0.0) return 1.0;
    float2 uv = clip.xy / clip.w * 0.5 + 0.5;
    if (any(uv <= 0.0) || any(uv >= 1.0)) return 1.0;
    float4 lightParams = _VirtualLightShadowLightParams[slice];
    float receiverDepth = distance(positionWS, lightParams.xyz) * lightParams.w;
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

#endif
