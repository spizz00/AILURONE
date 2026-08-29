Shader "AILURONE/System Error Target Preview"
{
    Properties
    {
        [HDR] _BaseColor ("Base Color", Color) = (0.02,0.45,1,1)
        [HDR] _AccentColor ("Accent Color", Color) = (1,0.85,0.12,1)
        _Intensity ("Intensity", Range(0,4)) = 1
        _HitAmount ("Hit Amount", Range(0,1)) = 0
        _AdsAmount ("ADS Glitch", Range(0,1)) = 0
        _KillAmount ("Kill Glitch", Range(0,1)) = 0
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
            Name "SystemErrorTarget"
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
                half _HitAmount;
                half _AdsAmount;
                half _KillAmount;
                float _Seed;
                half _Visibility;
            CBUFFER_END

            float Hash11(float value)
            {
                return frac(sin(value * 127.1 + 311.7) * 43758.5453);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionOS = input.positionOS.xyz;
                float band = floor(positionOS.y * 7.5 + _Seed * 9.0);
                float noiseValue = Hash11(band);
                float slice = step(0.56, frac(positionOS.y * 3.15 + positionOS.x * 0.42 + _Seed));
                float direction = noiseValue > 0.5 ? 1.0 : -1.0;

                positionOS.x += direction * slice * (_AdsAmount * 0.105 + _KillAmount * 0.205);
                positionOS.y += (noiseValue - 0.5) * slice * _KillAmount * 0.095;
                positionOS.z += (noiseValue - 0.5) * (_AdsAmount * 0.035 + _KillAmount * 0.075);

                output.positionCS = TransformObjectToHClip(positionOS);
                output.positionOS = positionOS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                clip(_Visibility - 0.5h);

                float3 normalWS = normalize(input.normalWS);
                float lighting = 0.67 + 0.33 * saturate(dot(normalWS, normalize(float3(-0.35, 0.72, -0.58))));
                float active = saturate(max(_HitAmount, max(_AdsAmount, _KillAmount)));

                float diagonalA = frac(input.positionOS.x * 2.4 + input.positionOS.y * 5.6 + _Seed * 1.7);
                float diagonalB = frac(-input.positionOS.x * 4.2 + input.positionOS.y * 7.1 + _Seed * 3.1);
                float blockHash = Hash11(floor(input.positionOS.x * 8.0) + floor(input.positionOS.y * 9.0) * 19.0 + _Seed * 23.0);

                float whiteBand = step(0.71, diagonalA) * step(0.48, blockHash) * active;
                float blackBand = step(0.78, diagonalB) * step(blockHash, 0.70) * active;
                float accentBand = step(0.84, frac(diagonalA + diagonalB * 0.63)) * active;

                half3 color = _BaseColor.rgb * lighting;
                color = lerp(color, half3(0.012h, 0.014h, 0.020h), saturate(blackBand * (0.92 + _KillAmount * 0.08)));
                color = lerp(color, half3(1.0h, 1.0h, 1.0h), saturate(whiteBand * (0.88 + _HitAmount * 0.12)));
                color = lerp(color, _AccentColor.rgb, saturate(accentBand * (0.68 + _AdsAmount * 0.22 + _KillAmount * 0.30)));

                float pulse = 1.0 + active * 0.18 + _HitAmount * 0.12;
                color *= max(0.0h, _Intensity) * pulse;
                return half4(color, 1.0h);
            }
            ENDHLSL
        }
    }
}
