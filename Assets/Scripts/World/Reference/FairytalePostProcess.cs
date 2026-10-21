using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 内置管线相机后期：柔和 bloom、环境光遮蔽、铅笔描边、胶片曲线、暖冷分离色调、晕影、纸张颗粒、水彩渗化。
    /// 不依赖 Post Processing 包；由 LightingDirector 按预设驱动参数。
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class FairytalePostProcess : MonoBehaviour
    {
        public LightingPreset Current = LightingPreset.Village();
        public bool enabledFX = true;

        private Material _material;
        private Camera _camera;
        private static readonly int ThresholdId = Shader.PropertyToID("_Threshold");
        private static readonly int BloomTexId = Shader.PropertyToID("_BloomTex");
        private static readonly int BloomTex2Id = Shader.PropertyToID("_BloomTex2");
        private static readonly int AOTexId = Shader.PropertyToID("_AOTex");
        private static readonly int BloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");
        private static readonly int SaturationId = Shader.PropertyToID("_Saturation");
        private static readonly int ContrastId = Shader.PropertyToID("_Contrast");
        private static readonly int VignetteId = Shader.PropertyToID("_Vignette");
        private static readonly int GrainId = Shader.PropertyToID("_Grain");
        private static readonly int TintId = Shader.PropertyToID("_Tint");
        private static readonly int ShadowsId = Shader.PropertyToID("_Shadows");
        private static readonly int HighlightsId = Shader.PropertyToID("_Highlights");
        private static readonly int SoftnessId = Shader.PropertyToID("_Softness");
        private static readonly int AOStrengthId = Shader.PropertyToID("_AOStrength");
        private static readonly int AORadiusId = Shader.PropertyToID("_AORadius");
        private static readonly int OutlineId = Shader.PropertyToID("_Outline");
        private static readonly int TonemapId = Shader.PropertyToID("_Tonemap");
        private static readonly int InkColorId = Shader.PropertyToID("_InkColor");

        private void OnEnable()
        {
            var shader = Shader.Find("Hidden/Xiaoman/Fairytale PostFX");
            if (shader == null || !shader.isSupported)
            {
                enabled = false;
                return;
            }
            _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _camera = GetComponent<Camera>();
            // AO 与描边需要深度 + 法线
            _camera.depthTextureMode |= DepthTextureMode.DepthNormals;
        }

        private void OnDisable()
        {
            if (_material != null)
            {
                if (Application.isPlaying) Destroy(_material);
                else DestroyImmediate(_material);
            }
        }

        private void OnRenderImage(RenderTexture source, RenderTexture destination)
        {
            if (!enabledFX || _material == null || Current == null)
            {
                Graphics.Blit(source, destination);
                return;
            }

            var p = Current;
            int w = source.width / 2, h = source.height / 2;
            var bloomA = RenderTexture.GetTemporary(w, h, 0, source.format);
            var bloomB = RenderTexture.GetTemporary(w, h, 0, source.format);
            var bloomC = RenderTexture.GetTemporary(w / 2, h / 2, 0, source.format);
            var bloomD = RenderTexture.GetTemporary(w / 2, h / 2, 0, source.format);

            _material.SetFloat(ThresholdId, p.bloomThreshold);
            Graphics.Blit(source, bloomA, _material, 0);
            Graphics.Blit(bloomA, bloomB, _material, 1);
            Graphics.Blit(bloomB, bloomA, _material, 2);
            // 二级更宽的模糊，形成柔和光晕尾巴
            Graphics.Blit(bloomA, bloomC, _material, 1);
            Graphics.Blit(bloomC, bloomD, _material, 2);
            Graphics.Blit(bloomD, bloomC, _material, 1);
            Graphics.Blit(bloomC, bloomD, _material, 2);

            // 环境光遮蔽：半分辨率计算 + 深度感知模糊
            RenderTexture aoA = null, aoB = null;
            bool wantAO = p.ambientOcclusion > 0.001f && (_camera.depthTextureMode & DepthTextureMode.DepthNormals) != 0;
            _material.SetFloat(AOStrengthId, wantAO ? p.ambientOcclusion : 0f);
            _material.SetFloat(AORadiusId, p.aoRadius);
            if (wantAO)
            {
                aoA = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.R8);
                aoB = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.R8);
                Graphics.Blit(source, aoA, _material, 4);
                Graphics.Blit(aoA, aoB, _material, 5);
                _material.SetTexture(AOTexId, aoB);
            }
            else _material.SetTexture(AOTexId, Texture2D.whiteTexture);

            _material.SetTexture(BloomTexId, bloomA);
            _material.SetTexture(BloomTex2Id, bloomD);
            _material.SetFloat(BloomIntensityId, p.bloomIntensity);
            _material.SetFloat(ExposureId, p.exposure);
            _material.SetFloat(SaturationId, p.saturation);
            _material.SetFloat(ContrastId, p.contrast);
            _material.SetFloat(VignetteId, p.vignette);
            _material.SetFloat(GrainId, p.grain);
            _material.SetFloat(SoftnessId, p.softness);
            _material.SetFloat(OutlineId, p.outline);
            _material.SetFloat(TonemapId, p.tonemap);
            _material.SetColor(InkColorId, p.inkColor);
            _material.SetColor(TintId, p.tint);
            _material.SetColor(ShadowsId, p.shadows);
            _material.SetColor(HighlightsId, p.highlights);
            Graphics.Blit(source, destination, _material, 3);

            RenderTexture.ReleaseTemporary(bloomA);
            RenderTexture.ReleaseTemporary(bloomB);
            RenderTexture.ReleaseTemporary(bloomC);
            RenderTexture.ReleaseTemporary(bloomD);
            if (aoA != null) RenderTexture.ReleaseTemporary(aoA);
            if (aoB != null) RenderTexture.ReleaseTemporary(aoB);
        }
    }
}
