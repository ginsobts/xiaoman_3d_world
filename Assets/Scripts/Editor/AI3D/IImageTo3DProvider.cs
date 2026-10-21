using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using VN.World;

namespace VN.EditorTools.AI3D
{
    public enum ProviderTaskStatus { Pending, Running, Succeeded, Failed }

    public class ProviderTaskResult
    {
        public ProviderTaskStatus status = ProviderTaskStatus.Pending;
        public int progress;
        public string fbxUrl;
        public string glbUrl;
        public string baseColorUrl;
        public string normalUrl;
        public string error;
        public string raw;
        /// <summary>多阶段任务（如 Tripo 生成 → 转换）切换到下一阶段时给出的新任务 id。</summary>
        public string nextTaskId;
        /// <summary>绑骨阶段附带的基础行走动画（FBX，含蒙皮）。</summary>
        public string walkingFbxUrl;
    }

    /// <summary>
    /// 图生 3D 服务抽象。道具只需要 CreateTask/Poll；人物额外经过 CreateRigTask/PollRig 与
    /// CreateAnimationTask/PollAnimation。不支持绑骨的服务商把 SupportsRigging 设为 false 即可。
    /// </summary>
    public interface IImageTo3DProvider
    {
        string Name { get; }
        string KeyPrefName { get; }
        bool SupportsRigging { get; }
        /// <summary>提交任务，images[0] 为正面主视图，其余为背面/侧面；返回任务 id。</summary>
        Task<string> CreateTask(IList<byte[]> images, ReferencePropSpec spec, Action<string> log);
        /// <summary>轮询任务。成功时需填好 fbxUrl（或 glbUrl）与 baseColorUrl。</summary>
        Task<ProviderTaskResult> Poll(string taskId, Action<string> log);
        /// <summary>对已完成的模型任务自动绑骨，返回绑骨任务 id。</summary>
        Task<string> CreateRigTask(string modelTaskId, ReferencePropSpec spec, Action<string> log);
        /// <summary>轮询绑骨任务。成功时 fbxUrl = 绑好骨的角色 FBX。</summary>
        Task<ProviderTaskResult> PollRig(string rigTaskId, Action<string> log);
        /// <summary>给绑好骨的角色套用一组预设动画，返回动画任务 id。</summary>
        Task<string> CreateAnimationTask(string rigTaskId, int[] actionIds, Action<string> log);
        /// <summary>轮询动画任务。成功时 fbxUrl = 含蒙皮与全部动画片段的 FBX。</summary>
        Task<ProviderTaskResult> PollAnimation(string animTaskId, Action<string> log);
    }

    public abstract class HttpProviderBase
    {
        protected static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XiaomanWorld-AI3D/1.1");
            return client;
        }

        protected string ApiKey;

        protected HttpProviderBase(string apiKey)
        {
            ApiKey = apiKey ?? "";
        }

        protected HttpRequestMessage Request(HttpMethod method, string url, object jsonBody = null)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", ApiKey);
            if (jsonBody != null)
                req.Content = new StringContent(MiniJson.Serialize(jsonBody), Encoding.UTF8, "application/json");
            return req;
        }

        protected static async Task<(bool ok, int code, string body)> Send(HttpRequestMessage req)
        {
            using (var resp = await Http.SendAsync(req).ConfigureAwait(false))
            {
                string body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                return (resp.IsSuccessStatusCode, (int)resp.StatusCode, body);
            }
        }

        public static async Task<byte[]> Download(string url)
        {
            using (var resp = await Http.GetAsync(url).ConfigureAwait(false))
            {
                resp.EnsureSuccessStatusCode();
                return await resp.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        /// <summary>按文件头判断 PNG / JPEG / WebP，生成 data URI。</summary>
        public static string DataUri(byte[] image)
        {
            string mime = "image/png";
            if (image.Length > 3 && image[0] == 0xFF && image[1] == 0xD8) mime = "image/jpeg";
            else if (image.Length > 12 && image[8] == (byte)'W' && image[9] == (byte)'E' && image[10] == (byte)'B' && image[11] == (byte)'P') mime = "image/webp";
            return "data:" + mime + ";base64," + Convert.ToBase64String(image);
        }
    }
}
