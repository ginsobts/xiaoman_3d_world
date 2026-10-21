using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// Meshy OpenAPI v1 图生 3D（Meshy 7.1）。
    /// 文档：https://docs.meshy.ai/en/api/image-to-3d
    /// </summary>
    public class MeshyProvider : HttpProviderBase, IImageTo3DProvider
    {
        public const string Pref = "Xiaoman.AI3D.MeshyKey";
        private const string Base = "https://api.meshy.ai/openapi/v1/image-to-3d";

        public string Name => "Meshy 7.1";
        public string KeyPrefName => Pref;

        public MeshyProvider(string apiKey) : base(apiKey) { }

        public async Task<string> CreateTask(byte[] png, ReferencePropSpec spec, Action<string> log)
        {
            string dataUri = "data:image/png;base64," + Convert.ToBase64String(png);
            var body = new Dictionary<string, object>
            {
                { "image_url", dataUri },
                { "ai_model", "latest" },
                { "should_remesh", true },
                { "should_texture", true },
                { "enable_pbr", true },
                { "topology", "triangle" },
                { "target_polycount", 30000 },
                { "symmetry_mode", "auto" },
                { "texture_resolution", "2048" },
            };
            if (!string.IsNullOrEmpty(spec.prompt)) body["texture_prompt"] = spec.prompt;

            var (ok, code, resp) = await Send(Request(HttpMethod.Post, Base, body));
            if (!ok) throw new Exception($"Meshy 创建任务失败 HTTP {code}: {resp}");
            var json = MiniJson.Parse(resp);
            string id = MiniJson.GetString(json, "result");
            if (string.IsNullOrEmpty(id)) throw new Exception("Meshy 返回里没有任务 id: " + resp);
            log?.Invoke($"[Meshy] 任务已创建 {id}");
            return id;
        }

        public async Task<ProviderTaskResult> Poll(string taskId, Action<string> log)
        {
            var result = new ProviderTaskResult();
            var (ok, code, resp) = await Send(Request(HttpMethod.Get, Base + "/" + taskId));
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
                case "SUCCEEDED":
                    result.status = ProviderTaskStatus.Succeeded;
                    result.fbxUrl = MiniJson.GetString(json, "model_urls.fbx");
                    result.glbUrl = MiniJson.GetString(json, "model_urls.glb");
                    result.baseColorUrl = MiniJson.GetString(json, "texture_urls.0.base_color");
                    break;
                case "FAILED":
                case "CANCELED":
                case "EXPIRED":
                    result.status = ProviderTaskStatus.Failed;
                    result.error = MiniJson.GetString(json, "task_error.message") ?? status;
                    break;
                case "IN_PROGRESS":
                    result.status = ProviderTaskStatus.Running;
                    break;
                default:
                    result.status = ProviderTaskStatus.Pending;
                    break;
            }
            return result;
        }
    }
}
