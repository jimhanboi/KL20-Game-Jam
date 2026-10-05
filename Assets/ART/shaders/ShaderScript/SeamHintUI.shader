Shader "Custom/SeamHintUI"
{
    Properties
    {
        [PerRendererData] _MainTex ("Mask", 2D) = "black" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _OutlinePixels ("Outline Pixels", Float) = 3
        _OutlineAlpha ("Outline Alpha", Range(0, 1)) = 1
        _FillAlpha ("Fill Alpha", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float _OutlinePixels;
            float _OutlineAlpha;
            float _FillAlpha;

            v2f vert(appdata_t v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                o.color = v.color * _Color;
                return o;
            }

 fixed4 frag(v2f i) : SV_Target
{
    float center = tex2D(_MainTex, i.uv).r;
    float high = center;
    float low = center;

    [unroll]
    for (int ring = 1; ring <= 2; ring++)
    {
        float radius = _OutlinePixels * ring * 0.5;

        [unroll]
        for (int k = 0; k < 16; k++)
        {
            float angle = k * 0.3926991;
            float2 offset = float2(cos(angle), sin(angle)) * radius * _MainTex_TexelSize.xy;
            float sample = tex2D(_MainTex, i.uv + offset).r;
            high = max(high, sample);
            low = min(low, sample);
        }
    }

    float edge = saturate(high - low);
    float alpha = saturate(edge * _OutlineAlpha + center * _FillAlpha);
    return fixed4(i.color.rgb, alpha * i.color.a);
}
            ENDCG
        }
    }
}