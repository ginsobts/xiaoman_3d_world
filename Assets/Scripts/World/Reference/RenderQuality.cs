using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 统一把渲染质量拉到「精细」档：高分辨率软阴影、4x MSAA、各向异性过滤、垂直同步、HDR。
    /// 与项目 Quality 预设无关，进入 3D 世界时由 LightingDirector 调用一次。
    /// </summary>
    public static class RenderQuality
    {
        public static void Apply(Camera camera, Light sun)
        {
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
            QualitySettings.shadowProjection = ShadowProjection.StableFit;
            QualitySettings.shadowCascades = 4;
            QualitySettings.shadowDistance = Mathf.Max(QualitySettings.shadowDistance, 110f);
            QualitySettings.shadowCascade4Split = new Vector3(0.06f, 0.18f, 0.45f);
            QualitySettings.pixelLightCount = Mathf.Max(QualitySettings.pixelLightCount, 8);
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            QualitySettings.antiAliasing = 4;
            QualitySettings.softParticles = true;
            QualitySettings.lodBias = Mathf.Max(QualitySettings.lodBias, 1.5f);
            // 垂直同步：帧时间稳定，画面不撕裂，对转视角时的眩晕有明显帮助
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = -1;

            if (camera != null)
            {
                camera.allowHDR = true;
                camera.allowMSAA = true;
                camera.nearClipPlane = Mathf.Max(camera.nearClipPlane, 0.08f);
                camera.farClipPlane = Mathf.Max(camera.farClipPlane, 400f);
            }
            if (sun != null)
            {
                sun.shadows = LightShadows.Soft;
                sun.shadowResolution = UnityEngine.Rendering.LightShadowResolution.VeryHigh;
                sun.shadowBias = 0.03f;
                sun.shadowNormalBias = 0.35f;
                sun.shadowNearPlane = 0.2f;
            }
        }
    }
}
