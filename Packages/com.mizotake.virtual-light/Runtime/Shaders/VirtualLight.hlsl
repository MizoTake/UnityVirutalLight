#ifndef MIZOT_VIRTUAL_LIGHT_INCLUDED
#define MIZOT_VIRTUAL_LIGHT_INCLUDED

#ifndef UNIVERSAL_LIGHTING_INCLUDED
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif
#ifndef MIZOT_VIRTUAL_LIGHT_DISABLE_SHADOWS
#include "Packages/com.mizotake.virtual-light/Runtime/Shaders/VirtualLightShadow.hlsl"
#endif

struct VirtualLightGpu
{
    float4 positionRadius;
    float4 colorIntensity;
    float4 directionType;
    float4 coneShadowFlags;
    float4 areaSizeParams;
};

StructuredBuffer<VirtualLightGpu> _VirtualLights;
StructuredBuffer<uint> _VirtualLightTileCounts;
StructuredBuffer<uint> _VirtualLightTileIndices;
uint _VirtualLightCount;
uint _VirtualLightUseTiling;
float4 _VirtualLightTileParams;

VirtualLightGpu MizotLoadVirtualLight(uint index)
{
    return _VirtualLights[index];
}

void MizotGetVirtualLightBasis(float3 forward, float rotation, out float3 right, out float3 up)
{
    float3 basisSeed = abs(forward.y) < 0.99 ? float3(0.0, 1.0, 0.0) : float3(1.0, 0.0, 0.0);
    float3 referenceRight = SafeNormalize(cross(basisSeed, forward));
    float3 referenceUp = SafeNormalize(cross(forward, referenceRight));
    float rotationCosine = cos(rotation);
    float rotationSine = sin(rotation);
    right = referenceRight * rotationCosine + referenceUp * rotationSine;
    up = referenceUp * rotationCosine - referenceRight * rotationSine;
}

float MizotRangeAttenuation(float distanceToLight, float rangeDistance, float radius)
{
    float distanceSquared = distanceToLight * distanceToLight;
    float normalizedDistance = rangeDistance / max(radius, 1e-4);
    float rangeWindow = saturate(1.0 - normalizedDistance * normalizedDistance * normalizedDistance * normalizedDistance);
    return rangeWindow * rangeWindow * rcp(max(distanceSquared, 1e-4));
}

float MizotSpotPenumbraAttenuation(float angularAttenuation, float sharpness)
{
    float standardAttenuation = angularAttenuation * angularAttenuation;
    float focusedAttenuation = standardAttenuation * standardAttenuation;
    focusedAttenuation *= focusedAttenuation;
    return lerp(standardAttenuation, focusedAttenuation, saturate(sharpness));
}

float3 MizotEvaluateSample(VirtualLightGpu light, float3 samplePosition, float sampleIntensity, float directionalAttenuation, float rangeDistance, BRDFData brdfData, BRDFData clearCoatBrdfData, half clearCoatMask, float3 positionWS, half3 normalWS, half3 viewDirectionWS)
{
    float3 toLight = samplePosition - positionWS;
    float distanceToLight = length(toLight);
    half3 lightDirection = SafeNormalize(toLight);
    if (rangeDistance < 0.0) rangeDistance = distanceToLight;
    float attenuation = MizotRangeAttenuation(distanceToLight, rangeDistance, light.positionRadius.w) * directionalAttenuation;
    half3 lightColor = light.colorIntensity.rgb * sampleIntensity;
#if defined(_SPECULARHIGHLIGHTS_OFF)
    return LightingPhysicallyBased(brdfData, clearCoatBrdfData, lightColor, lightDirection, attenuation, normalWS, viewDirectionWS, clearCoatMask, true);
#else
    return LightingPhysicallyBased(brdfData, clearCoatBrdfData, lightColor, lightDirection, attenuation, normalWS, viewDirectionWS, clearCoatMask, false);
#endif
}

float3 MizotEvaluateDirectionalLight(VirtualLightGpu light, BRDFData brdfData, BRDFData clearCoatBrdfData, half clearCoatMask, half3 normalWS, half3 viewDirectionWS)
{
    half3 lightDirection = -SafeNormalize(light.directionType.xyz);
    half3 lightColor = light.colorIntensity.rgb * light.colorIntensity.w;
#if defined(_SPECULARHIGHLIGHTS_OFF)
    return LightingPhysicallyBased(brdfData, clearCoatBrdfData, lightColor, lightDirection, 1.0, normalWS, viewDirectionWS, clearCoatMask, true);
#else
    return LightingPhysicallyBased(brdfData, clearCoatBrdfData, lightColor, lightDirection, 1.0, normalWS, viewDirectionWS, clearCoatMask, false);
#endif
}

float3 MizotEvaluateLight(VirtualLightGpu light, BRDFData brdfData, BRDFData clearCoatBrdfData, half clearCoatMask, float3 positionWS, half3 normalWS, half3 viewDirectionWS)
{
    uint flags = (uint)round(light.coneShadowFlags.w);
    uint lightType = (uint)round(light.directionType.w);
    if ((flags & 1u) == 0u || (flags & 4u) == 0u || light.colorIntensity.w <= 0.0 || (lightType != 3u && light.positionRadius.w <= 0.0)) return 0.0;
    float3 lightForward = SafeNormalize(light.directionType.xyz);
    uint punctualShape = (uint)round(light.areaSizeParams.y);
    if (lightType == 0u)
    {
        float3 offsetFromLight = positionWS - light.positionRadius.xyz;
        float rangeDistance = -1.0;
        if (punctualShape == 1u)
        {
            float3 shapeRight;
            float3 shapeUp;
            MizotGetVirtualLightBasis(lightForward, light.areaSizeParams.w, shapeRight, shapeUp);
            rangeDistance = max(abs(dot(offsetFromLight, shapeRight)), max(abs(dot(offsetFromLight, shapeUp)), abs(dot(offsetFromLight, lightForward))));
        }
        return MizotEvaluateSample(light, light.positionRadius.xyz, light.colorIntensity.w, 1.0, rangeDistance, brdfData, clearCoatBrdfData, clearCoatMask, positionWS, normalWS, viewDirectionWS);
    }
    if (lightType == 1u)
    {
        float3 directionFromLight = SafeNormalize(positionWS - light.positionRadius.xyz);
        float angularCosine = dot(lightForward, directionFromLight);
        if (punctualShape == 1u)
        {
            float3 shapeRight;
            float3 shapeUp;
            MizotGetVirtualLightBasis(lightForward, light.areaSizeParams.w, shapeRight, shapeUp);
            float forwardProjection = dot(lightForward, directionFromLight);
            float lateralProjection = max(abs(dot(shapeRight, directionFromLight)), abs(dot(shapeUp, directionFromLight)));
            angularCosine = forwardProjection * rsqrt(max(forwardProjection * forwardProjection + lateralProjection * lateralProjection, 1e-8));
        }
        float angleAttenuation = saturate((angularCosine - light.coneShadowFlags.y) / max(light.coneShadowFlags.x - light.coneShadowFlags.y, 1e-5));
        float spotAttenuation = MizotSpotPenumbraAttenuation(angleAttenuation, light.areaSizeParams.x);
#if !defined(MIZOT_VIRTUAL_LIGHT_DISABLE_SHADOWS) && !defined(_RECEIVE_SHADOWS_OFF)
        spotAttenuation *= MizotSampleVirtualLightShadow(light.coneShadowFlags.z, positionWS, normalWS, SafeNormalize(light.positionRadius.xyz - positionWS), false);
#endif
        return MizotEvaluateSample(light, light.positionRadius.xyz, light.colorIntensity.w, spotAttenuation, -1.0, brdfData, clearCoatBrdfData, clearCoatMask, positionWS, normalWS, viewDirectionWS);
    }
    if (lightType == 3u) return MizotEvaluateDirectionalLight(light, brdfData, clearCoatBrdfData, clearCoatMask, normalWS, viewDirectionWS);
    if (lightType != 2u) return 0.0;
    uint sampleCount = clamp((uint)round(light.areaSizeParams.z), 1u, 16u);
    bool horizontal = light.areaSizeParams.x >= light.areaSizeParams.y;
    uint gridWidth = sampleCount == 1u ? 1u : sampleCount == 2u ? (horizontal ? 2u : 1u) : sampleCount == 4u ? 2u : sampleCount == 8u ? (horizontal ? 4u : 2u) : 4u;
    uint gridHeight = sampleCount / gridWidth;
    float3 rotatedRight;
    float3 rotatedUp;
    MizotGetVirtualLightBasis(lightForward, light.areaSizeParams.w, rotatedRight, rotatedUp);
    float sampleArea = light.areaSizeParams.x * light.areaSizeParams.y / sampleCount;
    float3 result = 0.0;
    [loop]
    for (uint sampleIndex = 0u; sampleIndex < sampleCount; sampleIndex++)
    {
        uint x = sampleIndex % gridWidth;
        uint y = sampleIndex / gridWidth;
        float2 unitOffset = (float2(x, y) + 0.5) / float2(gridWidth, gridHeight) - 0.5;
        float3 samplePosition = light.positionRadius.xyz + rotatedRight * unitOffset.x * light.areaSizeParams.x + rotatedUp * unitOffset.y * light.areaSizeParams.y;
        float emissionCosine = dot(lightForward, SafeNormalize(positionWS - samplePosition));
        float directionalAttenuation = (flags & 256u) != 0u ? abs(emissionCosine) : saturate(emissionCosine);
        result += MizotEvaluateSample(light, samplePosition, light.colorIntensity.w * sampleArea, directionalAttenuation, -1.0, brdfData, clearCoatBrdfData, clearCoatMask, positionWS, normalWS, viewDirectionWS);
    }
    return result;
}

float3 MizotEvaluateVirtualLights(BRDFData brdfData, BRDFData clearCoatBrdfData, half clearCoatMask, float3 positionWS, half3 normalWS, half3 viewDirectionWS, float2 normalizedScreenUV)
{
    float3 result = 0.0;
    uint firstIndex = 0u;
    uint lightCount = _VirtualLightCount;
    if (_VirtualLightUseTiling == 1u)
    {
        uint2 tileCoordinates = min((uint2)(normalizedScreenUV * _ScreenParams.xy / max(_VirtualLightTileParams.z, 1.0)), (uint2)_VirtualLightTileParams.xy - 1u);
        uint tileIndex = tileCoordinates.y * (uint)_VirtualLightTileParams.x + tileCoordinates.x;
        lightCount = min(_VirtualLightTileCounts[tileIndex], (uint)_VirtualLightTileParams.w);
        firstIndex = tileIndex * (uint)_VirtualLightTileParams.w;
    }
    [loop]
    for (uint localIndex = 0u; localIndex < lightCount; localIndex++)
    {
        uint lightIndex = _VirtualLightUseTiling == 1u ? _VirtualLightTileIndices[firstIndex + localIndex] : localIndex;
        result += MizotEvaluateLight(MizotLoadVirtualLight(lightIndex), brdfData, clearCoatBrdfData, clearCoatMask, positionWS, normalWS, viewDirectionWS);
    }
    return result;
}

void VirtualLight_float(float3 PositionWS, float3 NormalWS, float3 ViewDirectionWS, float3 Albedo, float Metallic, float Smoothness, float2 ScreenUV, out float3 Lighting)
{
    half alpha = 1.0h;
    BRDFData brdfData;
    InitializeBRDFData((half3)Albedo, saturate(Metallic), half3(0.0h, 0.0h, 0.0h), saturate(Smoothness), alpha, brdfData);
    const BRDFData noClearCoat = (BRDFData)0;
    Lighting = MizotEvaluateVirtualLights(brdfData, noClearCoat, 0.0h, PositionWS, NormalizeNormalPerPixel(NormalWS), SafeNormalize(ViewDirectionWS), ScreenUV);
}

#endif
