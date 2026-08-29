Shader "AILURONE/Block Deletion Ghost Preview"
{
    Properties
    {
        [HDR] _BaseColor ("Color", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 0
        _GlitchAmount ("Glitch Amount", Range(0,1)) = 0
        _Seed ("Seed", Float) = 0
        _DepthOffset ("Depth Offset", Range(0,0.1)) = 0.024
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
            Name "BlockDeletionGhost"
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
                float3 positionOS : TEXCOORD0;
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
                float band = floor(positionOS.y * 8.2 + _Seed * 9.0);
                float bandNoise = Hash11(band);
                float slice = step(0.56, frac(positionOS.y * 3.7 + _Seed * 2.1));
                positionOS.x += (bandNoise - 0.5) * 0.18 * _GlitchAmount * slice;
                positionOS.y += (Hash11(band + 3.0) - 0.5) * 0.05 * _GlitchAmount;

                float3 positionWS = TransformObjectToWorld(positionOS);
                float3 towardCamera = normalize(_WorldSpaceCameraPos - positionWS);
                positionWS += towardCamera * _DepthOffset;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.positionOS = positionOS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 cell = floor(input.positionOS.xy * 7.0 + _Seed);
                float checker = fmod(abs(cell.x + cell.y), 2.0);
                float alphaMask = checker < 0.5 ? 1.0 : 0.58;
                return half4(_BaseColor.rgb, saturate(_Alpha * _BaseColor.a * alphaMask));
            }
            ENDHLSL
        }
    }
}
