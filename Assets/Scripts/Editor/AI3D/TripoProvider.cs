using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using VN.World;

namespace VN.EditorTools.AI3D
{
    /// <summary>
    /// Tripo v3 图生 3D（备用服务商）。生成 GLB 后再调用 convert 转 FBX。
    /// 文档：https://platform.tripo3d.ai/docs
    /// </summary>
    public class TripoProvider : HttpProviderBase, IImageTo3DProvider
    {
        public const string Pref = "Xiaoman.AI3D.TripoKey";
        private const string Base = "https://openapi.tripo3d.ai/v3";
        private const string Model = "v3.1-20260211";

        public string Name => "Tripo v3.1";
        public string KeyPrefName => Pref;
        public bool SupportsRigging => false;

        // 任务 id 格式："gen:<id>" 表示生成阶段，"conv:<id>" 表示 FBX 转换阶段
        private readonly Dictionary<string, string> _baseColorByTask = new Dictionary<string, string>();

        public TripoProvider(string apiKey) : base(apiKey) { }

        public Task<string> CreateRigTask(string modelTaskId, ReferencePropSpec spec, Action<string> log) => throw new NotSupportedException("Tripo 备用通道不做自动绑骨，请用 Meshy 生成人物。");
        public Task<ProviderTaskResult> PollRig(string rigTaskId, Action<string> log) => throw new NotSupportedException();
        public Task<string> CreateAnimationTask(string rigTaskId, int[] actionIds, Action<string> log) => throw new NotSupportedException();
        public Task<ProviderTaskResult> PollAnimation(string animTaskId, Action<string> log) => throw new NotSupportedException();

        public async Task<string> CreateTask(IList<byte[]> images, ReferencePropSpec spec, Action<string> log)
        {
            if (images == null || images.Count == 0) throw new Exception("没有可用的参考图");
            byte[] png = images[0];
            bool isJpeg = png.Length > 3 && png[0] == 0xFF && png[1] == 0xD8;
            // 1. 上传图片（Tripo 只取正面主视图）
            var upload = Request(HttpMethod.Post, Base + "/files");
            var form = new MultipartFormDataContent();
            var file = new ByteArrayContent(png);
            file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(isJpeg ? "image/jpeg" : "image/png");
            form.Add(file, "file", spec.id + (isJpeg ? ".jpg" : ".png"));
            upload.Content = form;
            var (ok, code, resp) = await Send(upload);
            if (!ok) throw new Exception($"Tripo 上传失败 HTTP {code}: {resp}");
            var json = MiniJson.Parse(resp);
            string token = MiniJson.GetString(json, "data.file_token") ?? MiniJson.GetString(json, "data.image_token");
            if (string.IsNullOrEmpty(token)) throw new Exception("Tripo 上传返回里没有 file_token: " + resp);

            // 2. 创建任务
            var body = new Dictionary<string, object>
            {
                { "model", Model },
                { "input", new Dictionary<string, object> { { "type", isJpeg ? "jpg" : "png" }, { "file_token", token } } },
                { "texture", true },
                { "pbr", true },
                { "texture_quality", "detailed" },
                { "face_limit", 150000 },
            };
            var (ok2, code2, resp2) = await Send(Request(HttpMethod.Post, Base + "/generation/image-to-model", body));
            if (!ok2) throw new Exception($"Tripo 创建任务失败 HTTP {code2}: {resp2}");
            var json2 = MiniJson.Parse(resp2);
            string id = MiniJson.GetString(json2, "data.task_id") ?? MiniJson.GetString(json2, "data.id");
            if (string.IsNullOrEmpty(id)) throw new Exception("Tripo 返回里没有 task_id: " + resp2);
            log?.Invoke($"[Tripo] 任务已创建 {id}");
            return "gen:" + id;
        }

        public async Task<ProviderTaskResult> Poll(string taskId, Action<string> log)
        {
            var result = new ProviderTaskResult();
            bool converting = taskId.StartsWith("conv:");
            string rawId = taskId.Substring(taskId.IndexOf(':') + 1);
            var (ok, code, resp) = await Send(Request(HttpMethod.Get, Base + "/tasks/" + rawId));
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
                result.error = MiniJson.GetString(json, "data.error") ?? MiniJson.GetString(json, "message") ?? status;
                return result;
            }
            if (status != "success")
            {
                result.status = status == "running" ? ProviderTaskStatus.Running : ProviderTaskStatus.Pending;
                return result;
            }

            if (!converting)
            {
                // 生成完成：记录 GLB 与贴图，再发起 FBX 转换
                string glb = MiniJson.GetString(json, "data.output.pbr_model") ?? MiniJson.GetString(json, "data.output.model");
                string baseColor = MiniJson.GetString(json, "data.output.base_color") ?? MiniJson.GetString(json, "data.output.rendered_image");
                var body = new Dictionary<string, object> { { "input", rawId }, { "format", "FBX" }, { "quad", false }, { "face_limit", 150000 } };
                var (ok2, code2, resp2) = await Send(Request(HttpMethod.Post, Base + "/models/convert", body));
                if (!ok2)
                {
                    // 转换不可用时直接交出 GLB，由导入器决定如何处理
                    log?.Invoke($"[Tripo] FBX 转换失败 HTTP {code2}，改用 GLB: {resp2}");
                    result.status = ProviderTaskStatus.Succeeded;
                    result.glbUrl = glb;
                    result.baseColorUrl = baseColor;
                    return result;
                }
                string convId = MiniJson.GetString(MiniJson.Parse(resp2), "data.task_id");
                _baseColorByTask["conv:" + convId] = baseColor;
                log?.Invoke($"[Tripo] 生成完成，开始转换 FBX {convId}");
                result.status = ProviderTaskStatus.Running;
                result.nextTaskId = "conv:" + convId;
                result.progress = 90;
                return result;
            }

            result.status = ProviderTaskStatus.Succeeded;
            result.fbxUrl = MiniJson.GetString(json, "data.output.model_url") ?? MiniJson.GetString(json, "data.output.model");
            _baseColorByTask.TryGetValue(taskId, out result.baseColorUrl);
            return result;
        }
    }
}
