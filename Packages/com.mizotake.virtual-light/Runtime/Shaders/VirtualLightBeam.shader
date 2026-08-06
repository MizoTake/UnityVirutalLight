Shader "MizoTake/Virtual Light/Beam"
{
    Properties
    {
        [HDR] _Color("Scattering Color", Color) = (0.05, 0.8, 2.0, 0.08)
        _Density("Extinction Density", Range(0.001, 1)) = 0.12
        _SingleScatteringAlbedo("Single Scattering Albedo", Range(0, 1)) = 0.92
        [Min(0)] _ScatteringIntensity("Source Intensity", Float) = 22
        _DistanceFalloff("Distance Falloff", Range(0, 2)) = 0.5
        _CoreStrength("Core Strength", Range(1, 12)) = 2.2
        _CoreRadius("Core Half-Width", Range(0.02, 0.95)) = 0.32
        _CoreWhiteMix("Hot Core Mix", Range(0, 1)) = 0
        _EdgeExponent("Edge Softness", Range(0.5, 8)) = 2.5
        _EdgeStart("Beam Plateau", Range(0, 0.95)) = 0.52
        _SourceRadius("Source Aperture Radius (m)", Range(0.001, 1)) = 0.08
        _SourceFade("Source Fade", Range(0.001, 0.3)) = 0.04
        _EndFade("End Fade", Range(0.001, 0.4)) = 0.12
        _NoiseAmount("Haze Variation", Range(0, 0.4)) = 0.1
        _NoiseScale("Haze Scale", Range(0.05, 4)) = 0.55
        _NoiseSpeed("Haze Drift", Range(0, 2)) = 0.12
        _Anisotropy("Forward Scattering", Range(-0.9, 0.9)) = 0.45
        _WideAngleScatter("Isotropic Scattering Fraction", Range(0, 1)) = 0.25
        _IntersectionSoftness("Intersection Softness", Range(0.01, 1)) = 0.2
        [PerRendererData] _VirtualLightShadowSlice("Shadow Slice", Float) = -1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-50" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "BeamVolume"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            ZTest Always
            Cull Front
            ColorMask RGB
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 4.5
            #pragma shader_feature_local _BEAM_QUALITY_LOW _BEAM_QUALITY_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.mizotake.virtual-light/Runtime/Shaders/VirtualLightShadow.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Density;
                half _SingleScatteringAlbedo;
                half _ScatteringIntensity;
                half _DistanceFalloff;
                half _CoreStrength;
                half _CoreRadius;
                half _CoreWhiteMix;
                half _EdgeExponent;
                half _EdgeStart;
                half _SourceRadius;
                half _SourceFade;
                half _EndFade;
                half _NoiseAmount;
                half _NoiseScale;
                half _NoiseSpeed;
                half _Anisotropy;
                half _WideAngleScatter;
                half _IntersectionSoftness;
                float _VirtualLightShadowSlice;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionOS = input.positionOS.xyz;
                return output;
            }

            float2 IntersectUnitBox(float3 rayOrigin, float3 rayDirection)
            {
                float3 safeDirection = rayDirection + (1.0 - step(1e-5, abs(rayDirection))) * 1e-5;
                float3 inverseDirection = rcp(safeDirection);
                float3 first = (-0.5 - rayOrigin) * inverseDirection;
                float3 second = (0.5 - rayOrigin) * inverseDirection;
                float3 minimum = min(first, second);
                float3 maximum = max(first, second);
                return float2(max(max(minimum.x, minimum.y), minimum.z), min(min(maximum.x, maximum.y), maximum.z));
            }

            float BeamRadius(float axial, float sourceRadius)
            {
                return lerp(sourceRadius, 0.5, saturate(axial));
            }

            void IncludeBeamIntersection(float candidate, float2 boxIntersection, float3 rayOrigin, float3 rayDirection, float sourceRadius, inout float2 beamIntersection)
            {
                if (candidate < boxIntersection.x - 1e-4 || candidate > boxIntersection.y + 1e-4) return;
                float3 position = rayOrigin + rayDirection * candidate;
                float axial = position.z + 0.5;
                if (axial < -1e-4 || axial > 1.0001) return;
                float radius = BeamRadius(axial, sourceRadius);
                if (dot(position.xy, position.xy) > radius * radius + 1e-4) return;
                beamIntersection.x = min(beamIntersection.x, candidate);
                beamIntersection.y = max(beamIntersection.y, candidate);
            }

            float2 IntersectUnitBeamFrustum(float3 rayOrigin, float3 rayDirection, float sourceRadius)
            {
                float2 boxIntersection = IntersectUnitBox(rayOrigin, rayDirection);
                if (boxIntersection.y <= boxIntersection.x) return float2(1.0, 0.0);
                float boxLength = boxIntersection.y - boxIntersection.x;
                float3 shiftedOrigin = rayOrigin + rayDirection * boxIntersection.x;
                float2 shiftedBoxIntersection = float2(0.0, boxLength);
                float2 shiftedBeamIntersection = float2(boxLength, 0.0);
                IncludeBeamIntersection(0.0, shiftedBoxIntersection, shiftedOrigin, rayDirection, sourceRadius, shiftedBeamIntersection);
                IncludeBeamIntersection(boxLength, shiftedBoxIntersection, shiftedOrigin, rayDirection, sourceRadius, shiftedBeamIntersection);
                float radiusSlope = 0.5 - sourceRadius;
                float radiusAtOrigin = sourceRadius + radiusSlope * (shiftedOrigin.z + 0.5);
                float quadraticA = dot(rayDirection.xy, rayDirection.xy) - radiusSlope * radiusSlope * rayDirection.z * rayDirection.z;
                float quadraticB = 2.0 * (dot(shiftedOrigin.xy, rayDirection.xy) - radiusAtOrigin * radiusSlope * rayDirection.z);
                float quadraticC = dot(shiftedOrigin.xy, shiftedOrigin.xy) - radiusAtOrigin * radiusAtOrigin;
                if (abs(quadraticA) <= 1e-6)
                {
                    if (abs(quadraticB) > 1e-6) IncludeBeamIntersection(-quadraticC / quadraticB, shiftedBoxIntersection, shiftedOrigin, rayDirection, sourceRadius, shiftedBeamIntersection);
                }
                else
                {
                    float discriminant = quadraticB * quadraticB - 4.0 * quadraticA * quadraticC;
                    if (discriminant >= 0.0)
                    {
                        float squareRoot = sqrt(discriminant);
                        float stableRoot = -0.5 * (quadraticB + (quadraticB >= 0.0 ? squareRoot : -squareRoot));
                        if (abs(stableRoot) > 1e-6)
                        {
                            IncludeBeamIntersection(stableRoot / quadraticA, shiftedBoxIntersection, shiftedOrigin, rayDirection, sourceRadius, shiftedBeamIntersection);
                            IncludeBeamIntersection(quadraticC / stableRoot, shiftedBoxIntersection, shiftedOrigin, rayDirection, sourceRadius, shiftedBeamIntersection);
                        }
                        else
                        {
                            IncludeBeamIntersection(-quadraticB / (2.0 * quadraticA), shiftedBoxIntersection, shiftedOrigin, rayDirection, sourceRadius, shiftedBeamIntersection);
                        }
                    }
                }
                return shiftedBeamIntersection + boxIntersection.x;
            }

            float HashCell(float3 position)
            {
                position = frac(position * 0.1031);
                position += dot(position, position.yzx + 33.33);
                return frac((position.x + position.y) * position.z);
            }

            float SmoothValueNoise(float3 position)
            {
                float3 cell = floor(position);
                float3 fraction = frac(position);
                fraction = fraction * fraction * (3.0 - 2.0 * fraction);
                float lower00 = lerp(HashCell(cell), HashCell(cell + float3(1.0, 0.0, 0.0)), fraction.x);
                float lower10 = lerp(HashCell(cell + float3(0.0, 1.0, 0.0)), HashCell(cell + float3(1.0, 1.0, 0.0)), fraction.x);
                float upper00 = lerp(HashCell(cell + float3(0.0, 0.0, 1.0)), HashCell(cell + float3(1.0, 0.0, 1.0)), fraction.x);
                float upper10 = lerp(HashCell(cell + float3(0.0, 1.0, 1.0)), HashCell(cell + float3(1.0, 1.0, 1.0)), fraction.x);
                return lerp(lerp(lower00, lower10, fraction.y), lerp(upper00, upper10, fraction.y), fraction.z);
            }

            float StableRaymarchJitter(float2 pixelPosition)
            {
                return frac(52.9829189 * frac(dot(floor(pixelPosition), float2(0.06711056, 0.00583715))));
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                #if UNITY_REVERSED_Z
                    float rawNearDepth = 1.0;
                    float rawFarDepth = 0.0;
                #else
                    float rawNearDepth = UNITY_NEAR_CLIP_VALUE;
                    float rawFarDepth = 1.0;
                #endif
                float3 cameraWS = GetCameraPositionWS();
                float3 nearWS = ComputeWorldSpacePosition(screenUV, rawNearDepth, UNITY_MATRIX_I_VP);
                float3 farWS = ComputeWorldSpacePosition(screenUV, rawFarDepth, UNITY_MATRIX_I_VP);
                float3 rayOriginWS = lerp(cameraWS, nearWS, unity_OrthoParams.w);
                float3 rayDirectionWS = SafeNormalize(farWS - nearWS);
                float3 rayOriginOS = TransformWorldToObject(rayOriginWS);
                float3 rayDirectionOS = TransformWorldToObjectDir(rayDirectionWS, true);
                float radialScaleWS = max((length(mul((float3x3)UNITY_MATRIX_M, float3(1.0, 0.0, 0.0))) + length(mul((float3x3)UNITY_MATRIX_M, float3(0.0, 1.0, 0.0)))) * 0.5, 1e-5);
                float sourceRadiusOS = clamp((float)_SourceRadius / radialScaleWS, 0.001, 0.49);
                float2 intersection = IntersectUnitBeamFrustum(rayOriginOS, rayDirectionOS, sourceRadiusOS);
                float nearDistance = max(intersection.x, 0.0);
                float farDistance = intersection.y;
                clip(farDistance - nearDistance);
                float sceneDepth = SampleSceneDepth(screenUV);
                #if !UNITY_REVERSED_Z
                    sceneDepth = lerp(UNITY_NEAR_CLIP_VALUE, 1.0, sceneDepth);
                #endif
                float3 sceneWS = ComputeWorldSpacePosition(screenUV, sceneDepth, UNITY_MATRIX_I_VP);
                float3 sceneOS = TransformWorldToObject(sceneWS);
                float sceneDistance = dot(sceneOS - rayOriginOS, rayDirectionOS);
                farDistance = min(farDistance, sceneDistance);
                clip(farDistance - nearDistance);
                float3 entryOS = rayOriginOS + rayDirectionOS * nearDistance;
                float3 exitOS = rayOriginOS + rayDirectionOS * farDistance;
                float3 entryWS = TransformObjectToWorld(entryOS);
                float3 exitWS = TransformObjectToWorld(exitOS);
                float3 sourceWS = TransformObjectToWorld(float3(0.0, 0.0, -0.5));
                float sceneDistanceWS = dot(sceneWS - rayOriginWS, rayDirectionWS);
                float segmentLengthWS = distance(entryWS, exitWS);

                #if defined(_BEAM_QUALITY_LOW)
                    #define MIZOT_BEAM_STEPS 12
                #elif defined(_BEAM_QUALITY_HIGH)
                    #define MIZOT_BEAM_STEPS 32
                #else
                    #define MIZOT_BEAM_STEPS 20
                #endif

                float stepLengthWS = segmentLengthWS / MIZOT_BEAM_STEPS;
                float transmittance = 1.0;
                float scattering = 0.0;
                float hotCoreScattering = 0.0;
                float sampleJitter = StableRaymarchJitter(input.positionCS.xy);
                [unroll]
                for (int sampleIndex = 0; sampleIndex < MIZOT_BEAM_STEPS; sampleIndex++)
                {
                    float progress = (sampleIndex + 0.1 + sampleJitter * 0.8) / MIZOT_BEAM_STEPS;
                    float3 sampleOS = lerp(entryOS, exitOS, progress);
                    float axial = saturate(sampleOS.z + 0.5);
                    float coneRadius = BeamRadius(axial, sourceRadiusOS);
                    float radial = length(sampleOS.xy) / coneRadius;
                    float radialProfile = pow(saturate(1.0 - smoothstep(_EdgeStart, 1.0, radial)), rcp(max(_EdgeExponent, 0.01)));
                    float sourceFade = smoothstep(0.0, _SourceFade, axial);
                    float endFade = 1.0 - smoothstep(1.0 - _EndFade, 1.0, axial);
                    float3 sampleWS = TransformObjectToWorld(sampleOS);
                    float noise = lerp(1.0 - _NoiseAmount, 1.0 + _NoiseAmount, SmoothValueNoise(sampleWS * _NoiseScale + float3(0.0, _Time.y * _NoiseSpeed, _Time.y * _NoiseSpeed * 0.37)));
                    float coreProfile = exp2(-pow(radial / max(_CoreRadius, 0.01), 2.0));
                    float core = lerp(1.0, _CoreStrength, coreProfile);
                    float beamEnvelope = radialProfile * sourceFade * endFade;
                    float sigmaT = _Density * noise;
                    float sigmaS = sigmaT * _SingleScatteringAlbedo;
                    float stepTransmittance = exp(-sigmaT * stepLengthWS);
                    float scatterWeight = sigmaS / max(sigmaT, 1e-5) * (1.0 - stepTransmittance);
                    float sourceDistanceWS = distance(sourceWS, sampleWS);
                    float sourceTransmittance = exp(-_Density * sourceDistanceWS);
                    float sourceFalloff = pow(rcp(max(sourceDistanceWS, 0.5)), _DistanceFalloff);
                    float depthFade = saturate((sceneDistanceWS - dot(sampleWS - rayOriginWS, rayDirectionWS)) / max(_IntersectionSoftness, 0.001));
                    float shadowVisibility = MizotSampleVirtualLightShadow(_VirtualLightShadowSlice, sampleWS, half3(0.0, 0.0, 0.0), half3(0.0, 0.0, 0.0), true);
                    float scatterContribution = transmittance * scatterWeight * beamEnvelope * sourceTransmittance * sourceFalloff * depthFade * shadowVisibility;
                    scattering += scatterContribution * core;
                    hotCoreScattering += scatterContribution * max(core - 1.0, 0.0);
                    transmittance *= stepTransmittance;
                }
                float3 beamDirectionWS = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0), true);
                float cosineTheta = dot(beamDirectionWS, -rayDirectionWS);
                float phaseDenominator = max(1.0 + _Anisotropy * _Anisotropy - 2.0 * _Anisotropy * cosineTheta, 0.01);
                float henyeyGreensteinPhase = (1.0 - _Anisotropy * _Anisotropy) / (12.5663706 * pow(phaseDenominator, 1.5));
                float phase = lerp(henyeyGreensteinPhase, 0.0795774715, saturate(_WideAngleScatter));
                float hotCoreFraction = saturate(hotCoreScattering / max(scattering, 1e-5));
                float peakColor = max(_Color.r, max(_Color.g, _Color.b));
                float3 beamColor = lerp(_Color.rgb, peakColor.xxx, saturate(_CoreWhiteMix) * hotCoreFraction);
                float3 radiance = beamColor * (_Color.a * _ScatteringIntensity * scattering * phase);
                return half4(radiance, 0.0);
            }
            ENDHLSL
        }
    }
}
