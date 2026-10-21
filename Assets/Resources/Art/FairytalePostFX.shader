Shader "Hidden/Xiaoman/Fairytale PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        CGINCLUDE
        #include "UnityCG.cginc"

        sampler2D _MainTex;
        float4 _MainTex_TexelSize;
        sampler2D _BloomTex;   // 近距离柔光
        sampler2D _BloomTex2;  // 远距离光晕
        float _Threshold;
        float _BloomIntensity;
        float _Exposure;
        float _Saturation;
        float _Contrast;
        float _Vignette;
        float _Grain;
        float4 _Tint;       // 乘色（暖黄 / 冷蓝）
        float4 _Shadows;    // 暗部偏色
        float4 _Highlights; // 亮部偏色
        float _Softness;    // 水彩柔化

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
        };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            return o;
        }

        float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

        fixed4 fragPrefilter(v2f i) : SV_Target
        {
            float3 c = tex2D(_MainTex, i.uv).rgb;
            float l = Luma(c);
            float soft = saturate((l - _Threshold) / max(0.001, 1.0 - _Threshold));
            return fixed4(c * soft * soft, 1);
        }

        fixed4 blur(v2f i, float2 dir)
        {
            float2 step = _MainTex_TexelSize.xy * dir;
            float3 c = tex2D(_MainTex, i.uv).rgb * 0.227;
            c += tex2D(_MainTex, i.uv + step * 1.385).rgb * 0.316;
            c += tex2D(_MainTex, i.uv - step * 1.385).rgb * 0.316;
            c += tex2D(_MainTex, i.uv + step * 3.231).rgb * 0.070;
            c += tex2D(_MainTex, i.uv - step * 3.231).rgb * 0.070;
            return fixed4(c, 1);
        }

        fixed4 fragBlurH(v2f i) : SV_Target { return blur(i, float2(1, 0)); }
        fixed4 fragBlurV(v2f i) : SV_Target { return blur(i, float2(0, 1)); }

        fixed4 fragComposite(v2f i) : SV_Target
        {
            float3 c = tex2D(_MainTex, i.uv).rgb;
            float3 bloom = tex2D(_BloomTex, i.uv).rgb * 0.55 + tex2D(_BloomTex2, i.uv).rgb * 0.8;

            // 轻微的水彩渗化：原图与柔化版混合，边缘变软
            float2 px = _MainTex_TexelSize.xy * 1.5;
            float3 soft = (tex2D(_MainTex, i.uv + px).rgb + tex2D(_MainTex, i.uv - px).rgb
                + tex2D(_MainTex, i.uv + float2(px.x, -px.y)).rgb
                + tex2D(_MainTex, i.uv + float2(-px.x, px.y)).rgb) * 0.25;
            c = lerp(c, soft, _Softness);

            c += bloom * _BloomIntensity;
            c *= _Exposure * _Tint.rgb;

            float l = Luma(c);
            c = lerp(float3(l, l, l), c, _Saturation);
            c = (c - 0.5) * _Contrast + 0.5;

            // 分离色调：暗部和亮部各自偏色，模拟彩铅纸面上的暖光冷影
            float t = saturate(l);
            c += _Shadows.rgb * (1 - t) * _Shadows.a + _Highlights.rgb * t * _Highlights.a;

            float2 d = i.uv - 0.5;
            float vig = 1 - dot(d, d) * _Vignette;
            c *= saturate(vig);

            float grain = frac(sin(dot(i.uv * _ScreenParams.xy + _Time.y, float2(12.9898, 78.233))) * 43758.5453);
            c += (grain - 0.5) * _Grain;

            return fixed4(saturate(c), 1);
        }
        ENDCG

        // 0: 亮部提取
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragPrefilter
            ENDCG
        }
        // 1: 横向模糊
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurH
            ENDCG
        }
        // 2: 纵向模糊
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragBlurV
            ENDCG
        }
        // 3: 合成（Bloom、曝光、色调、暗角、颗粒）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            ENDCG
        }
    }
    FallBack Off
}
