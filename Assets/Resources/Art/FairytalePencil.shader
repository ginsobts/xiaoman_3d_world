Shader "Xiaoman/Fairytale Pencil"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Base Color / Pencil Texture", 2D) = "white" {}
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Strength", Range(0,2)) = 1
        _Roughness ("Roughness", Range(0,1)) = 0.8
        _HatchStrength ("Hatch Strength", Range(0,0.35)) = 0.05
        _RimColor ("Rim Color", Color) = (0.45,0.38,0.55,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.35
        _Wrap ("Light Wrap (soft shading)", Range(0,1)) = 0.35
        _ShadowTint ("Shadow Tint (cool shade)", Color) = (0.62,0.66,0.86,1)
        _WarmTint ("Lit Tint (warm light)", Color) = (1.0,0.98,0.93,1)
        _SpecStrength ("Specular", Range(0,1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 300

        CGPROGRAM
        // 自定义绘本光照：包裹式漫反射（亮暗过渡柔和）、暖光冷影分离、柔和高光、边缘光；GI 走 Standard 的间接光。
        #pragma surface surf Fairytale fullforwardshadows
        #pragma shader_feature_local _NORMALMAP
        #pragma target 3.0

        #include "UnityPBSLighting.cginc"

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _BumpScale;
        half _Roughness;
        half _HatchStrength;
        fixed4 _RimColor;
        half _RimStrength;
        half _Wrap;
        fixed4 _ShadowTint;
        fixed4 _WarmTint;
        half _SpecStrength;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldPos;
        };

        struct SurfaceOutputFairytale
        {
            fixed3 Albedo;
            fixed3 Normal;
            half3 Emission;
            half Smoothness;
            half Occlusion;
            fixed Alpha;
            half Rim;
        };

        inline half4 LightingFairytale(SurfaceOutputFairytale s, float3 viewDir, UnityGI gi)
        {
            half3 n = normalize(s.Normal);
            half ndl = dot(n, gi.light.dir);
            // 包裹式漫反射：把 -wrap..1 映射到 0..1，暗面不会死黑，像纸上的彩铅过渡
            half wrap = saturate((ndl + _Wrap) / (1.0 + _Wrap));
            // 稍微压一下过渡区，形成柔和的两段式明暗
            half shade = smoothstep(0.0, 0.85, wrap);
            shade = lerp(wrap, shade, 0.5);

            // 暖光冷影：过渡区从冷色调渐变到暖色调；gi.light.color 已含阴影衰减，所以多光源叠加也正确
            half3 tint = lerp(_ShadowTint.rgb, _WarmTint.rgb, shade);
            half3 diffuse = s.Albedo * gi.light.color * tint * shade;

            // 柔和高光（Blinn-Phong，强度随光滑度）
            half3 h = normalize(gi.light.dir + viewDir);
            half ndh = saturate(dot(n, h));
            half specPow = exp2(2.0 + s.Smoothness * 9.0);
            half spec = pow(ndh, specPow) * s.Smoothness * _SpecStrength * saturate(ndl * 4.0);
            half3 specular = gi.light.color * spec;

            half4 c;
            c.rgb = diffuse + specular;
            #ifdef UNITY_PASS_FORWARDBASE
            // 间接光（三色环境 / 球谐）：没被主光照到的地方环境光偏冷，像纸面上的冷影
            half lit = saturate(dot(gi.light.color, half3(0.33, 0.33, 0.33)) * 2.0) * shade;
            half3 ambientTint = lerp(_ShadowTint.rgb, half3(1, 1, 1), lit);
            c.rgb += gi.indirect.diffuse * s.Albedo * s.Occlusion * ambientTint;
            // 边缘光：背光面更明显，让人物 / 道具从背景里"跳"出来一点
            c.rgb += _RimColor.rgb * s.Rim * _RimStrength * (0.6 + 0.4 * (1.0 - shade));
            #endif
            c.a = s.Alpha;
            return c;
        }

        inline void LightingFairytale_GI(SurfaceOutputFairytale s, UnityGIInput data, inout UnityGI gi)
        {
            gi = UnityGlobalIllumination(data, s.Occlusion, s.Normal);
        }

        void surf(Input IN, inout SurfaceOutputFairytale o)
        {
            fixed4 paper = tex2D(_MainTex, IN.uv_MainTex);

            // 两组略带歪斜的斜向铅笔排线，世界空间投影，让无贴图的程序化物体也有手绘颗粒
            float h1 = abs(frac((IN.worldPos.x + IN.worldPos.y * 0.73 + IN.worldPos.z * 0.17) * 9.0) - 0.5);
            float h2 = abs(frac((IN.worldPos.z - IN.worldPos.y * 0.61 + IN.worldPos.x * 0.21) * 13.0) - 0.5);
            float hatch = smoothstep(0.39, 0.49, min(h1, h2));
            fixed3 baseCol = paper.rgb * _Color.rgb;
            baseCol *= 1.0 - hatch * _HatchStrength;

            #ifdef _NORMALMAP
            fixed3 nrm = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #else
            fixed3 nrm = fixed3(0, 0, 1);
            #endif
            o.Normal = nrm;

            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), nrm)), 2.6);
            o.Rim = rim;
            o.Albedo = baseCol;
            o.Emission = 0;
            o.Smoothness = 1.0 - _Roughness;
            o.Occlusion = 1;
            o.Alpha = _Color.a * paper.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
