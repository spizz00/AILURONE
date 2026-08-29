Shader "Hidden/AILURONE/WorldRewriteFullscreen"
{
    Properties
    {
        [Header(Core Colour)]
        _RewriteTint ("Rewrite Tint", Color) = (0.62, 0.86, 0.93, 1.00)
        _OriginalColorRetention ("Original Colour Retention", Range(0, 1)) = 0.38
        _TintBlend ("Cold Tint Blend", Range(0, 1)) = 0.42
        _RewriteBrightness ("Rewrite Brightness", Range(0.5, 1.5)) = 1.00
        _RewriteContrast ("Rewrite Contrast", Range(0.5, 2.0)) = 1.14

        [Header(World Transition)]
        _FeatherWidth ("Transition Feather (Metres)", Range(0.05, 4.0)) = 2.00
        _EdgeColor ("Rewrite Frontier Colour", Color) = (0.34, 0.91, 1.00, 1.00)
        _EdgeWidth ("Frontier Width (Metres)", Range(0.01, 1.0)) = 0.36
        _EdgeIntensity ("Frontier Intensity", Range(0, 2)) = 0.12
        _FrontierDistortion ("Frontier Distortion (Metres)", Range(0, 2.0)) = 0.48
        _FrontierBreakup ("Frontier Breakup", Range(0, 1)) = 0.62

        [Header(Frontier Wake)]
        _WakeColor ("Compiled Wake Colour", Color) = (0.48, 0.94, 1.00, 1.00)
        _WakeWidth ("Compiled Wake Width (Metres)", Range(0.1, 8.0)) = 3.20
        _WakeIntensity ("Compiled Wake Intensity", Range(0, 2)) = 0.46
        _WakeBandFrequency ("Compiled Wake Band Frequency", Range(0.1, 8.0)) = 1.35
        _WakeBandStrength ("Compiled Wake Band Strength", Range(0, 1)) = 0.42

        [Header(World Anchored Layering)]
        _BandFrequency ("Primary Layer Frequency", Range(0.03, 4.0)) = 0.28
        _BandStrength ("Primary Layer Strength", Range(0, 0.5)) = 0.055
        _SecondaryBandFrequency ("Secondary Layer Frequency", Range(0.02, 2.0)) = 0.11
        _SecondaryBandStrength ("Secondary Layer Strength", Range(0, 0.3)) = 0.030
        _NoiseScale ("World Noise Scale", Range(0.03, 5.0)) = 0.32
        _NoiseStrength ("World Noise Strength", Range(0, 0.3)) = 0.022
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "WorldRewritePrototypeV11"

            ZWrite Off
            ZTest Always
            Cull Off
            Blend Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _AILU_RewriteCenterWS;
            float _AILU_RewriteRadius;
            float _AILU_RewriteAmount;

            CBUFFER_START(UnityPerMaterial)
                half4 _RewriteTint;
                half4 _EdgeColor;
                half4 _WakeColor;
                float _OriginalColorRetention;
                float _TintBlend;
                float _RewriteBrightness;
                float _RewriteContrast;
                float _FeatherWidth;
                float _EdgeWidth;
                float _EdgeIntensity;
                float _FrontierDistortion;
                float _FrontierBreakup;
                float _WakeWidth;
                float _WakeIntensity;
                float _WakeBandFrequency;
                float _WakeBandStrength;
                float _BandFrequency;
                float _BandStrength;
                float _SecondaryBandFrequency;
                float _SecondaryBandStrength;
                float _NoiseScale;
                float _NoiseStrength;
            CBUFFER_END

            float Hash11(float value)
            {
                return frac(sin(value * 127.1) * 43758.5453123);
            }

            float WorldNoise(float3 p, float scale)
            {
                p *= max(scale, 0.0001);

                float a = sin(dot(p, float3(1.17, 2.03, 0.79)));
                float b = sin(dot(p, float3(-0.73, 1.31, 2.11)) + a * 0.85);
                float c = sin(dot(p, float3(1.91, -0.57, 1.43)) + b * 0.65);

                return saturate((a + b + c) / 6.0 + 0.5);
            }

            float BroadLayer(float coordinate, float lowEdge, float highEdge)
            {
                float phase = frac(coordinate);

                float enter = smoothstep(
                    lowEdge,
                    lowEdge + 0.12,
                    phase
                );

                float exitLayer = 1.0 - smoothstep(
                    highEdge - 0.12,
                    highEdge,
                    phase
                );

                return enter * exitLayer;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float2 uv = input.texcoord;
                half4 source = SAMPLE_TEXTURE2D_X(
                    _BlitTexture,
                    sampler_LinearClamp,
                    uv
                );

                float amount = saturate(_AILU_RewriteAmount);
                float radius = max(_AILU_RewriteRadius, 0.0);

                if (amount <= 0.0001 || radius <= 0.0001)
                {
                    return source;
                }

                real rawDepth = SampleSceneDepth(uv);

                #if UNITY_REVERSED_Z
                    if (rawDepth < 0.0001)
                    {
                        return source;
                    }
                    real depth = rawDepth;
                #else
                    if (rawDepth > 0.9999)
                    {
                        return source;
                    }
                    real depth = lerp(
                        UNITY_NEAR_CLIP_VALUE,
                        1.0,
                        rawDepth
                    );
                #endif

                float3 worldPosition = ComputeWorldSpacePosition(
                    uv,
                    depth,
                    UNITY_MATRIX_I_VP
                );

                float baseDistance = distance(
                    worldPosition,
                    _AILU_RewriteCenterWS.xyz
                );

                // Low-frequency world distortion prevents the frontier from
                // reading as a perfect shield sphere.
                float frontierNoise = WorldNoise(
                    worldPosition + float3(11.3, -4.7, 8.9),
                    0.24
                );

                float signedFrontierNoise = frontierNoise * 2.0 - 1.0;
                float distortedDistance =
                    baseDistance
                    + signedFrontierNoise
                    * _FrontierDistortion;

                float feather = max(_FeatherWidth, 0.0001);
                float innerStart = max(radius - feather, 0.0);

                float insideMask = 1.0 - smoothstep(
                    innerStart,
                    radius,
                    distortedDistance
                );

                float rewriteMask = saturate(insideMask * amount);

                if (rewriteMask <= 0.0001)
                {
                    return source;
                }

                float luminance = dot(
                    source.rgb,
                    float3(0.2126, 0.7152, 0.0722)
                );

                // Preserve more of the original material than v1.
                // The world loses colour identity without becoming a flat cyan wash.
                float3 desaturated = lerp(
                    luminance.xxx,
                    source.rgb,
                    saturate(_OriginalColorRetention)
                );

                float3 coldResponse =
                    _RewriteTint.rgb
                    * (0.30 + luminance * 0.78);

                float3 rewrittenColour = lerp(
                    desaturated,
                    coldResponse,
                    saturate(_TintBlend)
                );

                rewrittenColour *= _RewriteBrightness;

                rewrittenColour =
                    (rewrittenColour - 0.5)
                    * _RewriteContrast
                    + 0.5;

                // Two large world-anchored strata with different directions.
                // Their low frequency and irregular gating avoid screen-space stripes.
                float3 directionA = normalize(
                    float3(0.22, 0.95, 0.21)
                );

                float3 directionB = normalize(
                    float3(-0.61, 0.36, 0.70)
                );

                float coordinateA =
                    dot(worldPosition, directionA)
                    * max(_BandFrequency, 0.0001);

                float coordinateB =
                    dot(worldPosition, directionB)
                    * max(_SecondaryBandFrequency, 0.0001);

                float cellA = floor(coordinateA);
                float cellB = floor(coordinateB);

                float layerA = BroadLayer(
                    coordinateA,
                    0.13,
                    0.72
                );

                float layerB = BroadLayer(
                    coordinateB,
                    0.22,
                    0.80
                );

                float gateNoise = WorldNoise(
                    worldPosition + float3(-3.1, 7.4, 2.6),
                    0.39
                );

                float signedA = Hash11(cellA) * 2.0 - 1.0;
                float signedB = Hash11(cellB + 41.0) * 2.0 - 1.0;

                float layerContribution =
                    signedA
                    * layerA
                    * _BandStrength
                    * lerp(0.30, 1.0, gateNoise)
                    +
                    signedB
                    * layerB
                    * _SecondaryBandStrength
                    * lerp(1.0, 0.35, gateNoise);

                rewrittenColour +=
                    _EdgeColor.rgb
                    * layerContribution;

                // Slow, stable surface variation. No time input means no flicker.
                float surfaceNoise = WorldNoise(
                    worldPosition,
                    _NoiseScale
                );

                rewrittenColour *=
                    1.0
                    + (surfaceNoise * 2.0 - 1.0)
                    * _NoiseStrength;

                float3 finalColour = lerp(
                    source.rgb,
                    rewrittenColour,
                    rewriteMask
                );

                // A short world-space wake immediately behind the frontier
                // makes the scan read as compiling geometry rather than as a
                // flat colour filter. It remains stable because it uses no time.
                float signedInside = radius - distortedDistance;
                float wakeWidth = max(_WakeWidth, 0.0001);
                float wakeMask = step(0.0, signedInside)
                    * (1.0 - smoothstep(
                        0.0,
                        wakeWidth,
                        signedInside
                    ));

                float wakePhase =
                    signedInside
                    * max(_WakeBandFrequency, 0.0001)
                    + dot(
                        worldPosition,
                        normalize(float3(0.43, 0.87, -0.24))
                    )
                    * 0.075;

                float wakeBands = 0.5 + 0.5 * sin(
                    wakePhase * 6.2831853
                );

                wakeBands = smoothstep(0.38, 0.82, wakeBands);

                float wakeSignal = wakeMask
                    * lerp(
                        1.0,
                        wakeBands,
                        saturate(_WakeBandStrength)
                    )
                    * insideMask
                    * amount;

                finalColour +=
                    _WakeColor.rgb
                    * wakeSignal
                    * _WakeIntensity;

                // Fragment the outer frontier so it reads as an incomplete
                // compilation boundary rather than a clean shield ring.
                float frontierDistance = abs(
                    distortedDistance - radius
                );

                float frontier = 1.0 - smoothstep(
                    _EdgeWidth,
                    _EdgeWidth + max(feather * 0.12, 0.03),
                    frontierDistance
                );

                float breakupNoise = WorldNoise(
                    worldPosition + float3(6.2, 3.8, -9.4),
                    0.52
                );

                float breakupThreshold = lerp(
                    0.20,
                    0.76,
                    saturate(_FrontierBreakup)
                );

                float frontierFragments = smoothstep(
                    breakupThreshold - 0.11,
                    breakupThreshold + 0.11,
                    breakupNoise
                );

                // Never let breakup erase the whole scan front. The broken
                // fragments are accents over a continuous readable core.
                frontier *= lerp(
                    0.55,
                    1.0,
                    frontierFragments
                );
                frontier *= amount;
                frontier *= saturate(insideMask + 0.18);

                finalColour +=
                    _EdgeColor.rgb
                    * frontier
                    * _EdgeIntensity;

                return half4(finalColour, source.a);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
