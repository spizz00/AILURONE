Shader "AILURONE/UI/PortraitGlitch"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _HeadMask ("Head Priority Mask", 2D) = "gray" {}
        _BackdropMask ("Backdrop Alpha Mask", 2D) = "black" {}
        _BackdropRect ("Backdrop Screen Rect", Vector) = (0,0,1,1)
        _Color ("Tint", Color) = (1,1,1,1)

        _GlitchAmount ("Low Health Glitch", Range(0,1)) = 0
        _BurstAmount ("Impact Burst", Range(0,1)) = 0
        _GlitchSeed ("Pattern Seed", Float) = 1
        _HeadStrength ("Head Strength", Range(0,3)) = 1.9
        _BodyStrength ("Body Strength", Range(0,2)) = 0.72
        _CyanColor ("Cyan Ghost", Color) = (0,1,0.96,1)
        _MagentaColor ("Magenta Ghost", Color) = (1,0,0.76,1)

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "PortraitGlitch"

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float4 screenPosition : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            sampler2D _HeadMask;
            sampler2D _BackdropMask;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _BackdropRect;

            float _GlitchAmount;
            float _BurstAmount;
            float _GlitchSeed;
            float _HeadStrength;
            float _BodyStrength;
            fixed4 _CyanColor;
            fixed4 _MagentaColor;

            float Hash11(float value)
            {
                return frac(sin(value * 12.9898) * 43758.5453);
            }

            float Hash21(float2 value)
            {
                return frac(
                    sin(dot(value, float2(127.1, 311.7)))
                    * 43758.5453);
            }

            v2f vert(appdata_t input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.worldPosition = input.vertex;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.screenPosition = ComputeScreenPos(output.vertex);
                output.texcoord = input.texcoord;
                output.color = input.color * _Color;
                return output;
            }

            fixed4 SamplePortrait(float2 uv)
            {
                return tex2D(_MainTex, saturate(uv)) + _TextureSampleAdd;
            }

            fixed4 frag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;

                float headMask = tex2D(_HeadMask, uv).r;

                // The supplied mask already contains a soft body value and a
                // bright head value. A small procedural head ellipse remains
                // as a fallback if the mask is not assigned yet.
                float2 headDelta = (uv - float2(0.245, 0.555))
                    / float2(0.205, 0.265);
                float proceduralHead = 1.0
                    - smoothstep(0.72, 1.15, length(headDelta));

                headMask = saturate(max(headMask, proceduralHead * 0.90));

                float regionalStrength = lerp(
                    _BodyStrength,
                    _HeadStrength,
                    headMask);

                float baseStrength = saturate(_GlitchAmount);
                float burstStrength = saturate(_BurstAmount);
                float combinedStrength = saturate(
                    baseStrength + burstStrength * 0.92);
                float localStrength = saturate(
                    combinedStrength * regionalStrength);

                if (combinedStrength <= 0.0001)
                {
                    return fixed4(0, 0, 0, 0);
                }

                float timeValue = _Time.y;
                float updateRate = lerp(9.0, 24.0, combinedStrength);
                float frame = floor(timeValue * updateRate);

                float bandCount = lerp(18.0, 52.0, localStrength);
                float bandIndex = floor(uv.y * bandCount);
                float bandNoise = Hash21(float2(
                    bandIndex + _GlitchSeed,
                    frame + _GlitchSeed * 0.37));

                float tearChance = saturate(
                    0.025
                    + localStrength * 0.22
                    + burstStrength * 0.28);
                float tearGate = step(1.0 - tearChance, bandNoise);

                float tearDirection = Hash11(
                    bandIndex * 7.17
                    + frame * 0.91
                    + _GlitchSeed) * 2.0 - 1.0;

                float tearPixels = lerp(
                    1.5,
                    18.0,
                    localStrength)
                    + burstStrength * 11.0;

                float horizontalTear = tearGate
                    * tearDirection
                    * tearPixels
                    * _MainTex_TexelSize.x;

                float2 tornUV = uv + float2(horizontalTear, 0.0);

                // Large block corruption. Lower cell counts create larger
                // pixel blocks as health approaches zero.
                float2 blockCellCount = lerp(
                    float2(72.0, 82.0),
                    float2(18.0, 25.0),
                    localStrength);

                float2 blockCell = floor(tornUV * blockCellCount);
                float blockNoise = Hash21(
                    blockCell
                    + float2(
                        frame * 0.73,
                        _GlitchSeed * 1.31));

                float blockChance = saturate(
                    0.015
                    + localStrength * 0.20
                    + burstStrength * 0.26);
                float blockGate = step(
                    1.0 - blockChance,
                    blockNoise);

                float2 pixelUV = (
                    floor(tornUV * blockCellCount) + 0.5)
                    / blockCellCount;

                float pixelBlend = blockGate
                    * saturate(0.34 + localStrength * 0.66);

                float2 sampleUV = lerp(
                    tornUV,
                    pixelUV,
                    pixelBlend);

                float splitDirection = Hash11(
                    frame * 3.11
                    + bandIndex * 0.41
                    + _GlitchSeed) > 0.5
                    ? 1.0
                    : -1.0;

                float splitPixels = lerp(
                    0.8,
                    10.5,
                    localStrength)
                    + burstStrength * 8.0;

                float verticalJitter = (
                    Hash11(
                        frame * 4.37
                        + bandIndex
                        + _GlitchSeed) - 0.5)
                    * splitPixels
                    * 0.18
                    * _MainTex_TexelSize.y;

                float2 splitOffset = float2(
                    splitDirection
                        * splitPixels
                        * _MainTex_TexelSize.x,
                    verticalJitter);

                fixed4 centreSample = SamplePortrait(sampleUV);
                fixed4 redSample = SamplePortrait(sampleUV + splitOffset);
                fixed4 blueSample = SamplePortrait(sampleUV - splitOffset);

                float ghostMultiplier = 1.45 + burstStrength * 0.65;
                fixed4 cyanSample = SamplePortrait(
                    sampleUV + splitOffset * ghostMultiplier);
                fixed4 magentaSample = SamplePortrait(
                    sampleUV - splitOffset * ghostMultiplier);

                float3 separatedRGB = float3(
                    redSample.r,
                    centreSample.g,
                    blueSample.b);

                float separationBlend = saturate(
                    localStrength * 0.92
                    + burstStrength * 0.60);

                float3 resultRGB = lerp(
                    centreSample.rgb,
                    separatedRGB,
                    separationBlend);

                float cyanAlpha = cyanSample.a;
                float magentaAlpha = magentaSample.a;
                float ghostAmount = saturate(
                    localStrength * 0.58
                    + burstStrength * 0.74);

                resultRGB += cyanSample.rgb
                    * _CyanColor.rgb
                    * cyanAlpha
                    * ghostAmount
                    * 0.62;

                resultRGB += magentaSample.rgb
                    * _MagentaColor.rgb
                    * magentaAlpha
                    * ghostAmount
                    * 0.62;

                // Some corrupted blocks become flat cyan or magenta slabs,
                // matching the large digital colour blocks in the reference.
                float blockColourChoice = Hash11(
                    blockCell.x * 5.7
                    + blockCell.y * 13.1
                    + frame
                    + _GlitchSeed);

                float3 flatBlockColour = blockColourChoice > 0.5
                    ? _CyanColor.rgb
                    : _MagentaColor.rgb;

                float sourceCoverage = max(
                    centreSample.a,
                    max(cyanAlpha, magentaAlpha));

                float flatBlockBlend = blockGate
                    * sourceCoverage
                    * saturate(
                        localStrength * 0.48
                        + burstStrength * 0.42);

                resultRGB = lerp(
                    resultRGB,
                    flatBlockColour,
                    flatBlockBlend);

                // Darker colour at lower health increases contrast without
                // making the portrait unreadable.
                resultRGB *= lerp(
                    1.0,
                    0.70,
                    saturate(localStrength * 0.55));

                float separatedAlpha = max(
                    centreSample.a,
                    max(redSample.a, blueSample.a));
                float ghostAlpha = max(cyanAlpha, magentaAlpha);

                float overlayVisibility = saturate(
                    baseStrength * 1.38
                    + burstStrength * 1.10);

                float outputAlpha = saturate(
                    separatedAlpha
                        * lerp(0.22, 0.64, localStrength)
                    + ghostAlpha
                        * ghostAmount
                        * 0.42);

                outputAlpha *= overlayVisibility;

                fixed4 outputColour = fixed4(
                    resultRGB,
                    outputAlpha);

                outputColour *= input.color;

                float2 screenUV = input.screenPosition.xy
                    / max(input.screenPosition.w, 0.0001);

                float2 backdropUV =
                    (screenUV - _BackdropRect.xy)
                    / max(
                        _BackdropRect.zw,
                        float2(0.0001, 0.0001));

                float insideBackdropRect =
                    step(0.0, backdropUV.x)
                    * step(0.0, backdropUV.y)
                    * step(backdropUV.x, 1.0)
                    * step(backdropUV.y, 1.0);

                float backdropAlpha = tex2D(
                    _BackdropMask,
                    saturate(backdropUV)).a
                    * insideBackdropRect;

                outputColour.a *= 1.0
                    - smoothstep(0.01, 0.08, backdropAlpha);

                #ifdef UNITY_UI_CLIP_RECT
                outputColour.a *= UnityGet2DClipping(
                    input.worldPosition.xy,
                    _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(outputColour.a - 0.001);
                #endif

                return outputColour;
            }
            ENDCG
        }
    }
}
