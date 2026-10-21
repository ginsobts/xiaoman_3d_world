using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;

namespace VN.EditorTools.AI3D
{
    /// <summary>一个 Marble 世界生成完成后的关键信息。</summary>
    public class MarbleWorld
    {
        public string worldId;
        public string marbleUrl;
        public string spzFullRes;
        public string spz500k;
        public string spz100k;
        public string colliderGlbUrl;
        public string panoUrl;
        public string caption;
        public float metricScale = 1f;
        public float groundOffset;
        public string raw;

        public string SpzFor(string resolution)
        {
            switch (resolution)
            {
                case "500k": return spz500k ?? spzFullRes;
                case "100k": return spz100k ?? spz500k ?? spzFullRes;
                default: return spzFullRes ?? spz500k;
            }
        }
    }

    public class MarbleOperation
    {
        public bool done;
        public string error;
        public string status;
        public string description;
        public MarbleWorld world;
        public string raw;
    }

    /// <summary>
    /// World Labs Marble API（图生 3D 世界 / 高斯泼溅）。
    /// 文档：https://docs.worldlabs.ai/api
    /// </summary>
    public class MarbleProvider : HttpProviderBase
    {
        public const string Pref = "Xiaoman.AI3D.MarbleKey";
        public const string ModelPref = "Xiaoman.AI3D.MarbleModel";
        public static readonly string[] Models = { "marble-1.1-plus", "marble-1.1", "marble-1.0", "marble-1.0-draft" };
        private const string Root = "https://api.worldlabs.ai/marble/v1";

        public static string Model
        {
            get => EditorPrefs.GetString(ModelPref, Models[0]);
            set => EditorPrefs.SetString(ModelPref, value);
        }

        public MarbleProvider(string apiKey) : base(apiKey) { }

        private HttpRequestMessage MarbleRequest(HttpMethod method, string url, object body = null)
        {
            var req = new HttpRequestMessage(method, url);
            req.Headers.TryAddWithoutValidation("WLT-Api-Key", ApiKey);
            if (body != null) req.Content = new StringContent(MiniJson.Serialize(body), Encoding.UTF8, "application/json");
            return req;
        }

        /// <summary>上传本地图片为 media asset，返回 id。</summary>
        public async Task<string> UploadImage(byte[] image, string fileName, Action<string> log)
        {
            string ext = fileName.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "png" : "jpg";
            var prep = new Dictionary<string, object> { { "file_name", fileName }, { "kind", "image" }, { "extension", ext } };
            var (ok, code, resp) = await Send(MarbleRequest(HttpMethod.Post, Root + "/media-assets:prepare_upload", prep));
            if (!ok) throw new Exception($"Marble 准备上传失败 HTTP {code}: {resp}");
            var json = MiniJson.Parse(resp);
            string id = MiniJson.GetString(json, "media_asset.id");
            string uploadUrl = MiniJson.GetString(json, "upload_info.upload_url");
            string method = MiniJson.GetString(json, "upload_info.upload_method") ?? "PUT";
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(uploadUrl)) throw new Exception("Marble 上传信息不完整: " + resp);

            var put = new HttpRequestMessage(new HttpMethod(method), uploadUrl) { Content = new ByteArrayContent(image) };
            put.Content.Headers.TryAddWithoutValidation("Content-Type", ext == "png" ? "image/png" : "image/jpeg");
            if (MiniJson.Get(json, "upload_info.required_headers") is Dictionary<string, object> headers)
            {
                foreach (var kv in headers)
                {
                    string v = kv.Value?.ToString() ?? "";
                    if (kv.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))
                    {
                        put.Content.Headers.Remove("Content-Type");
                        put.Content.Headers.TryAddWithoutValidation("Content-Type", v);
                    }
                    else if (!put.Headers.TryAddWithoutValidation(kv.Key, v))
                        put.Content.Headers.TryAddWithoutValidation(kv.Key, v);
                }
            }
            using (var r = await Http.SendAsync(put).ConfigureAwait(false))
            {
                if (!r.IsSuccessStatusCode)
                    throw new Exception($"Marble 图片上传失败 HTTP {(int)r.StatusCode}: {await r.Content.ReadAsStringAsync().ConfigureAwait(false)}");
            }
            log?.Invoke($"[Marble] 图片已上传 {id} ({image.Length / 1024} KB)");
            return id;
        }

        /// <summary>用一张图（+ 文字提示）生成世界，返回 operation id。</summary>
        public async Task<string> Generate(string mediaAssetId, string prompt, string displayName, Action<string> log)
        {
            var body = new Dictionary<string, object>
            {
                { "display_name", displayName },
                { "model", Model },
                { "world_prompt", new Dictionary<string, object>
                    {
                        { "type", "image" },
                        { "image_prompt", new Dictionary<string, object> { { "source", "media_asset" }, { "media_asset_id", mediaAssetId } } },
                        { "text_prompt", string.IsNullOrEmpty(prompt) ? null : prompt },
                    }
                },
            };
            var (ok, code, resp) = await Send(MarbleRequest(HttpMethod.Post, Root + "/worlds:generate", body));
            if (!ok) throw new Exception($"Marble 创建世界失败 HTTP {code}: {resp}");
            string op = MiniJson.GetString(MiniJson.Parse(resp), "operation_id");
            if (string.IsNullOrEmpty(op)) throw new Exception("Marble 返回里没有 operation_id: " + resp);
            log?.Invoke($"[Marble] 世界生成已提交 {op}（模型 {Model}）");
            return op;
        }

        public async Task<MarbleOperation> PollOperation(string operationId)
        {
            var result = new MarbleOperation();
            var (ok, code, resp) = await Send(MarbleRequest(HttpMethod.Get, Root + "/operations/" + operationId));
            result.raw = resp;
            if (!ok) { result.done = true; result.error = $"HTTP {code}: {resp}"; return result; }
            var json = MiniJson.Parse(resp);
            result.done = MiniJson.Get(json, "done") is bool b && b;
            result.status = MiniJson.GetString(json, "metadata.progress.status");
            result.description = MiniJson.GetString(json, "metadata.progress.description");
            var err = MiniJson.Get(json, "error");
            if (err != null && !(err is string s && string.IsNullOrEmpty(s)))
                result.error = MiniJson.GetString(json, "error.message") ?? err.ToString();
            if (result.done && string.IsNullOrEmpty(result.error) && MiniJson.Get(json, "response") != null)
                result.world = ParseWorld(MiniJson.Get(json, "response"), resp);
            return result;
        }

        public async Task<MarbleWorld> GetWorld(string worldId)
        {
            var (ok, code, resp) = await Send(MarbleRequest(HttpMethod.Get, Root + "/worlds/" + worldId));
            if (!ok) throw new Exception($"Marble 读取世界失败 HTTP {code}: {resp}");
            return ParseWorld(MiniJson.Parse(resp), resp);
        }

        /// <summary>导出 PLY（SPZ 读不了时的备选）；通常同步返回下载地址。</summary>
        public async Task<string> ExportPly(string worldId, string resolution, Action<string> log)
        {
            var body = new Dictionary<string, object> { { "asset_type", "splats" }, { "format", "ply" }, { "resolution", resolution } };
            var (ok, code, resp) = await Send(MarbleRequest(HttpMethod.Post, Root + "/worlds/" + worldId + ":export", body));
            if (!ok) throw new Exception($"Marble 导出失败 HTTP {code}: {resp}");
            var json = MiniJson.Parse(resp);
            string url = MiniJson.GetString(json, "response.url");
            string op = MiniJson.GetString(json, "operation_id");
            var started = DateTime.UtcNow;
            while (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(op))
            {
                await Task.Delay(5000);
                var (ok2, code2, resp2) = await Send(MarbleRequest(HttpMethod.Get, Root + "/operations/" + op));
                if (!ok2) throw new Exception($"Marble 导出轮询失败 HTTP {code2}: {resp2}");
                var j2 = MiniJson.Parse(resp2);
                url = MiniJson.GetString(j2, "response.url");
                string err = MiniJson.GetString(j2, "error.message");
                if (!string.IsNullOrEmpty(err)) throw new Exception("Marble 导出失败: " + err);
                if ((DateTime.UtcNow - started).TotalMinutes > 20) throw new Exception("Marble 导出超时");
            }
            log?.Invoke("[Marble] PLY 导出完成");
            return url;
        }

        private static MarbleWorld ParseWorld(object json, string raw)
        {
            var w = new MarbleWorld
            {
                worldId = MiniJson.GetString(json, "id"),
                marbleUrl = MiniJson.GetString(json, "world_marble_url"),
                spzFullRes = MiniJson.GetString(json, "assets.splats.spz_urls.full_res"),
                spz500k = MiniJson.GetString(json, "assets.splats.spz_urls.500k"),
                spz100k = MiniJson.GetString(json, "assets.splats.spz_urls.100k"),
                colliderGlbUrl = MiniJson.GetString(json, "assets.mesh.collider_mesh_url"),
                panoUrl = MiniJson.GetString(json, "assets.imagery.pano_url"),
                caption = MiniJson.GetString(json, "assets.caption"),
                metricScale = (float)MiniJson.GetNumber(json, "assets.splats.semantics_metadata.metric_scale_factor", 1.0),
                groundOffset = (float)MiniJson.GetNumber(json, "assets.splats.semantics_metadata.ground_plane_offset", 0.0),
                raw = raw,
            };
            if (w.metricScale <= 0f) w.metricScale = 1f;
            return w;
        }
    }
}
