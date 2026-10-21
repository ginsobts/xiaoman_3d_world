using System;
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
        public string error;
        public string raw;
        /// <summary>多阶段任务（如 Tripo 生成 → 转换）切换到下一阶段时给出的新任务 id。</summary>
        public string nextTaskId;
    }

    /// <summary>图生 3D 服务抽象。换服务商只需实现这三个方法。</summary>
    public interface IImageTo3DProvider
    {
        string Name { get; }
        string KeyPrefName { get; }
        /// <summary>提交任务，返回任务 id。</summary>
        Task<string> CreateTask(byte[] png, ReferencePropSpec spec, Action<string> log);
        /// <summary>轮询任务。成功时需填好 fbxUrl（或 glbUrl）与 baseColorUrl。</summary>
        Task<ProviderTaskResult> Poll(string taskId, Action<string> log);
    }

    public abstract class HttpProviderBase
    {
        protected static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("XiaomanWorld-AI3D/1.0");
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
    }
}
