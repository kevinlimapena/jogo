// Pós-processo estilo PlayStation 1:
// a imagem (renderizada em baixa resolução) é reduzida para poucas cores por canal
// com pontilhado (dithering) ordenado 4x4, como o modo 15-bit do PS1.
Shader "Hidden/RunnerPS1Post"
{
    Properties
    {
        _MainTex ("Textura", 2D) = "white" {}
        _Levels ("Níveis por canal", Float) = 32
        _Dither ("Força do pontilhado", Float) = 1
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _Levels;
            float _Dither;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert (appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            float Bayer4(float2 p)
            {
                // matriz de Bayer 4x4 (0..15)
                float4 r0 = float4( 0,  8,  2, 10);
                float4 r1 = float4(12,  4, 14,  6);
                float4 r2 = float4( 3, 11,  1,  9);
                float4 r3 = float4(15,  7, 13,  5);
                float4 row = p.y < 1 ? r0 : (p.y < 2 ? r1 : (p.y < 3 ? r2 : r3));
                float v = p.x < 1 ? row.x : (p.x < 2 ? row.y : (p.x < 3 ? row.z : row.w));
                return (v + 0.5) / 16.0 - 0.5;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 c = tex2D(_MainTex, i.uv).rgb;
            #ifndef UNITY_COLORSPACE_GAMMA
                c = LinearToGammaSpace(c);
            #endif
                float2 px = fmod(floor(i.uv * _MainTex_TexelSize.zw), 4.0);
                float levels = max(2.0, _Levels) - 1.0;
                c = floor(c * levels + 0.5 + Bayer4(px) * _Dither) / levels;
                c = saturate(c);
            #ifndef UNITY_COLORSPACE_GAMMA
                c = GammaToLinearSpace(c);
            #endif
                return float4(c, 1.0);
            }
            ENDCG
        }
    }
}
