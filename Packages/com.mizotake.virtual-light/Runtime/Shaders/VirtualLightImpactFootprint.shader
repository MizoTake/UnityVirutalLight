Shader "MizoTake/Virtual Light/Impact Footprint"
{
    Properties
    {
        [HDR] _Color("Radiance", Color) = (0.08, 1.2, 3.0, 0.65)
        _InnerRatio("Inner Cone Ratio", Range(0, 0.99)) = 0.35
        _EdgeExponent("Edge Exponent", Range(0.1, 4)) = 0.8
        _SurfaceClipDistance("Surface Clip Distance (m)", Range(0.001, 0.2)) = 0.04
        [PerRendererData][NoScaleOffset] _VirtualLightGoboTexture("Gobo Texture", 2D) = "white" {}
        [PerRendererData] _VirtualLightGoboEnabled("Gobo Enabled", Float) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Transparent" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Impact Footprint"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_VirtualLightGoboTexture);
            SAMPLER(sampler_VirtualLightGoboTexture);

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _InnerRatio;
                half _EdgeExponent;
                float _SurfaceClipDistance;
                float _VirtualLightGoboEnabled;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 footprintOS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.footprintOS = input.positionOS.xy;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float radial = length(input.footprintOS) * 2.0;
                clip(1.0 - radial);
                float screenDepth = SampleSceneDepth(input.positionCS.xy / _ScaledScreenParams.xy);
                #if UNITY_REVERSED_Z
                    clip(screenDepth - 1e-6);
                #else
                    clip(0.999999 - screenDepth);
                #endif
                float3 sceneWS = ComputeWorldSpacePosition(input.positionCS.xy / _ScaledScreenParams.xy, screenDepth, UNITY_MATRIX_I_VP);
                float3 planeOriginWS = TransformObjectToWorld(float3(0.0, 0.0, 0.0));
                float3 planeNormalWS = TransformObjectToWorldDir(float3(0.0, 0.0, 1.0), true);
                clip(_SurfaceClipDistance - abs(dot(sceneWS - planeOriginWS, planeNormalWS)));
                half innerRatio = min(saturate(_InnerRatio), 0.99h);
                half profile = pow(saturate(1.0h - smoothstep(innerRatio, 1.0h, (half)radial)), max(_EdgeExponent, 0.1h));
                half4 goboSample = SAMPLE_TEXTURE2D(_VirtualLightGoboTexture, sampler_VirtualLightGoboTexture, input.footprintOS + 0.5);
                half goboMask = lerp(1.0h, saturate(dot(goboSample.rgb, half3(0.2126h, 0.7152h, 0.0722h)) * goboSample.a), saturate(_VirtualLightGoboEnabled));
                return half4(_Color.rgb * (_Color.a * profile * goboMask), 0.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
