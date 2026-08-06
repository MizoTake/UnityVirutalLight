Shader "Hidden/MizoTake/Virtual Light/Shadow Caster"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "VirtualLightShadowCaster"
            Cull Back
            ZWrite Off
            ZTest Always
            Blend One One
            BlendOp Min
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            float4 _VirtualLightShadowCasterPositionRange;

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                return output;
            }

            float Frag(Varyings input) : SV_Target
            {
                return saturate(distance(input.positionWS, _VirtualLightShadowCasterPositionRange.xyz) * _VirtualLightShadowCasterPositionRange.w);
            }
            ENDHLSL
        }
    }
}
