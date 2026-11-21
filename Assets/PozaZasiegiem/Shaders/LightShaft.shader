// Snop światła / "god ray" w powietrzu – addytywny stożek z miękkimi krawędziami
// i powoli przesuwającym się kurzem. Działa w URP i Built-in (proste unlit).
Shader "PozaZasiegiem/LightShaft"
{
    Properties
    {
        _Color ("Kolor (A = siła)", Color) = (1, 0.9, 0.7, 0.15)
        _Noise ("Szum (kurz)", 2D) = "white" {}
        _Intensity ("Jasność", Float) = 1
        _EdgeSoft ("Miękkość krawędzi", Float) = 2
        _LengthFade ("Zanik wzdłuż", Float) = 1.2
        _NearFade ("Zanik przy kamerze (m)", Float) = 1.5
        _Scroll ("Ruch kurzu", Vector) = (0.02, 0.05, 0, 0)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Blend One One
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _Noise;
            float4 _Noise_ST;
            float4 _Color;
            float _Intensity, _EdgeSoft, _LengthFade, _NearFade;
            float4 _Scroll;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 wpos : TEXCOORD1;
                float3 wnorm : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.wpos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.wnorm = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.wpos);
                float edge = pow(saturate(abs(dot(viewDir, normalize(i.wnorm)))), _EdgeSoft);
                float along = pow(saturate(1.0 - i.uv.y), _LengthFade);
                float2 nuv = i.uv * _Noise_ST.xy + _Time.y * _Scroll.xy;
                float n = tex2D(_Noise, nuv).r * 0.7 + tex2D(_Noise, nuv * 2.3 + 0.37).r * 0.3;
                float camDist = distance(_WorldSpaceCameraPos, i.wpos);
                float nearF = saturate((camDist - 0.3) / max(_NearFade, 0.01));
                float a = _Color.a * _Intensity * edge * along * nearF * (0.45 + 0.9 * n);
                return fixed4(_Color.rgb * a, 1);
            }
            ENDCG
        }
    }
    FallBack Off
}
