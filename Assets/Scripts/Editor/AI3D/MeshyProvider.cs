using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// Meshy OpenAPI v1（Meshy 7.1）最高精度管线：
    ///  - 单图：/image-to-3d，geometry_resolution 4k；多视角：/multi-image-to-3d，geometry_resolution 2k
    ///  - 4K 贴图 + PBR，目标面数由 QualityTier 决定
    ///  - 人物：pose_mode a-pose → /rigging 自动绑骨 → /animations 预设动画（一个文件多段 clip）
    /// 文档：https://docs.meshy.ai/en/api/image-to-3d
    /// </summary>
    public class MeshyProvider : HttpProviderBase, IImageTo3DProvider
    {
        public const string Pref = "Xiaoman.AI3D.MeshyKey";
        public const string TierPref = "Xiaoman.AI3D.MeshyTier";
        private const string Root = "https://api.meshy.ai/openapi/v1";

        /// <summary>精度档位。Ultra 不做减面（Meshy 推荐的最高质量），High 为 15 万面，Standard 为 5 万面。</summary>
        public enum QualityTier { Standard = 0, High = 1, Ultra = 2 }

        public static QualityTier Tier
        {
            get => (QualityTier)EditorPrefs.GetInt(TierPref, (int)QualityTier.High);
            set => EditorPrefs.SetInt(TierPref, (int)value);
        }

        public string Name => "Meshy 7.1";
        public string KeyPrefName => Pref;
        public bool SupportsRigging => true;

        public MeshyProvider(string apiKey) : base(apiKey) { }

        // 任务 id 前缀："img:" 单图 / "multi:" 多图，用来选对查询接口
        private static string Endpoint(string taskId, out string rawId)
        {
            int i = taskId.IndexOf(':');
            string kind = i > 0 ? taskId.Substring(0, i) : "img";
            rawId = i > 0 ? taskId.Substring(i + 1) : taskId;
            return kind == "multi" ? Root + "/multi-image-to-3d" : Root + "/image-to-3d";
        }

        public async Task<string> CreateTask(IList<byte[]> images, ReferencePropSpec spec, Action<string> log)
        {
            if (images == null || images.Count == 0) throw new Exception("没有可用的参考图");
            bool multi = images.Count > 1;
            var tier = Tier;
            var body = new Dictionary<string, object>
            {
                { "ai_model", "meshy-7.1" },
                { "geometry_resolution", multi ? "2k" : "4k" },
                { "should_texture", true },
                { "enable_pbr", true },
                { "texture_resolution", "4k" },
                { "image_enhancement", true },
                { "remove_lighting", true },
                { "target_formats", new List<object> { "fbx", "glb" } },
            };
            if (multi)
            {
                var urls = new List<object>();
                foreach (var img in images) urls.Add(DataUri(img));
                body["image_urls"] = urls;
            }
            else body["image_url"] = DataUri(images[0]);

            if (tier == QualityTier.Ultra && !spec.isCharacter)
            {
                body["should_remesh"] = false;
            }
            else
            {
                body["should_remesh"] = true;
                body["topology"] = "triangle";
                // 绑骨上限 30 万面；人物取 10 万面足够细腻且蒙皮稳定
                int poly = spec.isCharacter ? 100000 : (tier == QualityTier.Standard ? 50000 : 150000);
                body["target_polycount"] = poly;
            }
            if (spec.isCharacter) body["pose_mode"] = "a-pose";
            if (!string.IsNullOrEmpty(spec.prompt))
                body["texture_prompt"] = spec.prompt.Length > 800 ? spec.prompt.Substring(0, 800) : spec.prompt;

            string url = multi ? Root + "/multi-image-to-3d" : Root + "/image-to-3d";
            var (ok, code, resp) = await Send(Request(HttpMethod.Post, url, body));
            if (!ok) throw new Exception($"Meshy 创建任务失败 HTTP {code}: {resp}");
            string id = MiniJson.GetString(MiniJson.Parse(resp), "result");
            if (string.IsNullOrEmpty(id)) throw new Exception("Meshy 返回里没有任务 id: " + resp);
            log?.Invoke($"[Meshy] {(multi ? images.Count + " 视角" : "单图")} 任务已创建 {id}（{tier}，几何 {(multi ? "2k" : "4k")}，贴图 4k）");
            return (multi ? "multi:" : "img:") + id;
        }

        public async Task<ProviderTaskResult> Poll(string taskId, Action<string> log)
        {
            string endpoint = Endpoint(taskId, out string rawId);
            var result = await GetTask(endpoint + "/" + rawId);
            if (result.status != ProviderTaskStatus.Succeeded) return result;
            var json = MiniJson.Parse(result.raw);
            result.fbxUrl = MiniJson.GetString(json, "model_urls.fbx");
            result.glbUrl = MiniJson.GetString(json, "model_urls.glb");
            result.baseColorUrl = MiniJson.GetString(json, "texture_urls.0.base_color");
            result.normalUrl = MiniJson.GetString(json, "texture_urls.0.normal");
            return result;
        }

        public async Task<string> CreateRigTask(string modelTaskId, ReferencePropSpec spec, Action<string> log)
        {
            Endpoint(modelTaskId, out string rawId);
            var body = new Dictionary<string, object>
            {
                { "input_task_id", rawId },
                { "height_meters", Math.Max(0.3f, spec.targetHeight) },
            };
            var (ok, code, resp) = await Send(Request(HttpMethod.Post, Root + "/rigging", body));
            if (!ok) throw new Exception($"Meshy 绑骨失败 HTTP {code}: {resp}");
            string id = MiniJson.GetString(MiniJson.Parse(resp), "result");
            if (string.IsNullOrEmpty(id)) throw new Exception("Meshy 绑骨返回里没有任务 id: " + resp);
            log?.Invoke($"[Meshy] 绑骨任务已创建 {id}（身高 {spec.targetHeight:0.00} m）");
            return id;
        }

        public async Task<ProviderTaskResult> PollRig(string rigTaskId, Action<string> log)
        {
            var result = await GetTask(Root + "/rigging/" + rigTaskId);
            if (result.status != ProviderTaskStatus.Succeeded) return result;
            var json = MiniJson.Parse(result.raw);
            result.fbxUrl = MiniJson.GetString(json, "result.rigged_character_fbx_url");
            result.glbUrl = MiniJson.GetString(json, "result.rigged_character_glb_url");
            result.walkingFbxUrl = MiniJson.GetString(json, "result.basic_animations.walking_fbx_url");
            return result;
        }

        public async Task<string> CreateAnimationTask(string rigTaskId, int[] actionIds, Action<string> log)
        {
            var ids = new List<object>();
            foreach (int a in actionIds) if (!ids.Contains(a)) ids.Add(a);
            if (ids.Count == 0) ids.Add(0);
            if (ids.Count > 10) ids.RemoveRange(10, ids.Count - 10);
            var body = new Dictionary<string, object>
            {
                { "rig_task_id", rigTaskId },
                { "action_ids", ids },
            };
            var (ok, code, resp) = await Send(Request(HttpMethod.Post, Root + "/animations", body));
            if (!ok) throw new Exception($"Meshy 动画失败 HTTP {code}: {resp}");
            string id = MiniJson.GetString(MiniJson.Parse(resp), "result");
            if (string.IsNullOrEmpty(id)) throw new Exception("Meshy 动画返回里没有任务 id: " + resp);
            log?.Invoke($"[Meshy] 动画任务已创建 {id}（动作 {string.Join(",", ids)}）");
            return id;
        }

        public async Task<ProviderTaskResult> PollAnimation(string animTaskId, Action<string> log)
        {
            var result = await GetTask(Root + "/animations/" + animTaskId);
            if (result.status != ProviderTaskStatus.Succeeded) return result;
            var json = MiniJson.Parse(result.raw);
            result.fbxUrl = MiniJson.GetString(json, "result.animation_fbx_url");
            result.glbUrl = MiniJson.GetString(json, "result.animation_glb_url");
            return result;
        }

        private async Task<ProviderTaskResult> GetTask(string url)
        {
            var result = new ProviderTaskResult();
            var (ok, code, resp) = await Send(Request(HttpMethod.Get, url));
            result.raw = resp;
            if (!ok)
            {
                result.status = ProviderTaskStatus.Failed;
                result.error = $"HTTP {code}: {resp}";
                return result;
            }
            var json = MiniJson.Parse(resp);
            string status = MiniJson.GetString(json, "status") ?? "";
            result.progress = (int)MiniJson.GetNumber(json, "progress");
            switch (status)
            {
                case "SUCCEEDED": result.status = ProviderTaskStatus.Succeeded; break;
                case "FAILED":
                case "CANCELED":
                case "EXPIRED":
                    result.status = ProviderTaskStatus.Failed;
                    result.error = MiniJson.GetString(json, "task_error.message");
                    if (string.IsNullOrEmpty(result.error)) result.error = status;
                    break;
                case "IN_PROGRESS": result.status = ProviderTaskStatus.Running; break;
                default: result.status = ProviderTaskStatus.Pending; break;
            }
            return result;
        }
    }
}
