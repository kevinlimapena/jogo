// Shader estilo PlayStation 1:
// - vértices "grudam" numa grade de baixa resolução (o famoso tremido/wobble do PS1)
// - iluminação por vértice (Gouraud), sem sombras
// - neblina linear calculada por vértice
// Os parâmetros globais (_PS1*) são enviados pelo script RunnerPS1.cs.
Shader "Runner/PS1Lit"
{
    Properties
    {
        [MainColor] _BaseColor ("Cor", Color) = (1,1,1,1)
        _Color ("Cor (legado)", Color) = (1,1,1,1)
        _EmissionColor ("Emissão", Color) = (0,0,0,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100

        Pass
        {
            ZWrite On
            Cull Back

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 _BaseColor;
            float4 _EmissionColor;

            float4 _PS1LightDir;     // direção em que a luz viaja (forward da luz)
            float4 _PS1LightColor;
            float4 _PS1Ambient;
            float4 _PS1FogColor;
            float4 _PS1FogParams;    // x = início, y = fim, z = ligado
            float4 _PS1Snap;         // xy = grade de snap, z = ligado

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR0;
                float fog : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 clip = UnityObjectToClipPos(v.vertex);
                if (_PS1Snap.z > 0.5 && clip.w > 0.0001)
                {
                    float2 grid = _PS1Snap.xy;
                    clip.xy = floor(clip.xy / clip.w * grid + 0.5) / grid * clip.w;
                }
                o.pos = clip;

                float3 n = normalize(UnityObjectToWorldNormal(v.normal));
                float ndl = saturate(dot(n, -_PS1LightDir.xyz));
                float3 lit = _PS1Ambient.rgb + _PS1LightColor.rgb * ndl;
                o.col = float4(_BaseColor.rgb * lit + _EmissionColor.rgb, 1.0);

                float3 wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float d = distance(wpos, _WorldSpaceCameraPos);
                float range = max(0.001, _PS1FogParams.y - _PS1FogParams.x);
                o.fog = _PS1FogParams.z > 0.5 ? saturate((d - _PS1FogParams.x) / range) : 0.0;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                return float4(lerp(i.col.rgb, _PS1FogColor.rgb, i.fog), 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
