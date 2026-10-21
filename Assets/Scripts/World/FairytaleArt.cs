using System.Collections.Generic;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 彩铅童话美术工厂。用少量圆润基础网格组合出可直接游玩的低模资产，
    /// 再叠加生成的彩铅贴图和专用 Shader；没有贴图时也会以配色正常显示。
    /// 所有方法都返回一个独立根节点，方便以后逐个替换为正式 FBX / Prefab。
    /// </summary>
    public static class FairytaleArt
    {
        public static readonly Color Ink = Hex("50465B");
        public static readonly Color Cream = Hex("F6EACD");
        public static readonly Color Rose = Hex("C98491");
        public static readonly Color Teal = Hex("5F9992");
        public static readonly Color Blue = Hex("668EBB");
        public static readonly Color Mustard = Hex("D8AE54");
        public static readonly Color Sage = Hex("779B68");
        public static readonly Color LeafLight = Hex("9DBB72");
        public static readonly Color Bark = Hex("76543E");
        public static readonly Color Wood = Hex("A9784D");
        public static readonly Color Stone = Hex("9189A3");
        public static readonly Color Roof = Hex("A85F4C");
        public static readonly Color Hair = Hex("49382F");
        public static readonly Color Skin = Hex("F3CFAD");
        public static readonly Color Crystal = Hex("A997DE");

        private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
        private static Shader _shader;

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }

        public static Material Material(string id, Color tint, string texture = null, bool glow = false)
        {
            string key = id + ColorUtility.ToHtmlStringRGBA(tint) + texture + glow;
            if (Materials.TryGetValue(key, out var cached) && cached != null) return cached;

            if (_shader == null) _shader = Shader.Find("Xiaoman/Fairytale Pencil");
            if (_shader == null) _shader = Shader.Find("Standard");
            var mat = new Material(_shader) { name = "Pencil_" + id };
            mat.color = tint;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);
            if (mat.HasProperty("_Roughness")) mat.SetFloat("_Roughness", 0.86f);
            if (mat.HasProperty("_HatchStrength")) mat.SetFloat("_HatchStrength", 0.08f);
            if (mat.HasProperty("_RimColor")) mat.SetColor("_RimColor", Ink);

            if (!string.IsNullOrEmpty(texture))
            {
                string path = texture.Contains("/") ? "Art/" + texture : "Art/Textures/" + texture;
                var tex = Resources.Load<Texture2D>(path);
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    mat.mainTextureScale = new Vector2(2f, 2f);
                }
            }

            if (glow && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", tint * 0.65f);
            }
            Materials[key] = mat;
            return mat;
        }

        private static GameObject Root(string name, Transform parent, Vector3 localPosition)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            return go;
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Color color, string texture = null,
            Vector3? euler = null, bool keepCollider = false, bool glow = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            if (euler.HasValue) go.transform.localRotation = Quaternion.Euler(euler.Value);
            go.GetComponent<Renderer>().sharedMaterial = Material(name, color, texture, glow);
            if (!keepCollider)
            {
                var collider = go.GetComponent<Collider>();
                if (Application.isPlaying) Object.Destroy(collider);
                else Object.DestroyImmediate(collider);
            }
            return go;
        }

        private static GameObject Box(string name, Transform parent, Vector3 center, Vector3 size,
            Color color, string texture = null, bool keepCollider = false)
        {
            return Part(PrimitiveType.Cube, name, parent, center, size, color, texture, null, keepCollider);
        }

        // ---------------- 角色 ----------------

        public static GameObject BuildExplorer(Transform parent)
        {
            var root = Root("ExplorerArt", parent, Vector3.zero);
            // 腿和靴子
            Part(PrimitiveType.Capsule, "LegL", root.transform, new Vector3(-0.16f, 0.47f, 0),
                new Vector3(0.18f, 0.35f, 0.18f), Hex("A88C69"));
            Part(PrimitiveType.Capsule, "LegR", root.transform, new Vector3(0.16f, 0.47f, 0),
                new Vector3(0.18f, 0.35f, 0.18f), Hex("A88C69"));
            Box("BootL", root.transform, new Vector3(-0.16f, 0.16f, 0.08f), new Vector3(0.25f, 0.24f, 0.38f), Bark);
            Box("BootR", root.transform, new Vector3(0.16f, 0.16f, 0.08f), new Vector3(0.25f, 0.24f, 0.38f), Bark);

            // 蓝外套和手臂
            Part(PrimitiveType.Capsule, "Coat", root.transform, new Vector3(0, 1.02f, 0),
                new Vector3(0.52f, 0.5f, 0.4f), Blue);
            Part(PrimitiveType.Capsule, "ArmL", root.transform, new Vector3(-0.43f, 1.03f, 0),
                new Vector3(0.16f, 0.42f, 0.16f), Blue, null, new Vector3(0, 0, -9));
            Part(PrimitiveType.Capsule, "ArmR", root.transform, new Vector3(0.43f, 1.03f, 0),
                new Vector3(0.16f, 0.42f, 0.16f), Blue, null, new Vector3(0, 0, 9));

            // 头、头发、围巾
            Part(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0, 1.65f, 0),
                new Vector3(0.55f, 0.58f, 0.5f), Skin);
            Part(PrimitiveType.Sphere, "HairCap", root.transform, new Vector3(0, 1.82f, -0.02f),
                new Vector3(0.59f, 0.42f, 0.54f), Hair);
            for (int i = -2; i <= 2; i++)
                Part(PrimitiveType.Sphere, "HairTuft", root.transform,
                    new Vector3(i * 0.105f, 1.99f + (2 - Mathf.Abs(i)) * 0.025f, 0.07f),
                    Vector3.one * 0.18f, Hair);
            Part(PrimitiveType.Cylinder, "Scarf", root.transform, new Vector3(0, 1.4f, 0),
                new Vector3(0.36f, 0.08f, 0.36f), Mustard);
            Box("ScarfTail", root.transform, new Vector3(0.2f, 1.2f, 0.31f),
                new Vector3(0.16f, 0.42f, 0.07f), Mustard);

            // 背包
            Box("Satchel", root.transform, new Vector3(0.48f, 0.91f, -0.04f),
                new Vector3(0.28f, 0.42f, 0.18f), Wood);
            root.AddComponent<FairytaleWalker>();
            return root;
        }

        public static GameObject BuildXiaoman(Transform parent)
        {
            var root = Root("XiaomanArt", parent, Vector3.zero);
            // 裙子由倒置圆锥近似（圆柱底部更宽）
            Part(PrimitiveType.Cylinder, "Skirt", root.transform, new Vector3(0, 0.58f, 0),
                new Vector3(0.55f, 0.55f, 0.55f), Teal);
            Box("Blouse", root.transform, new Vector3(0, 1.12f, 0),
                new Vector3(0.63f, 0.55f, 0.36f), Cream);
            Part(PrimitiveType.Capsule, "SleeveL", root.transform, new Vector3(-0.42f, 1.05f, 0),
                new Vector3(0.18f, 0.42f, 0.18f), Cream, null, new Vector3(0, 0, -7));
            Part(PrimitiveType.Capsule, "SleeveR", root.transform, new Vector3(0.42f, 1.05f, 0),
                new Vector3(0.18f, 0.42f, 0.18f), Cream, null, new Vector3(0, 0, 7));
            Part(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0, 1.68f, 0),
                new Vector3(0.56f, 0.59f, 0.51f), Skin);
            Part(PrimitiveType.Sphere, "Hair", root.transform, new Vector3(0, 1.77f, -0.09f),
                new Vector3(0.66f, 0.67f, 0.56f), Hair);
            // 露出脸部的浅色椭球
            Part(PrimitiveType.Sphere, "Face", root.transform, new Vector3(0, 1.68f, 0.18f),
                new Vector3(0.49f, 0.49f, 0.3f), Skin);
            // 玫瑰披肩 + 星星胸针
            Part(PrimitiveType.Cylinder, "Cape", root.transform, new Vector3(0, 1.34f, 0),
                new Vector3(0.48f, 0.12f, 0.42f), Rose);
            Part(PrimitiveType.Sphere, "StarBrooch", root.transform, new Vector3(0, 1.38f, 0.38f),
                Vector3.one * 0.11f, Mustard, null, null, false, true);
            Box("BootL", root.transform, new Vector3(-0.18f, 0.12f, 0.05f), new Vector3(0.24f, 0.24f, 0.35f), Bark);
            Box("BootR", root.transform, new Vector3(0.18f, 0.12f, 0.05f), new Vector3(0.24f, 0.24f, 0.35f), Bark);
            return root;
        }

        // ---------------- 环境模型 ----------------

        public static GameObject BuildBackdrop(Transform parent)
        {
            var root = Root("PencilLandscapeBackdrop", parent, Vector3.zero);
            var mat = Material("Landscape", Color.white, "UI/SkyPanorama");
            mat.mainTextureScale = Vector2.one;
            // 四幅远景像舞台布景一样围住可玩区域；轻微雾色让接缝不抢眼。
            foreach (var data in new[]
                     {
                         (new Vector3(-21.5f, 12f, 43f), new Vector3(43f, 24f, 0.2f), 180f),
                         (new Vector3(21.5f, 12f, 43f), new Vector3(43f, 24f, 0.2f), 180f),
                         (new Vector3(-21.5f, 12f, -43f), new Vector3(43f, 24f, 0.2f), 0f),
                         (new Vector3(21.5f, 12f, -43f), new Vector3(43f, 24f, 0.2f), 0f),
                         (new Vector3(43f, 12f, -21.5f), new Vector3(43f, 24f, 0.2f), -90f),
                         (new Vector3(43f, 12f, 21.5f), new Vector3(43f, 24f, 0.2f), -90f),
                         (new Vector3(-43f, 12f, -21.5f), new Vector3(43f, 24f, 0.2f), 90f),
                         (new Vector3(-43f, 12f, 21.5f), new Vector3(43f, 24f, 0.2f), 90f),
                     })
            {
                var panel = Box("DistantHills", root.transform, data.Item1, data.Item2, Color.white);
                panel.transform.localRotation = Quaternion.Euler(0, data.Item3, 0);
                panel.GetComponent<Renderer>().sharedMaterial = mat;
            }
            return root;
        }

        public static GameObject BuildTree(Transform parent, Vector3 position, float height, int seed)
        {
            var root = Root("PencilTree", parent, position);
            var random = new System.Random(seed);
            Part(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0, height * 0.42f, 0),
                new Vector3(0.32f, height * 0.42f, 0.32f), Bark, "Bark", new Vector3(0, 0, RandomRange(random, -4f, 4f)), true);
            for (int i = 0; i < 7; i++)
            {
                float a = i / 7f * Mathf.PI * 2f;
                float radius = i == 6 ? 0f : RandomRange(random, 0.35f, 0.9f);
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, height + RandomRange(random, -0.15f, 0.75f), Mathf.Sin(a) * radius);
                float size = RandomRange(random, 1.15f, 1.75f);
                Color c = i % 2 == 0 ? Sage : LeafLight;
                Part(PrimitiveType.Sphere, "CrayonCanopy", root.transform, p,
                    new Vector3(size, size * 0.9f, size), c, "Leaves");
            }
            return root;
        }

        public static GameObject BuildCottage(Transform parent, Vector3 position, float yaw = 0f)
        {
            var root = Root("CrookedCottage", parent, position);
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("House", root.transform, new Vector3(0, 1.35f, 0), new Vector3(5.2f, 2.7f, 4.2f), Cream, "CreamPlaster", true);
            // 两块倾斜屋顶，夸张、略歪
            Box("RoofL", root.transform, new Vector3(-1.25f, 3.15f, 0), new Vector3(3.2f, 0.3f, 4.8f),
                Roof, "RoofTiles").transform.localRotation = Quaternion.Euler(0, 0, 28f);
            Box("RoofR", root.transform, new Vector3(1.25f, 3.15f, 0), new Vector3(3.2f, 0.3f, 4.8f),
                Roof, "RoofTiles").transform.localRotation = Quaternion.Euler(0, 0, -28f);
            Box("Door", root.transform, new Vector3(0, 1f, -2.13f), new Vector3(1.05f, 2f, 0.14f), Wood, "WoodPlanks");
            foreach (float x in new[] { -1.65f, 1.65f })
            {
                Box("Window", root.transform, new Vector3(x, 1.55f, -2.14f), new Vector3(0.9f, 0.9f, 0.12f), Blue);
                Box("WindowBar", root.transform, new Vector3(x, 1.55f, -2.22f), new Vector3(0.08f, 0.9f, 0.05f), Cream);
                Box("WindowBar", root.transform, new Vector3(x, 1.55f, -2.22f), new Vector3(0.9f, 0.08f, 0.05f), Cream);
            }
            Box("Chimney", root.transform, new Vector3(1.25f, 3.95f, 0.7f), new Vector3(0.62f, 1.9f, 0.62f), Stone, "LavenderStone");
            return root;
        }

        public static GameObject BuildFence(Transform parent, Vector3 position, int count, float yaw = 0f)
        {
            var root = Root("CurvedFence", parent, position);
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int i = 0; i < count; i++)
            {
                float x = (i - (count - 1) * 0.5f) * 1.05f;
                float y = Mathf.Sin(i * 0.75f) * 0.08f;
                Box("Picket", root.transform, new Vector3(x, 0.72f + y, 0), new Vector3(0.22f, 1.45f, 0.18f), Wood, "WoodPlanks", true);
            }
            Box("Rail", root.transform, new Vector3(0, 0.55f, 0.04f), new Vector3(count * 1.05f, 0.17f, 0.16f), Bark, "WoodPlanks");
            Box("Rail", root.transform, new Vector3(0, 1.05f, 0.04f), new Vector3(count * 1.05f, 0.17f, 0.16f), Bark, "WoodPlanks");
            return root;
        }

        public static GameObject BuildMushrooms(Transform parent, Vector3 position, int seed)
        {
            var root = Root("MushroomCluster", parent, position);
            var random = new System.Random(seed);
            for (int i = 0; i < 4; i++)
            {
                float h = RandomRange(random, 0.35f, 0.8f);
                Vector3 p = new Vector3(RandomRange(random, -0.6f, 0.6f), 0, RandomRange(random, -0.6f, 0.6f));
                Part(PrimitiveType.Cylinder, "Stem", root.transform, p + Vector3.up * h * 0.5f,
                    new Vector3(0.12f, h * 0.5f, 0.12f), Cream);
                Part(PrimitiveType.Sphere, "Cap", root.transform, p + Vector3.up * h,
                    new Vector3(0.42f, 0.18f, 0.42f), i % 2 == 0 ? Roof : Rose);
            }
            return root;
        }

        public static GameObject BuildFlowers(Transform parent, Vector3 position, int seed)
        {
            var root = Root("Wildflowers", parent, position);
            var random = new System.Random(seed);
            Color[] petals = { Rose, Mustard, Hex("A997C8"), Hex("86A9D3"), Cream };
            for (int i = 0; i < 9; i++)
            {
                float h = RandomRange(random, 0.35f, 0.9f);
                Vector3 p = new Vector3(RandomRange(random, -0.8f, 0.8f), 0, RandomRange(random, -0.8f, 0.8f));
                Part(PrimitiveType.Cylinder, "Stem", root.transform, p + Vector3.up * h * 0.5f,
                    new Vector3(0.025f, h * 0.5f, 0.025f), Sage);
                Part(PrimitiveType.Sphere, "Flower", root.transform, p + Vector3.up * h,
                    new Vector3(0.14f, 0.08f, 0.14f), petals[i % petals.Length]);
            }
            return root;
        }

        public static GameObject BuildWell(Transform parent, Vector3 position)
        {
            var root = Root("WishingWell", parent, position);
            for (int i = 0; i < 12; i++)
            {
                float a = i / 12f * Mathf.PI * 2f;
                Part(PrimitiveType.Cube, "Stone", root.transform,
                    new Vector3(Mathf.Cos(a) * 1.05f, 0.45f, Mathf.Sin(a) * 1.05f),
                    new Vector3(0.7f, 0.55f, 0.45f), Stone, "Cobblestone",
                    new Vector3(0, -a * Mathf.Rad2Deg, 0), true);
            }
            Box("PostL", root.transform, new Vector3(-1.2f, 1.7f, 0), new Vector3(0.22f, 2.8f, 0.22f), Bark);
            Box("PostR", root.transform, new Vector3(1.2f, 1.7f, 0), new Vector3(0.22f, 2.8f, 0.22f), Bark);
            Box("Beam", root.transform, new Vector3(0, 3f, 0), new Vector3(2.8f, 0.25f, 0.25f), Wood);
            Part(PrimitiveType.Cylinder, "Bucket", root.transform, new Vector3(0, 1.2f, 0),
                new Vector3(0.38f, 0.42f, 0.38f), Wood);
            return root;
        }

        public static GameObject BuildBench(Transform parent, Vector3 position, float yaw)
        {
            var root = Root("StorybookBench", parent, position);
            root.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            Box("Seat", root.transform, new Vector3(0, 0.58f, 0), new Vector3(2.5f, 0.22f, 0.72f), Wood, "WoodPlanks", true);
            Box("Back", root.transform, new Vector3(0, 1.25f, 0.31f), new Vector3(2.5f, 0.85f, 0.18f), Wood, "WoodPlanks");
            Box("LegL", root.transform, new Vector3(-0.9f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.55f), Bark);
            Box("LegR", root.transform, new Vector3(0.9f, 0.3f, 0), new Vector3(0.2f, 0.6f, 0.55f), Bark);
            return root;
        }

        public static GameObject BuildXylophone(Transform parent, Vector3 position, float yaw = 0f)
        {
            var root = Root("RainbowXylophone", parent, position);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box("Stand", root.transform, new Vector3(0f, 0.55f, 0f),
                new Vector3(3.2f, 0.18f, 1.25f), Bark, "WoodPlanks", true);
            Color[] colors = { Rose, Roof, Mustard, LeafLight, Teal, Blue, Crystal, Cream };
            for (int i = 0; i < 8; i++)
            {
                float x = (i - 3.5f) * 0.38f;
                float length = 1.05f - i * 0.055f;
                Box("Note_" + i, root.transform, new Vector3(x, 0.72f, 0f),
                    new Vector3(0.3f, 0.12f, length), colors[i]);
            }
            Box("LegL", root.transform, new Vector3(-1.15f, 0.25f, 0f),
                new Vector3(0.18f, 0.6f, 0.8f), Wood);
            Box("LegR", root.transform, new Vector3(1.15f, 0.25f, 0f),
                new Vector3(0.18f, 0.6f, 0.8f), Wood);
            return root;
        }

        public static GameObject BuildLantern(Transform parent, Vector3 position)
        {
            var root = Root("CrayonLantern", parent, position);
            Part(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0, 1.5f, 0),
                new Vector3(0.12f, 1.5f, 0.12f), Ink, null, null, true);
            Box("Lamp", root.transform, new Vector3(0, 3.05f, 0), new Vector3(0.62f, 0.8f, 0.62f), Mustard, null);
            Part(PrimitiveType.Sphere, "Glow", root.transform, new Vector3(0, 3.05f, 0),
                Vector3.one * 0.38f, Cream, null, null, false, true);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.78f, 0.42f);
            root.AddComponent<StorybookMotion>().mode = StorybookMotion.Mode.Glow;
            return root;
        }

        public static GameObject BuildKey(Transform parent, Vector3 position)
        {
            var root = Root("GoldenStoryKey", parent, position);
            root.transform.localRotation = Quaternion.Euler(25f, 0f, -35f);
            Part(PrimitiveType.Cylinder, "KeyBow", root.transform, new Vector3(0, 0.32f, 0),
                new Vector3(0.34f, 0.07f, 0.34f), Mustard, null, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Cylinder, "KeyHole", root.transform, new Vector3(0, 0.32f, 0.03f),
                new Vector3(0.16f, 0.08f, 0.16f), Ink, null, new Vector3(90f, 0f, 0f));
            Box("KeyShaft", root.transform, new Vector3(0, -0.2f, 0), new Vector3(0.13f, 0.85f, 0.13f), Mustard);
            Box("KeyToothA", root.transform, new Vector3(0.16f, -0.55f, 0), new Vector3(0.42f, 0.14f, 0.13f), Mustard);
            Box("KeyToothB", root.transform, new Vector3(0.11f, -0.38f, 0), new Vector3(0.3f, 0.12f, 0.13f), Mustard);
            return root;
        }

        public static GameObject BuildCrystal(Transform parent, Vector3 position)
        {
            var root = Root("CrystalCluster", parent, position);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                float h = i == 0 ? 2.1f : 1.1f + (i % 3) * 0.25f;
                Part(PrimitiveType.Cube, "CrystalShard", root.transform,
                    new Vector3(Mathf.Cos(a) * 0.45f, h * 0.5f, Mathf.Sin(a) * 0.45f),
                    new Vector3(0.34f, h, 0.34f), i % 2 == 0 ? Crystal : Hex("8BC8CF"),
                    null, new Vector3(i * 5f, -a * Mathf.Rad2Deg, (i - 3) * 4f), false, true);
            }
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 6f;
            light.intensity = 1.2f;
            light.color = Crystal;
            root.AddComponent<StorybookMotion>().mode = StorybookMotion.Mode.Float;
            return root;
        }

        private static float RandomRange(System.Random random, float min, float max)
        {
            return min + (float)random.NextDouble() * (max - min);
        }
    }
}
