Shader "AILURONE/System Error Ghost Preview"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 0
        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _Seed ("Seed", Float) = 0
        _DepthOffset ("Depth Offset", Range(0,0.1)) = 0.025
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent+20"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "SystemErrorGhost"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half _Alpha;
                half _GlitchAmount;
                float _Seed;
                half _DepthOffset;
            CBUFFER_END

            float Hash11(float value)
            {
                return frac(sin(value * 91.7 + 17.3) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float band = floor(positionOS.y * 8.5 + _Seed * 11.0);
                float noiseValue = Hash11(band);
                float slice = step(0.50, frac(positionOS.y * 3.8 + _Seed * 2.2));
                positionOS.x += (noiseValue - 0.5) * 0.24 * _GlitchAmount * slice;
                positionOS.y += (Hash11(band + 4.0) - 0.5) * 0.08 * _GlitchAmount;

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 towardCamera = normalize(_WorldSpaceCameraPos - positionWS);
                positionWS += towardCamera * _DepthOffset;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return half4(_BaseColor.rgb, saturate(_Alpha * _BaseColor.a));
            }
            ENDHLSL
        }
    }
}
