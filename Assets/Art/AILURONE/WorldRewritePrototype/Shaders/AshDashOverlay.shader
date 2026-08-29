Shader "Hidden/AILURONE/AshDashPhaseWarp"
{
    Properties
    {
        _PhaseColorA ("Phase Cyan", Color) = (0.22, 0.88, 1, 1)
        _PhaseColorB ("Phase Violet", Color) = (0.55, 0.18, 1, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "AshDashPhaseWarp"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _AshDashIntensity;
            float4 _AshDashDirection;
            float _AshDashPhase;
            half4 _PhaseColorA;
            half4 _PhaseColorB;

            float2 ClampScreenUV(float2 uv)
            {
                return clamp(uv, 0.001, 0.999);
            }

            half4 SampleScene(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    ClampScreenUV(uv)
                );
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = input.texcoord;
                half4 source = SampleScene(uv);
                float intensity = max(0.0, _AshDashIntensity);

                if (intensity <= 0.0005)
                {
                    return source;
                }

                float aspect =
                    _ScreenParams.x /
                    max(1.0, _ScreenParams.y);

                float2 centered = uv - 0.5;
                float2 aspectCentered =
                    float2(centered.x * aspect, centered.y);

                float radius = length(aspectCentered);
                float2 radial =
                    aspectCentered /
                    max(0.0001, radius);

                float2 direction =
                    _AshDashDirection.xy /
                    max(0.001, length(_AshDashDirection.xy));

                float forwardAmount =
                    saturate(abs(direction.y));

                float forwardSign =
                    direction.y >= 0.0 ? 1.0 : -1.0;

                float2 flow =
                    radial * forwardAmount * forwardSign +
                    float2(direction.x, 0.0) *
                    (1.0 - forwardAmount * 0.45);

                flow.x /= aspect;

                float edgeResponse =
                    smoothstep(0.04, 0.86, radius);

                float travelPulse =
                    sin(saturate(_AshDashPhase) * 3.14159265);

                float warpDistance =
                    intensity *
                    (0.012 + 0.042 * edgeResponse) *
                    (0.72 + 0.28 * travelPulse);

                float2 offset =
                    flow * warpDistance;

                half4 sampleA = SampleScene(uv - offset * 0.28);
                half4 sampleB = SampleScene(uv - offset * 0.62);
                half4 sampleC = SampleScene(uv - offset);
                half4 sampleD = SampleScene(uv + offset * 0.18);

                half3 smear =
                    source.rgb * 0.38 +
                    sampleA.rgb * 0.25 +
                    sampleB.rgb * 0.19 +
                    sampleC.rgb * 0.12 +
                    sampleD.rgb * 0.06;

                float2 chromaOffset =
                    offset * 0.22 +
                    flow * intensity * 0.0015;

                half redChannel =
                    SampleScene(uv - chromaOffset).r;

                half blueChannel =
                    SampleScene(uv + chromaOffset).b;

                smear.r = lerp(
                    smear.r,
                    redChannel,
                    saturate(intensity * 0.7)
                );

                smear.b = lerp(
                    smear.b,
                    blueChannel,
                    saturate(intensity * 0.85)
                );

                half contentEdge =
                    saturate(
                        length(sampleA.rgb - sampleC.rgb) * 1.8
                    );

                float ringRadius =
                    lerp(
                        0.03,
                        1.15,
                        saturate(_AshDashPhase * 1.18)
                    );

                float radialFront =
                    1.0 -
                    smoothstep(
                        0.015,
                        0.075,
                        abs(radius - ringRadius)
                    );

                float sideCoordinate =
                    centered.x * sign(direction.x + 0.0001);

                float sideFrontPosition =
                    lerp(-0.58, 0.72, _AshDashPhase);

                float sideFront =
                    1.0 -
                    smoothstep(
                        0.012,
                        0.065,
                        abs(sideCoordinate - sideFrontPosition)
                    );

                float phaseFront = lerp(
                    radialFront,
                    sideFront,
                    saturate(abs(direction.x))
                );

                half3 phaseColor = lerp(
                    _PhaseColorA.rgb,
                    _PhaseColorB.rgb,
                    saturate(radius * 0.65 + _AshDashPhase * 0.25)
                );

                half3 result = lerp(
                    source.rgb,
                    smear,
                    saturate(intensity * (0.7 + edgeResponse * 0.3))
                );

                result +=
                    phaseColor *
                    intensity *
                    (
                        contentEdge * (0.2 + edgeResponse * 0.5) +
                        phaseFront * 0.13
                    );

                return half4(result, source.a);
            }
            ENDHLSL
        }
    }
}
