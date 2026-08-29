Shader "AILURONE/Block Deletion Target Preview V6"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.02,0.45,1,1)
        [HDR] _AccentColor ("Accent Color", Color) = (1,0.85,0.12,1)
        _Intensity ("Intensity", Range(0,4)) = 1
        _ErrorAmount ("Error Amount", Range(0,1)) = 0
        _SliceAmount ("Slice Amount", Range(0,1)) = 0
        _DeleteAmount ("Delete Amount", Range(0,1)) = 0
        _Seed ("Seed", Float) = 0
        _Visibility ("Visibility", Range(0,1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Opaque"
            "Queue"="Geometry"
            "RenderPipeline"="UniversalPipeline"
        }

        Pass
        {
            Name "BlockDeletionTarget"
            ZWrite On
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
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionOS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _AccentColor;
                half _Intensity;
                half _ErrorAmount;
                half _SliceAmount;
                half _DeleteAmount;
                float _Seed;
                half _Visibility;
            CBUFFER_END

            float Hash11(float value)
            {
                return frac(sin(value * 127.1 + 311.7) * 43758.5453);
            }

            float Hash21(float2 value)
            {
                return frac(sin(dot(value, float2(127.1, 311.7)) + _Seed * 19.3) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float band = floor(positionOS.y * 8.0 + _Seed * 7.0);
                float sliceMask = step(0.56, frac(positionOS.y * 3.25 + positionOS.x * 0.31 + _Seed));
                float bandNoise = Hash11(band);
                float direction = bandNoise > 0.5 ? 1.0 : -1.0;

                positionOS.x += direction * sliceMask * _SliceAmount * 0.16;
                positionOS.y += (bandNoise - 0.5) * sliceMask * _SliceAmount * 0.055;
                positionOS.z += (Hash11(band + 4.0) - 0.5) * _SliceAmount * 0.045;

                output.positionCS = TransformObjectToHClip(positionOS);
                output.positionOS = positionOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_Visibility - 0.5h);

                float2 cell = floor(input.positionOS.xy * 7.0 + float2(_Seed * 1.7, _Seed * 2.3));
                float cellHash = Hash21(cell);
                float keepCell = step(_DeleteAmount, cellHash);
                clip(keepCell - 0.5);

                float3 normalWS = normalize(input.normalWS);
                float lighting = 0.68 + 0.32 * saturate(dot(normalWS, normalize(float3(-0.32, 0.74, -0.56))));
                half3 normalColor = _BaseColor.rgb * lighting;

                float checker = fmod(abs(cell.x + cell.y), 2.0);
                float roleHash = Hash21(cell + float2(19.0, 7.0));
                half3 errorColor = checker < 0.5 ? half3(1.0h, 1.0h, 1.0h) : half3(0.01h, 0.012h, 0.018h);
                errorColor = roleHash > 0.84 ? _AccentColor.rgb : errorColor;

                float hardReplace = saturate(_ErrorAmount * 1.22);
                half3 color = lerp(normalColor, errorColor, hardReplace);

                // Keep a small amount of the state color in low-error cells only.
                float retainedState = step(0.72, roleHash) * (1.0 - hardReplace) * 0.12;
                color = lerp(color, _BaseColor.rgb, retainedState);
                color *= max(0.0h, _Intensity);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
