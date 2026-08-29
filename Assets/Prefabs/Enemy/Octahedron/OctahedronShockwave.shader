Shader "AILURONE/Octahedron Shockwave"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (1.8, 0.08, 0.65, 0.45)
        _FresnelPower("Fresnel Power", Range(0.5, 8)) = 3.2
        _Opacity("Opacity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+40"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "Shockwave"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _FresnelPower;
                half _Opacity;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs =
                    GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs =
                    GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = normalInputs.normalWS;
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 viewDirectionWS =
                    GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(
                    1.0h - saturate(abs(dot(normalWS, viewDirectionWS))),
                    _FresnelPower);

                half hasVertexColor =
                    dot(input.color.rgb, input.color.rgb) > 0.0001h
                        ? 1.0h
                        : 0.0h;
                half3 tint = lerp(
                    _Color.rgb,
                    input.color.rgb,
                    hasVertexColor);
                half vertexAlpha = input.color.a > 0.001h
                    ? input.color.a
                    : 1.0h;
                half alpha =
                    _Color.a * _Opacity * vertexAlpha *
                    lerp(0.12h, 1.0h, fresnel);

                return half4(tint * lerp(0.45h, 1.3h, fresnel), alpha);
            }
            ENDHLSL
        }
    }
}
