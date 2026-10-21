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
        sampler2D _AOTex;      // 环境光遮蔽（半分辨率）
        sampler2D _CameraDepthNormalsTexture;
        float4 _CameraDepthNormalsTexture_TexelSize;
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
        float _AOStrength;  // 环境光遮蔽强度
        float _AORadius;    // 遮蔽半径（米）
        float _Outline;     // 铅笔描边强度
        float _Tonemap;     // 胶片曲线强度
        float4 _InkColor;   // 描边颜色

        struct v2f
        {
            float4 pos : SV_POSITION;
            float2 uv : TEXCOORD0;
            float2 uvDepth : TEXCOORD1;
        };

        v2f vert(appdata_img v)
        {
            v2f o;
            o.pos = UnityObjectToClipPos(v.vertex);
            o.uv = v.texcoord;
            o.uvDepth = v.texcoord;
            #if UNITY_UV_STARTS_AT_TOP
            if (_MainTex_TexelSize.y < 0) o.uvDepth.y = 1 - o.uvDepth.y;
            #endif
            return o;
        }

        float Luma(float3 c) { return dot(c, float3(0.299, 0.587, 0.114)); }

        // ---------- 深度 / 法线工具 ----------

        void ReadDepthNormal(float2 uv, out float depth01, out float3 viewNormal)
        {
            DecodeDepthNormal(tex2D(_CameraDepthNormalsTexture, uv), depth01, viewNormal);
        }

        // 视空间位置（OpenGL 约定：相机朝 -z）
        float3 ViewPos(float2 uv, float depth01)
        {
            float eye = depth01 * _ProjectionParams.z;
            float2 ndc = uv * 2.0 - 1.0;
            return float3(ndc.x * eye / unity_CameraProjection._m00, ndc.y * eye / unity_CameraProjection._m11, -eye);
        }

        float2 ProjectUv(float3 vp)
        {
            float4 clip = mul(unity_CameraProjection, float4(vp, 1.0));
            return clip.xy / clip.w * 0.5 + 0.5;
        }

        // ---------- Bloom ----------

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

        // ---------- 环境光遮蔽（半球采样，半分辨率） ----------

        fixed4 fragAO(v2f i) : SV_Target
        {
            float depth01; float3 n;
            ReadDepthNormal(i.uvDepth, depth01, n);
            if (depth01 >= 0.999) return fixed4(1, 1, 1, 1);   // 天空
            float3 vp = ViewPos(i.uvDepth, depth01);
            n = normalize(n);

            // 每像素旋转的采样核，12 个半球方向
            float noise = frac(sin(dot(i.uv * _ScreenParams.xy, float2(12.9898, 78.233))) * 43758.5453) * 6.2831;
            float cs = cos(noise), sn = sin(noise);
            float3 tangent = normalize(abs(n.y) < 0.99 ? cross(n, float3(0, 1, 0)) : cross(n, float3(1, 0, 0)));
            float3 bitangent = cross(n, tangent);
            float3 rt = tangent * cs + bitangent * sn;
            float3 rb = cross(n, rt);

            const float3 kernel[12] = {
                float3( 0.53,  0.12, 0.40), float3(-0.32,  0.44, 0.35), float3( 0.08, -0.55, 0.30),
                float3(-0.60, -0.18, 0.25), float3( 0.25,  0.30, 0.80), float3(-0.15, -0.25, 0.70),
                float3( 0.70, -0.40, 0.20), float3(-0.45,  0.65, 0.15), float3( 0.10,  0.08, 0.98),
                float3( 0.38, -0.12, 0.55), float3(-0.22,  0.18, 0.60), float3( 0.05,  0.62, 0.45)
            };

            float occlusion = 0.0;
            float radius = _AORadius * (0.6 + 0.4 * saturate(2.0 / max(0.5, -vp.z)));  // 近处略大，远处收小
            [unroll]
            for (int k = 0; k < 12; k++)
            {
                float3 d = kernel[k];
                float scale = lerp(0.2, 1.0, (k + 1.0) / 12.0);
                float3 sp = vp + (rt * d.x + rb * d.y + n * d.z) * radius * scale;
                float2 suv = ProjectUv(sp);
                if (any(suv < 0.0) || any(suv > 1.0)) continue;
                float sd; float3 sn2;
                ReadDepthNormal(suv, sd, sn2);
                float sceneZ = -sd * _ProjectionParams.z;
                float dz = sceneZ - sp.z;   // 场景表面比采样点更靠近相机 → 被遮挡
                float rangeCheck = smoothstep(0.0, 1.0, radius / max(abs(vp.z - sceneZ), 1e-4));
                occlusion += (dz > 0.02 * radius ? 1.0 : 0.0) * rangeCheck;
            }
            float ao = 1.0 - occlusion / 12.0;
            ao = saturate(pow(ao, 1.0 + _AOStrength * 1.5));
            return fixed4(ao, ao, ao, 1);
        }

        // 深度感知模糊，避免遮蔽溢到前景边缘
        fixed4 fragAOBlur(v2f i) : SV_Target
        {
            float d0; float3 n0;
            ReadDepthNormal(i.uvDepth, d0, n0);
            float2 px = _MainTex_TexelSize.xy;
            float sum = 0.0, wsum = 0.0;
            [unroll]
            for (int y = -2; y <= 2; y++)
            [unroll]
            for (int x = -2; x <= 2; x++)
            {
                float2 o = float2(x, y) * px;
                float ds; float3 ns;
                ReadDepthNormal(i.uvDepth + o, ds, ns);
                float w = exp(-(x * x + y * y) * 0.25) * saturate(1.0 - abs(ds - d0) * 400.0) * saturate(dot(n0, ns));
                sum += tex2D(_MainTex, i.uv + o).r * w;
                wsum += w;
            }
            float ao = wsum > 1e-4 ? sum / wsum : tex2D(_MainTex, i.uv).r;
            return fixed4(ao, ao, ao, 1);
        }

        // ---------- 合成 ----------

        float3 Filmic(float3 x)
        {
            // ACES 近似拟合，保住高光细节，不会一片死白
            return saturate((x * (2.51 * x + 0.03)) / (x * (2.43 * x + 0.59) + 0.14));
        }

        float OutlineAt(float2 uvD)
        {
            float dc; float3 nc;
            ReadDepthNormal(uvD, dc, nc);
            if (dc >= 0.999) return 0.0;
            float2 px = _CameraDepthNormalsTexture_TexelSize.xy * 1.2;
            float edge = 0.0;
            float2 offs[4] = { float2(px.x, 0), float2(-px.x, 0), float2(0, px.y), float2(0, -px.y) };
            [unroll]
            for (int k = 0; k < 4; k++)
            {
                float d; float3 n;
                ReadDepthNormal(uvD + offs[k], d, n);
                float depthEdge = saturate(abs(d - dc) / max(dc, 0.002) * 12.0 - 0.15);
                float normalEdge = saturate((1.0 - dot(nc, n)) * 1.6 - 0.1);
                edge = max(edge, max(depthEdge, normalEdge * 0.6));
            }
            // 远处线条变淡，像铅笔收笔
            return edge * saturate(1.3 - dc * 6.0);
        }

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

            // 环境光遮蔽：角落、接触面变暗并略偏冷
            float ao = tex2D(_AOTex, i.uv).r;
            float aoMix = lerp(1.0, ao, _AOStrength);
            c *= lerp(float3(0.82, 0.86, 1.0), float3(1, 1, 1), aoMix) * aoMix;

            c += bloom * _BloomIntensity;
            c *= _Exposure * _Tint.rgb;
            c = lerp(c, Filmic(c), _Tonemap);

            float l = Luma(c);
            c = lerp(float3(l, l, l), c, _Saturation);
            c = (c - 0.5) * _Contrast + 0.5;

            // 分离色调：暗部和亮部各自偏色，模拟彩铅纸面上的暖光冷影
            float t = saturate(l);
            c += _Shadows.rgb * (1 - t) * _Shadows.a + _Highlights.rgb * t * _Highlights.a;

            // 铅笔描边：按深度 / 法线突变画细线
            if (_Outline > 0.001)
            {
                float edge = OutlineAt(i.uvDepth) * _Outline;
                c = lerp(c, _InkColor.rgb * (0.4 + 0.6 * l), edge);
            }

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
        // 3: 合成（Bloom、AO、曝光、胶片曲线、色调、描边、暗角、颗粒）
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragComposite
            #pragma target 3.0
            ENDCG
        }
        // 4: 环境光遮蔽
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAO
            #pragma target 3.0
            ENDCG
        }
        // 5: 遮蔽模糊
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment fragAOBlur
            #pragma target 3.0
            ENDCG
        }
    }
    FallBack Off
}
