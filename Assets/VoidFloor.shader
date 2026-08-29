Shader "Unlit/VoidFloor"
{
    Properties
    {
        _MainTex ("Pattern Texture", 2D) = "white" {}
        _PatternColor ("Pattern Color", Color) = (0.5, 0.5, 0.5, 1)
        _BackgroundColor ("Background/Fade Color", Color) = (0.1, 0.1, 0.1, 1)
        _GridSize ("Grid Size", Float) = 0.05
        _PanSpeed ("Pan Speed X", Float) = 0.2
        _Thickness ("Cross Thickness", Float) = 0.1
        _Size ("Cross Size", Float) = 0.35
        _FadeStart ("Fade Start Distance", Float) = 50.0
        _FadeEnd ("Fade End Distance", Float) = 150.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _PatternColor;
            float4 _BackgroundColor;
            float _GridSize;
            float _PanSpeed;
            float _FadeStart;
            float _FadeEnd;

            float _Thickness;
            float _Size;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.uv = o.worldPos.xz * _GridSize;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Pan UVs
                float2 uv = i.uv;
                uv.x += _Time.y * _PanSpeed;
                uv.y += _Time.y * _PanSpeed * 0.2;

                // Procedural Minimalist Pattern (2x2 Repeating Block)
                float2 tile = floor(uv);
                float2 f = frac(uv);
                float dx = abs(f.x - 0.5);
                float dy = abs(f.y - 0.5);
                
                int mx = (int)fmod(abs(tile.x), 2.0);
                int my = (int)fmod(abs(tile.y), 2.0);
                int shapeIndex = mx + my * 2; // 0, 1, 2, or 3
                
                float pattern = 0.0;
                
                if (shapeIndex == 0) {
                    // Shape 0: Classic Cross
                    if (dx < _Thickness && dy < _Size) pattern = 1.0;
                    if (dy < _Thickness && dx < _Size) pattern = 1.0;
                } 
                else if (shapeIndex == 1) {
                    // Shape 1: Hollow Square
                    if (dx < _Size && dy < _Size && (dx > _Size - _Thickness || dy > _Size - _Thickness)) pattern = 1.0;
                }
                else if (shapeIndex == 2) {
                    // Shape 2: Small Solid Box
                    if (dx < _Thickness * 1.5 && dy < _Thickness * 1.5) pattern = 1.0;
                }
                else if (shapeIndex == 3) {
                    // Shape 3: Four Corner Dots
                    if (abs(dx - _Size * 0.7) < _Thickness && abs(dy - _Size * 0.7) < _Thickness) pattern = 1.0;
                }

                fixed4 col = lerp(_BackgroundColor, _PatternColor, pattern);

                float dist = distance(i.worldPos, _WorldSpaceCameraPos);
                float fade = smoothstep(_FadeStart, _FadeEnd, dist);
                
                return lerp(col, _BackgroundColor, fade);
            }
            ENDCG
        }
    }
}
