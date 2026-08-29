Shader "Skybox/AILURONE"
{
    Properties
    {
        _MainTex ("Pattern Texture", 2D) = "white" {}
        _TopColor ("Top Color (Bright)", Color) = (1, 1, 1, 1)
        _BottomColor ("Bottom Color (Dark)", Color) = (0.1, 0.1, 0.1, 1)
        _GridSize ("Texture Tiling", Float) = 4.0
        _PanSpeed ("Pan Speed X", Float) = 0.05
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

            struct appdata_t {
                float4 vertex : POSITION;
                float3 texcoord : TEXCOORD0;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float3 texcoord : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _TopColor;
            float4 _BottomColor;
            float _GridSize;
            float _PanSpeed;

            v2f vert (appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.texcoord = v.texcoord;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Normalize the direction vector
                float3 dir = normalize(i.texcoord);
                
                // Vertical gradient: 0 at bottom, 1 at top
                float gradient = smoothstep(-0.5, 0.5, dir.y);
                fixed4 skyColor = lerp(_BottomColor, _TopColor, gradient);
                
                return skyColor;
            }
            ENDCG
        }
    }
}
