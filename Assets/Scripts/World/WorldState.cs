using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 3D 世界的全局状态：剧情变量（与 AVG 对话共享）+ 玩家位置存档。
    /// 变量用字符串存，和 VNView 的 %变量% / setVariables / conditionalNexts 完全互通。
    /// 约定："1" 代表真，空或 "0" 代表假。
    /// </summary>
    public static class WorldState
    {
        public static readonly Dictionary<string, string> Variables = new Dictionary<string, string>();

        private static string SavePath => Path.Combine(Application.persistentDataPath, "world_save.json");

        public static string Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            return Variables.TryGetValue(name, out var v) ? v : "";
        }

        public static void Set(string name, string value)
        {
            if (string.IsNullOrEmpty(name)) return;
            Variables[name] = value ?? "";
        }

        public static void SetFlag(string name, bool on = true) => Set(name, on ? "1" : "0");

        /// <summary>变量是否为真（非空且不为 "0"）。</summary>
        public static bool Is(string name)
        {
            string v = Get(name);
            return !string.IsNullOrEmpty(v) && v != "0";
        }

        /// <summary>把对话结束后 VNView 里的变量合并回来。</summary>
        public static void Merge(IReadOnlyDictionary<string, string> source)
        {
            if (source == null) return;
            foreach (var kv in source)
                Variables[kv.Key] = kv.Value;
        }

        public static void Reset() => Variables.Clear();

        // ---------------- 存档 ----------------

        [Serializable]
        private class SaveData
        {
            public string[] keys = new string[0];
            public string[] values = new string[0];
            public Vector3 playerPosition;
            public float playerYaw;
        }

        public static bool HasSave => File.Exists(SavePath);

        public static void Save(Vector3 playerPosition, float playerYaw)
        {
            var data = new SaveData
            {
                keys = new string[Variables.Count],
                values = new string[Variables.Count],
                playerPosition = playerPosition,
                playerYaw = playerYaw,
            };
            int i = 0;
            foreach (var kv in Variables)
            {
                data.keys[i] = kv.Key;
                data.values[i] = kv.Value;
                i++;
            }

            try
            {
                File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
                Debug.Log("[World] 已存档: " + SavePath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[World] 存档失败: " + e.Message);
            }
        }

        /// <summary>读取存档；成功时变量已被替换为存档内容。</summary>
        public static bool TryLoad(out Vector3 playerPosition, out float playerYaw)
        {
            playerPosition = Vector3.zero;
            playerYaw = 0f;
            try
            {
                if (!File.Exists(SavePath)) return false;
                var data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return false;

                Variables.Clear();
                int n = Mathf.Min(data.keys?.Length ?? 0, data.values?.Length ?? 0);
                for (int i = 0; i < n; i++)
                    if (!string.IsNullOrEmpty(data.keys[i]))
                        Variables[data.keys[i]] = data.values[i] ?? "";

                playerPosition = data.playerPosition;
                playerYaw = data.playerYaw;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[World] 读档失败: " + e.Message);
                return false;
            }
        }

        public static void DeleteSave()
        {
            try { if (File.Exists(SavePath)) File.Delete(SavePath); } catch { }
        }
    }
}
