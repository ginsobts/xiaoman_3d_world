Shader "Xiaoman/Fairytale Pencil"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _MainTex ("Pencil Texture", 2D) = "white" {}
        _Roughness ("Roughness", Range(0,1)) = 0.8
        _HatchStrength ("Hatch Strength", Range(0,0.35)) = 0.09
        _RimColor ("Rim Color", Color) = (0.45,0.38,0.55,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        fixed4 _Color;
        half _Roughness;
        half _HatchStrength;
        fixed4 _RimColor;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 paper = tex2D(_MainTex, IN.uv_MainTex);

            // Two imperfect diagonal pencil strokes. World-space projection keeps the
            // hand-drawn grain visible even on untextured procedural primitives.
            float h1 = abs(frac((IN.worldPos.x + IN.worldPos.y * 0.73 + IN.worldPos.z * 0.17) * 9.0) - 0.5);
            float h2 = abs(frac((IN.worldPos.z - IN.worldPos.y * 0.61 + IN.worldPos.x * 0.21) * 13.0) - 0.5);
            float hatch = smoothstep(0.39, 0.49, min(h1, h2));
            fixed3 baseCol = paper.rgb * _Color.rgb;
            baseCol *= 1.0 - hatch * _HatchStrength;

            half rim = pow(1.0 - saturate(dot(normalize(IN.viewDir), normalize(IN.worldNormal))), 2.6);
            o.Albedo = lerp(baseCol, _RimColor.rgb, rim * 0.15);
            o.Metallic = 0;
            o.Smoothness = 1.0 - _Roughness;
            o.Alpha = _Color.a * paper.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
