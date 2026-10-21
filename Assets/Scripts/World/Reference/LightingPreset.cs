using System;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 一幅画的完整光照配方：太阳、环境三色、雾、天空底色，以及屏幕后期。
    /// 用于在进入不同参考场景时整体切换"画面气质"。
    /// </summary>
    [Serializable]
    public class LightingPreset
    {
        public string id = "default";

        [Header("太阳")]
        public Vector3 sunEuler = new Vector3(50f, -30f, 0f);
        public Color sunColor = new Color(1f, 0.96f, 0.88f);
        public float sunIntensity = 1.1f;
        public float shadowStrength = 0.75f;

        [Header("环境光（天空 / 地平 / 地面）")]
        public Color ambientSky = new Color(0.72f, 0.82f, 0.95f);
        public Color ambientEquator = new Color(0.86f, 0.82f, 0.72f);
        public Color ambientGround = new Color(0.42f, 0.5f, 0.38f);

        [Header("雾与天空")]
        public Color fogColor = new Color(0.77f, 0.84f, 0.88f);
        public float fogStart = 42f;
        public float fogEnd = 78f;
        public Color background = new Color(0.55f, 0.7f, 0.85f);

        [Header("屏幕后期")]
        public float bloomThreshold = 0.78f;
        public float bloomIntensity = 0.55f;
        public float exposure = 1.0f;
        public float saturation = 1.08f;
        public float contrast = 1.04f;
        public float vignette = 0.45f;
        public float grain = 0.014f;
        public float softness = 0.10f;
        public Color tint = Color.white;
        public Color shadows = new Color(0.05f, 0.03f, 0.12f, 0.35f);
        public Color highlights = new Color(0.12f, 0.09f, 0.0f, 0.35f);

        [Header("细节增强")]
        [Tooltip("屏幕空间环境光遮蔽强度：角落与接触面变暗，模型立体感明显增强")]
        public float ambientOcclusion = 0.75f;
        public float aoRadius = 0.6f;
        [Tooltip("铅笔描边强度：按深度 / 法线突变画细线")]
        public float outline = 0.35f;
        [Tooltip("胶片曲线强度：高光不过曝、暗部不死黑")]
        public float tonemap = 0.6f;
        public Color inkColor = new Color(0.16f, 0.13f, 0.22f, 1f);

        public LightingPreset Clone()
        {
            return (LightingPreset)MemberwiseClone();
        }

        public static LightingPreset Lerp(LightingPreset a, LightingPreset b, float t)
        {
            t = Mathf.Clamp01(t);
            var r = b.Clone();
            r.sunEuler = new Vector3(
                Mathf.LerpAngle(a.sunEuler.x, b.sunEuler.x, t),
                Mathf.LerpAngle(a.sunEuler.y, b.sunEuler.y, t),
                Mathf.LerpAngle(a.sunEuler.z, b.sunEuler.z, t));
            r.sunColor = Color.Lerp(a.sunColor, b.sunColor, t);
            r.sunIntensity = Mathf.Lerp(a.sunIntensity, b.sunIntensity, t);
            r.shadowStrength = Mathf.Lerp(a.shadowStrength, b.shadowStrength, t);
            r.ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
            r.ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            r.ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
            r.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            r.fogStart = Mathf.Lerp(a.fogStart, b.fogStart, t);
            r.fogEnd = Mathf.Lerp(a.fogEnd, b.fogEnd, t);
            r.background = Color.Lerp(a.background, b.background, t);
            r.bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t);
            r.bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t);
            r.exposure = Mathf.Lerp(a.exposure, b.exposure, t);
            r.saturation = Mathf.Lerp(a.saturation, b.saturation, t);
            r.contrast = Mathf.Lerp(a.contrast, b.contrast, t);
            r.vignette = Mathf.Lerp(a.vignette, b.vignette, t);
            r.grain = Mathf.Lerp(a.grain, b.grain, t);
            r.softness = Mathf.Lerp(a.softness, b.softness, t);
            r.tint = Color.Lerp(a.tint, b.tint, t);
            r.shadows = Color.Lerp(a.shadows, b.shadows, t);
            r.highlights = Color.Lerp(a.highlights, b.highlights, t);
            r.ambientOcclusion = Mathf.Lerp(a.ambientOcclusion, b.ambientOcclusion, t);
            r.aoRadius = Mathf.Lerp(a.aoRadius, b.aoRadius, t);
            r.outline = Mathf.Lerp(a.outline, b.outline, t);
            r.tonemap = Mathf.Lerp(a.tonemap, b.tonemap, t);
            r.inkColor = Color.Lerp(a.inkColor, b.inkColor, t);
            return r;
        }

        // ---------- 预设 ----------

        /// <summary>村庄默认：和 LevelBuilder 原本的设置一致。</summary>
        public static LightingPreset Village()
        {
            return new LightingPreset { id = "village" };
        }

        /// <summary>温室钢琴：高角度逆光，透过玻璃的强烈暖绿散射，大量 bloom。</summary>
        public static LightingPreset Greenhouse()
        {
            return new LightingPreset
            {
                id = "greenhouse",
                sunEuler = new Vector3(38f, 155f, 0f),
                sunColor = new Color(1f, 0.95f, 0.78f),
                sunIntensity = 1.65f,
                shadowStrength = 0.55f,
                ambientSky = new Color(0.86f, 0.9f, 0.72f),
                ambientEquator = new Color(0.62f, 0.72f, 0.5f),
                ambientGround = new Color(0.28f, 0.34f, 0.22f),
                fogColor = new Color(0.9f, 0.94f, 0.78f),
                fogStart = 18f,
                fogEnd = 46f,
                background = new Color(0.93f, 0.95f, 0.82f),
                bloomThreshold = 0.62f,
                bloomIntensity = 1.1f,
                exposure = 1.05f,
                saturation = 0.98f,
                contrast = 1.02f,
                vignette = 0.42f,
                softness = 0.14f,
                tint = new Color(1f, 0.99f, 0.9f),
                shadows = new Color(0.02f, 0.08f, 0.02f, 0.35f),
                highlights = new Color(0.14f, 0.12f, 0.0f, 0.4f),
            };
        }

        /// <summary>海边：高亮冷白天空，阳光在海面上反射，几乎无雾、低饱和。</summary>
        public static LightingPreset Seaside()
        {
            return new LightingPreset
            {
                id = "seaside",
                sunEuler = new Vector3(42f, 10f, 0f),
                sunColor = new Color(1f, 0.98f, 0.94f),
                sunIntensity = 1.35f,
                shadowStrength = 0.5f,
                ambientSky = new Color(0.78f, 0.88f, 1f),
                ambientEquator = new Color(0.8f, 0.86f, 0.92f),
                ambientGround = new Color(0.36f, 0.48f, 0.34f),
                fogColor = new Color(0.86f, 0.92f, 0.98f),
                fogStart = 25f,
                fogEnd = 150f,
                background = new Color(0.8f, 0.9f, 0.98f),
                bloomThreshold = 0.72f,
                bloomIntensity = 0.75f,
                exposure = 1.08f,
                saturation = 0.9f,
                contrast = 0.98f,
                vignette = 0.35f,
                softness = 0.11f,
                tint = new Color(0.96f, 0.99f, 1f),
                shadows = new Color(0.0f, 0.05f, 0.14f, 0.35f),
                highlights = new Color(0.1f, 0.1f, 0.08f, 0.3f),
            };
        }

        /// <summary>林荫街道：树冠漏下的斑驳暖光，整体黄绿调，雾色偏绿白。</summary>
        public static LightingPreset ShadedStreet()
        {
            return new LightingPreset
            {
                id = "street",
                sunEuler = new Vector3(58f, -40f, 0f),
                sunColor = new Color(1f, 0.93f, 0.72f),
                sunIntensity = 1.5f,
                shadowStrength = 0.7f,
                ambientSky = new Color(0.78f, 0.86f, 0.62f),
                ambientEquator = new Color(0.66f, 0.74f, 0.5f),
                ambientGround = new Color(0.3f, 0.36f, 0.24f),
                fogColor = new Color(0.86f, 0.9f, 0.74f),
                fogStart = 26f,
                fogEnd = 70f,
                background = new Color(0.86f, 0.9f, 0.76f),
                bloomThreshold = 0.68f,
                bloomIntensity = 0.85f,
                exposure = 1.0f,
                saturation = 1.05f,
                contrast = 1.05f,
                vignette = 0.42f,
                softness = 0.15f,
                tint = new Color(1f, 1f, 0.92f),
                shadows = new Color(0.02f, 0.1f, 0.02f, 0.4f),
                highlights = new Color(0.14f, 0.1f, 0.0f, 0.4f),
            };
        }

        /// <summary>图书馆：室内偏暖，窗外高亮冷色草原，强对比。</summary>
        public static LightingPreset Library()
        {
            return new LightingPreset
            {
                id = "library",
                sunEuler = new Vector3(28f, 180f, 0f),
                sunColor = new Color(1f, 0.97f, 0.9f),
                sunIntensity = 1.2f,
                shadowStrength = 0.65f,
                ambientSky = new Color(0.7f, 0.8f, 0.92f),
                ambientEquator = new Color(0.6f, 0.52f, 0.42f),
                ambientGround = new Color(0.22f, 0.16f, 0.12f),
                fogColor = new Color(0.6f, 0.56f, 0.5f),
                fogStart = 40f,
                fogEnd = 140f,
                background = new Color(0.62f, 0.76f, 0.94f),
                bloomThreshold = 0.7f,
                bloomIntensity = 0.7f,
                exposure = 0.98f,
                saturation = 1.0f,
                contrast = 1.1f,
                vignette = 0.42f,
                softness = 0.08f,
                tint = new Color(1f, 0.97f, 0.92f),
                shadows = new Color(0.1f, 0.05f, 0.0f, 0.35f),
                highlights = new Color(0.0f, 0.05f, 0.12f, 0.3f),
            };
        }

    /// <summary>橘子树院子：正午顶光，白墙反光极亮，树影斑驳，暖黄。</summary>
        public static LightingPreset OrangeCourtyard()
        {
            return new LightingPreset
            {
                id = "orange",
                sunEuler = new Vector3(66f, -20f, 0f),
                sunColor = new Color(1f, 0.95f, 0.82f),
                sunIntensity = 1.7f,
                shadowStrength = 0.72f,
                ambientSky = new Color(0.86f, 0.9f, 0.84f),
                ambientEquator = new Color(0.78f, 0.8f, 0.7f),
                ambientGround = new Color(0.46f, 0.46f, 0.4f),
                fogColor = new Color(0.92f, 0.93f, 0.86f),
                fogStart = 30f,
                fogEnd = 90f,
                background = new Color(0.9f, 0.92f, 0.84f),
                bloomThreshold = 0.66f,
                bloomIntensity = 0.95f,
                exposure = 1.08f,
                saturation = 1.0f,
                contrast = 1.06f,
                vignette = 0.45f,
                softness = 0.13f,
                tint = new Color(1f, 0.99f, 0.93f),
                shadows = new Color(0.0f, 0.06f, 0.1f, 0.3f),
                highlights = new Color(0.14f, 0.11f, 0.02f, 0.45f),
            };
        }
    }
}
