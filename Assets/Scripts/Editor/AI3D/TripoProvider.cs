using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// Tripo v3 OpenAPI（H3.1）。第一次创建 API Key 送 2000 Credits，足够把全部道具 + 人物各做一遍。
    /// 道具：image-to-model → PBR GLB（直接用 MiniGlb 导入，不花转换费）。
    /// 人物：image-to-model → animations/rig（v1.0 biped，支持坐 / 抱臂等 90+ 预设）→ animations/retarget（FBX）。
    /// 文档：https://developers.tripo3d.ai/en/docs
    /// </summary>
    public class TripoProvider : HttpProviderBase, IImageTo3DProvider
    {
        public const string Pref = "Xiaoman.AI3D.TripoKey";
        public const string TierPref = "Xiaoman.AI3D.TripoTier";
        private const string Base = "https://openapi.tripo3d.ai/v3";
        private const string Model = "v3.1-20260211";
        private const string RigModel = "v1.0-20240301";   // 预设动作最全（sit / fold_arms / look_around …）

        public enum QualityTier { Standard, High, Ultra }

        public static QualityTier Tier
        {
            get => (QualityTier)EditorPrefs.GetInt(TierPref, (int)QualityTier.High);
            set => EditorPrefs.SetInt(TierPref, (int)value);
        }

        /// <summary>估算一次生成的 Credits（道具 / 人物）。</summary>
        public static int EstimateCredits(ReferencePropSpec spec)
        {
            var t = Tier;
            int gen = t == QualityTier.Standard ? 30 : t == QualityTier.High ? 40 : 60;
            return spec.isCharacter ? gen + 25 + 10 : gen;
        }

        public string Name => "Tripo v3.1";
        public string KeyPrefName => Pref;
        public bool SupportsRigging => true;

        public TripoProvider(string apiKey) : base(apiKey) { }

        // ---------- 生成 ----------

        public async Task<string> CreateTask(IList<byte[]> images, ReferencePropSpec spec, Action<string> log)
        {
            if (images == null || images.Count == 0) throw new Exception("没有可用的参考图");
            if (string.IsNullOrEmpty(ApiKey)) throw new Exception("还没有填写 Tripo API Key。到 https://platform.tripo3d.ai 免费注册并创建 Key（首次送 2000 Credits）。");
            string token = await Upload(images[0], spec.id);
            var tier = Tier;

            var body = new Dictionary<string, object>
            {
                { "input", token },
                { "model", Model },
                { "texture", true },
                { "pbr", true },
                { "texture_quality", tier == QualityTier.Standard ? "standard" : "detailed" },
                { "texture_alignment", "original_image" },
                { "orientation", "align_image" },
                { "enable_image_autofix", true },   // 原画裁剪分辨率低，让服务端先补全增强
                { "auto_size", false },
            };
            if (tier == QualityTier.Ultra) body["geometry_quality"] = "detailed";
            else body["face_limit"] = tier == QualityTier.High ? 150000 : 60000;

            // 多视角：有背面图时走 multiview-to-model（view-key 格式）
            string endpoint = "/generation/image-to-model";
            if (images.Count >= 2)
            {
                string backToken = await Upload(images[1], spec.id + "_back");
                body.Remove("input");
                body.Remove("enable_image_autofix");
                body["inputs"] = new List<object>
                {
                    new Dictionary<string, object> { { "front", token } },
                    new Dictionary<string, object> { { "back", backToken } },
                };
                endpoint = "/generation/multiview-to-model";
            }

            var (ok, code, resp) = await Send(Request(HttpMethod.Post, Base + endpoint, body));
            if (!ok) throw new Exception($"Tripo 创建任务失败 HTTP {code}: {resp}");
            var json = MiniJson.Parse(resp);
            string id = MiniJson.GetString(json, "data.task_id");
            if (string.IsNullOrEmpty(id)) throw new Exception("Tripo 返回里没有 task_id: " + resp);
            log?.Invoke($"[Tripo] 任务已创建 {id}（{tier}）");
            return id;
        }

        private async Task<string> Upload(byte[] image, string name)
        {
            bool isJpeg = image.Length > 3 && image[0] == 0xFF && image[1] == 0xD8;
            var upload = Request(HttpMethod.Post, Base + "/files");
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(image);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(isJpeg ? "image/jpeg" : "image/png");
            form.Add(file, "file", name + (isJpeg ? ".jpg" : ".png"));
            upload.Content = form;
            var (ok, code, resp) = await Send(upload);
            if (!ok) throw new Exception($"Tripo 上传失败 HTTP {code}: {resp}");
            var json = MiniJson.Parse(resp);
            string token = MiniJson.GetString(json, "data.file_token") ?? MiniJson.GetString(json, "data.image_token");
            if (string.IsNullOrEmpty(token)) throw new Exception("Tripo 上传返回里没有 file_token: " + resp);
            return token;
        }

        public Task<ProviderTaskResult> Poll(string taskId, Action<string> log) => GetTask(taskId, false);

        private async Task<ProviderTaskResult> GetTask(string taskId, bool wantFbx)
        {
            var result = new ProviderTaskResult();
            var (ok, code, resp) = await Send(Request(HttpMethod.Get, Base + "/tasks/" + taskId));
            result.raw = resp;
            if (!ok)
            {
                result.status = ProviderTaskStatus.Failed;
                result.error = $"HTTP {code}: {resp}";
                return result;
            }
            var json = MiniJson.Parse(resp);
            string status = (MiniJson.GetString(json, "data.status") ?? "").ToLowerInvariant();
            result.progress = (int)MiniJson.GetNumber(json, "data.progress");
            if (status == "failed" || status == "cancelled" || status == "banned" || status == "expired" || status == "unknown")
            {
                result.status = ProviderTaskStatus.Failed;
                result.error = MiniJson.GetString(json, "data.error") ?? MiniJson.GetString(json, "data.message") ?? MiniJson.GetString(json, "message") ?? status;
                return result;
            }
            if (status != "success")
            {
                result.status = status == "running" ? ProviderTaskStatus.Running : ProviderTaskStatus.Pending;
                return result;
            }
            result.status = ProviderTaskStatus.Succeeded;
            string url = MiniJson.GetString(json, "data.output.model_url")
                ?? MiniJson.GetString(json, "data.output.pbr_model")
                ?? MiniJson.GetString(json, "data.output.model")
                ?? MiniJson.GetString(json, "data.output.model_urls.0");
            if (string.IsNullOrEmpty(url)) { result.status = ProviderTaskStatus.Failed; result.error = "任务成功但没有模型地址: " + resp; return result; }
            if (wantFbx || url.ToLowerInvariant().Contains(".fbx")) result.fbxUrl = url; else result.glbUrl = url;
            result.baseColorUrl = MiniJson.GetString(json, "data.output.base_color");
            return result;
        }

        // ---------- 绑骨与动画 ----------

        public async Task<string> CreateRigTask(string modelTaskId, ReferencePropSpec spec, Action<string> log)
        {
            // 可选的 rig-check 免费；失败也不阻塞
            try
            {
                var (okc, _, respc) = await Send(Request(HttpMethod.Post, Base + "/animations/rig-check", new Dictionary<string, object> { { "input", modelTaskId } }));
                if (okc)
                {
                    var jc = MiniJson.Parse(respc);
                    var riggable = MiniJson.Get(jc, "data.riggable") ?? MiniJson.Get(jc, "data.output.riggable");
                    if (riggable is bool b && !b) log?.Invoke("[Tripo] rig-check 认为该模型不适合绑骨，仍尝试绑骨");
                }
            }
            catch { /* 可选 */ }

            var body = new Dictionary<string, object>
            {
                { "input", modelTaskId },
                { "model", RigModel },
                { "rig_type", "biped" },
                { "spec", "mixamo" },
                { "out_format", "fbx" },
            };
            var (ok, code, resp) = await Send(Request(HttpMethod.Post, Base + "/animations/rig", body));
            if (!ok) throw new Exception($"Tripo 绑骨失败 HTTP {code}: {resp}");
            string id = MiniJson.GetString(MiniJson.Parse(resp), "data.task_id");
            if (string.IsNullOrEmpty(id)) throw new Exception("Tripo 绑骨返回里没有 task_id: " + resp);
            log?.Invoke($"[Tripo] 绑骨任务 {id}（25 Credits）");
            return id;
        }

        public Task<ProviderTaskResult> PollRig(string rigTaskId, Action<string> log) => GetTask(rigTaskId, true);

        /// <summary>Meshy 动作库编号 → Tripo v1.0 biped 预设。</summary>
        public static string PresetFor(int meshyActionId)
        {
            switch (meshyActionId)
            {
                case 30: return "preset:biped:walk";
                case 32:
                case 33: return "preset:biped:sit";
                case 56: return "preset:biped:fold_arms";
                case 11: return "preset:biped:standing_relax";
                case 12: return "preset:biped:look_around";
                default: return "preset:biped:idle";
            }
        }

        public async Task<string> CreateAnimationTask(string rigTaskId, int[] actionIds, Action<string> log)
        {
            string preset = PresetFor(actionIds != null && actionIds.Length > 0 ? actionIds[0] : 0);
            var body = new Dictionary<string, object>
            {
                { "input", rigTaskId },
                { "animation", preset },
                { "out_format", "fbx" },
                { "export_with_geometry", true },
                { "animate_in_place", true },
            };
            var (ok, code, resp) = await Send(Request(HttpMethod.Post, Base + "/animations/retarget", body));
            if (!ok) throw new Exception($"Tripo 动画失败 HTTP {code}: {resp}");
            string id = MiniJson.GetString(MiniJson.Parse(resp), "data.task_id");
            if (string.IsNullOrEmpty(id)) throw new Exception("Tripo 动画返回里没有 task_id: " + resp);
            log?.Invoke($"[Tripo] 动画任务 {id}：{preset}（10 Credits）");
            return id;
        }

        public Task<ProviderTaskResult> PollAnimation(string animTaskId, Action<string> log) => GetTask(animTaskId, true);
    }
}
