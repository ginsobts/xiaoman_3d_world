using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// 腾讯 Hunyuan3D-2 本地 API 服务（完全免费，不需要账号）。
    /// 对接官方 api_server.py：POST /send 提交（base64 图片），GET /status/{uid} 轮询，完成时返回 GLB 的 base64。
    /// 服务由 Tools/Hunyuan3D/start_hunyuan_api.bat 启动，默认 http://127.0.0.1:8081。
    /// </summary>
    public class HunyuanLocalProvider : HttpProviderBase, IImageTo3DProvider
    {
        public const string UrlPref = "Xiaoman.AI3D.HunyuanUrl";
        public const string DefaultUrl = "http://127.0.0.1:8081";
        public const string TierPref = "Xiaoman.AI3D.HunyuanTier";

        public enum QualityTier { Standard, High, Ultra }

        public static QualityTier Tier
        {
            get => (QualityTier)EditorPrefs.GetInt(TierPref, (int)QualityTier.High);
            set => EditorPrefs.SetInt(TierPref, (int)value);
        }

        public static string BaseUrl
        {
            get => EditorPrefs.GetString(UrlPref, DefaultUrl).TrimEnd('/');
            set => EditorPrefs.SetString(UrlPref, (value ?? DefaultUrl).TrimEnd('/'));
        }

        public string Name => "Hunyuan3D-2 本地";
        public string KeyPrefName => UrlPref;
        public bool SupportsRigging => false;

        private readonly string _base;

        public HunyuanLocalProvider(string baseUrl = null) : base("")
        {
            _base = string.IsNullOrEmpty(baseUrl) ? BaseUrl : baseUrl.TrimEnd('/');
        }

        public Task<string> CreateRigTask(string modelTaskId, ReferencePropSpec spec, Action<string> log) => throw new NotSupportedException("本地 Hunyuan3D 不做绑骨；人物用 Tripo（免费额度含绑骨）或 Meshy。");
        public Task<ProviderTaskResult> PollRig(string rigTaskId, Action<string> log) => throw new NotSupportedException();
        public Task<string> CreateAnimationTask(string rigTaskId, int[] actionIds, Action<string> log) => throw new NotSupportedException();
        public Task<ProviderTaskResult> PollAnimation(string animTaskId, Action<string> log) => throw new NotSupportedException();

        /// <summary>服务是否在线（GET /status/ping 能返回 JSON 即可）。</summary>
        public static async Task<bool> IsOnline(string baseUrl = null)
        {
            try
            {
                string url = (string.IsNullOrEmpty(baseUrl) ? BaseUrl : baseUrl.TrimEnd('/')) + "/status/ping";
                using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(3)))
                using (var resp = await Http.GetAsync(url, cts.Token).ConfigureAwait(false))
                    return resp.IsSuccessStatusCode;
            }
            catch { return false; }
        }

        public async Task<string> CreateTask(IList<byte[]> images, ReferencePropSpec spec, Action<string> log)
        {
            if (images == null || images.Count == 0) throw new Exception("没有可用的参考图");
            var tier = Tier;
            // octree_resolution 决定几何细节（128 粗 / 256 细 / 384 极细）；face_count 为贴图前的减面上限
            int octree = tier == QualityTier.Ultra ? 384 : tier == QualityTier.High ? 256 : 128;
            int faces = tier == QualityTier.Ultra ? 200000 : tier == QualityTier.High ? 120000 : 50000;
            int steps = tier == QualityTier.Standard ? 5 : 8;   // turbo 模型 5–10 步即可
            var body = new Dictionary<string, object>
            {
                { "image", Convert.ToBase64String(images[0]) },
                { "texture", true },
                { "octree_resolution", octree },
                { "num_inference_steps", steps },
                { "guidance_scale", 5.0 },
                { "face_count", faces },
                { "seed", 1234 },
                { "type", "glb" },
            };
            var req = new HttpRequestMessage(HttpMethod.Post, _base + "/send")
            {
                Content = new StringContent(MiniJson.Serialize(body), System.Text.Encoding.UTF8, "application/json")
            };
            var (ok, code, resp) = await Send(req);
            if (!ok) throw new Exception($"本地 Hunyuan3D 提交失败 HTTP {code}: {resp}\n请先运行 Tools/Hunyuan3D/start_hunyuan_api.bat");
            string uid = MiniJson.GetString(MiniJson.Parse(resp), "uid");
            if (string.IsNullOrEmpty(uid)) throw new Exception("本地服务没有返回 uid: " + resp);
            log?.Invoke($"[Hunyuan3D] 任务已提交 {uid}（octree {octree}, {faces / 1000}k 面, {steps} 步）");
            return uid;
        }

        public async Task<ProviderTaskResult> Poll(string taskId, Action<string> log)
        {
            var result = new ProviderTaskResult();
            var (ok, code, resp) = await Send(new HttpRequestMessage(HttpMethod.Get, _base + "/status/" + taskId));
            if (!ok)
            {
                result.status = ProviderTaskStatus.Failed;
                result.error = $"HTTP {code}: {resp}";
                return result;
            }
            var json = MiniJson.Parse(resp);
            string status = MiniJson.GetString(json, "status") ?? "";
            if (status == "completed")
            {
                string b64 = MiniJson.GetString(json, "model_base64");
                if (string.IsNullOrEmpty(b64)) { result.status = ProviderTaskStatus.Failed; result.error = "完成但没有模型数据"; return result; }
                result.glbBytes = Convert.FromBase64String(b64);
                result.status = ProviderTaskStatus.Succeeded;
                result.progress = 100;
                result.raw = "(glb " + result.glbBytes.Length / 1024 + " KB)";
                return result;
            }
            if (status == "failed")
            {
                result.status = ProviderTaskStatus.Failed;
                result.error = "本地 Hunyuan3D 失败: " + (MiniJson.GetString(json, "error") ?? resp);
                return result;
            }
            result.raw = resp;
            result.status = ProviderTaskStatus.Running;
            string stage = MiniJson.GetString(json, "stage") ?? "";
            result.progress = stage == "texture" ? 70 : stage == "export" ? 95 : stage == "cleanup" ? 55 : stage == "shape" ? 25 : 5;
            return result;
        }
    }
}
