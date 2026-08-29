Shader "Skybox/LevelSkyBoxBW"
{
    Properties
    {
        _TopColor ("Top Sky Color", Color) = (0.95, 0.95, 0.95, 1)
        _BottomColor ("Bottom Sky Color", Color) = (0.85, 0.85, 0.85, 1)
        _GroundColor ("Ground Color (Lower Part)", Color) = (0.05, 0.05, 0.05, 1)
        _HorizonFadeSpread ("Horizon Transition Spread", Float) = 0.4
        _PatternColor1 ("Pattern 1 (Top)", Color) = (0.05, 0.05, 0.05, 1)
        _PatternColor2 ("Pattern 2 (Top)", Color) = (0.9, 0.1, 0.1, 1)
        _PatternColor1Alt ("Pattern 1 (Bottom Invert)", Color) = (0.95, 0.95, 0.95, 1)
        _PatternColor2Alt ("Pattern 2 (Bottom Invert)", Color) = (0.1, 0.8, 0.9, 1)
        _GridSize ("Grid Size", Float) = 8.0
        _PanSpeedX ("Pan Speed X", Float) = 0.02
        _PanSpeedY ("Pan Speed Y", Float) = 0.01
        _ShapeThickness ("Shape Thickness", Float) = 0.08
        _ShapeSize ("Shape Size", Float) = 0.3
        _GlobeDensityX ("Globe Vert Lines Density", Float) = 40.0
        _GlobeDensityY ("Globe Horiz Lines Density", Float) = 20.0
        _GlobeLineChance ("Globe Line Probability", Range(0, 1)) = 0.15
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            float4 _TopColor;
            float4 _BottomColor;
            float4 _GroundColor;
            float _HorizonFadeSpread;
            float4 _PatternColor1;
            float4 _PatternColor2;
            float4 _PatternColor1Alt;
            float4 _PatternColor2Alt;
            float _GridSize;
            float _PanSpeedX;
            float _PanSpeedY;
            float _ShapeThickness;
            float _ShapeSize;
            float _GlobeDensityX;
            float _GlobeDensityY;
            float _GlobeLineChance;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            float hash11(float p)
            {
                return frac(sin(p * 12.9898) * 43758.5453);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.texcoord);
                float2 uvBase;
                uvBase.x = atan2(dir.z, dir.x) / (2.0 * 3.14159265);
                uvBase.y = asin(dir.y) / 3.14159265;

                float sectionsX = _GlobeDensityX;
                float lineID_X = floor(uvBase.x * sectionsX);
                float randX = hash11(lineID_X);
                float isLineX = step(1.0 - _GlobeLineChance, randX);
                float localX = frac(uvBase.x * sectionsX);
                float vertLine = isLineX * step(abs(localX - 0.5), 0.04);

                float sectionsY = _GlobeDensityY;
                float lineID_Y = floor(uvBase.y * sectionsY);
                float randY = hash11(lineID_Y + 123.45);
                float isLineY = step(1.0 - _GlobeLineChance, randY);
                float localY = frac(uvBase.y * sectionsY);
                float horizLine = isLineY * step(abs(localY - 0.5), 0.04);

                float isGlobeLine = max(vertLine, horizLine);
                float lineHash = max(vertLine * randX, horizLine * randY);
                float isWhiteLine = step(0.9, lineHash);

                float2 uv1 = uvBase;
                uv1.x += _Time.y * _PanSpeedX;
                uv1.y += _Time.y * _PanSpeedY;
                uv1 *= _GridSize;

                float2 uv2 = uvBase;
                uv2.x -= _Time.y * _PanSpeedX;
                uv2.y -= _Time.y * _PanSpeedY;
                uv2 *= _GridSize;

                float pattern1 = 0.0;
                float pattern2 = 0.0;

                float2 f1 = frac(uv1);
                float2 tile1 = floor(uv1);
                float dx1 = abs(f1.x - 0.5);
                float dy1 = abs(f1.y - 0.5);
                int mx1 = (int)fmod(abs(tile1.x), 4.0);
                int my1 = (int)fmod(abs(tile1.y), 4.0);
                if (mx1 == 0 && my1 == 0)
                {
                    if (dx1 < _ShapeSize && dy1 < _ShapeSize) pattern1 = 1.0;
                }
                else if (mx1 == 1 && my1 == 3)
                {
                    if (dx1 < _ShapeThickness && dy1 < _ShapeThickness) pattern1 = 1.0;
                }

                float2 f2 = frac(uv2);
                float2 tile2 = floor(uv2);
                float dx2 = abs(f2.x - 0.5);
                float dy2 = abs(f2.y - 0.5);
                int mx2 = (int)fmod(abs(tile2.x), 4.0);
                int my2 = (int)fmod(abs(tile2.y), 4.0);
                if (mx2 == 2 && my2 == 2)
                {
                    if (dx2 < _ShapeSize && dy2 < _ShapeSize * 0.4) pattern2 = 1.0;
                }
                else if (mx2 == 3 && my2 == 1)
                {
                    if (dx2 < _ShapeSize && dy2 < _ShapeSize &&
                        (dx2 > _ShapeSize - _ShapeThickness ||
                         dy2 > _ShapeSize - _ShapeThickness)) pattern2 = 1.0;
                }

                float skyFactor = smoothstep(0.0, _HorizonFadeSpread, dir.y);
                fixed4 skyGradient = lerp(_BottomColor, _TopColor, skyFactor);
                float transitionFactor = smoothstep(
                    -_HorizonFadeSpread,
                    _HorizonFadeSpread,
                    dir.y);
                fixed4 bgColor = lerp(
                    _GroundColor,
                    skyGradient,
                    transitionFactor);
                fixed4 currentColor1 = lerp(
                    _PatternColor1Alt,
                    _PatternColor1,
                    transitionFactor);
                fixed4 currentColor2 = lerp(
                    _PatternColor2Alt,
                    _PatternColor2,
                    transitionFactor);

                fixed4 finalCol = bgColor;
                if (pattern2 > 0.5) finalCol = currentColor2;
                if (pattern1 > 0.5) finalCol = currentColor1;
                if (isGlobeLine > 0.5)
                {
                    fixed4 lineTopColor = isWhiteLine
                        ? fixed4(1, 1, 1, 1)
                        : fixed4(0, 0, 0, 1);
                    fixed4 lineBotColor = isWhiteLine
                        ? fixed4(0, 0, 0, 1)
                        : fixed4(1, 1, 1, 1);
                    finalCol = lerp(
                        lineBotColor,
                        lineTopColor,
                        transitionFactor);
                }
                return finalCol;
            }
            ENDCG
        }
    }
}
