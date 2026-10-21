using System.Collections.Generic;
using UnityEngine;
using VN.Multiplayer;
using static VN.World.FairytaleArt;

namespace VN.World
{
    /// <summary>
    /// 把五幅参考画作搭成可走进去的立体场景（放在村庄远处），并在村庄南侧摆一排画框传送门。
    /// 环境（地面、墙、玻璃、天空、光柱、光斑）为程序化；道具通过 AIAssetLibrary 取 AI 模型或占位。
    /// </summary>
    public static class ReferenceSceneBuilder
    {
        private static Texture2D _shaftTex;
        private static Texture2D _glowTex;

        private static void Kill(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        /// <summary>场景里没有画廊时搭建；已存在则跳过（支持在 Scene 视图里继续手工编辑）。</summary>
        public static GameObject Ensure(WorldLevel level)
        {
            if (level == null) return null;
            var existing = level.transform.Find(ReferenceSceneLibrary.GalleryRootName);
            if (existing != null) return existing.gameObject;
            return Build(level);
        }

        public static GameObject Build(WorldLevel level)
        {
            Transform root = level.transform;
            var galleries = Root(ReferenceSceneLibrary.GalleryRootName, root, Vector3.zero);
            var portals = Root(ReferenceSceneLibrary.PortalRowName, root, Vector3.zero);

            Vector3 villageSpawn = level.spawnPoint != null ? level.spawnPoint.position : new Vector3(0f, 0f, -8f);

            // 村庄南侧的画廊长廊：一排木地台 + 五个画框
            Box("GalleryDeck", portals.transform, new Vector3(0f, 0.08f, -25.5f), new Vector3(40f, 0.16f, 4f), Wood, "WoodPlanks", true);
            foreach (var scene in ReferenceSceneLibrary.Scenes)
            {
                var easel = Easel(portals.transform, scene.portalInVillage + Vector3.up * 0.16f, 180f + scene.portalYaw,
                    scene.imageName, scene.imageUv, scene.title,
                    scene.origin + scene.spawnLocal, scene.spawnYaw, scene.lightingId);
                easel.name = "Portal_" + scene.id;
                BuildLantern(portals.transform, scene.portalInVillage + new Vector3(3.2f, 0.16f, -0.6f));
            }

            foreach (var scene in ReferenceSceneLibrary.Scenes)
            {
                var sceneRoot = Root("Gallery_" + scene.id, galleries.transform, scene.origin);
                switch (scene.id)
                {
                    case "greenhouse": BuildGreenhouse(sceneRoot.transform); break;
                    case "seaside": BuildSeaside(sceneRoot.transform); break;
                    case "street": BuildStreet(sceneRoot.transform); break;
                    case "library": BuildLibrary(sceneRoot.transform); break;
                    case "orange": BuildOrangeCourtyard(sceneRoot.transform); break;
                }

                var propsRoot = Root("Props", sceneRoot.transform, Vector3.zero);
                foreach (var placement in scene.props)
                {
                    var go = AIAssetLibrary.Instantiate(placement.propId, propsRoot.transform, placement.position, placement.yaw, placement.scale);
                    if (placement.propId == "upright_piano") MakePianoPlayable(go);
                }

                // 光照区域：覆盖整个场景体积
                var zoneGO = Root("LightingZone", sceneRoot.transform, new Vector3(0f, 10f, 0f));
                var zoneCol = zoneGO.AddComponent<BoxCollider>();
                zoneCol.isTrigger = true;
                zoneCol.size = new Vector3(80f, 40f, 80f);
                var zone = zoneGO.AddComponent<LightingZone>();
                zone.presetId = scene.lightingId;
                zone.Preset = scene.Lighting;

                // 出生点标记 + 回村画框
                var spawn = Root("Spawn", sceneRoot.transform, scene.spawnLocal);
                spawn.transform.localRotation = Quaternion.Euler(0f, scene.spawnYaw, 0f);
                var back = Easel(sceneRoot.transform, scene.spawnLocal + Quaternion.Euler(0f, scene.spawnYaw, 0f) * new Vector3(0f, 0f, -3.2f),
                    scene.spawnYaw + 180f, "UI/SkyPanorama", new Rect(0.3f, 0.2f, 0.4f, 0.6f),
                    GameLanguage.IsEnglish ? "Back to the village" : "回到村庄", villageSpawn, 0f, "village");
                back.name = "Portal_Back";
            }
            return galleries;
        }

        // ---------------- 公共部件 ----------------

        private static GameObject Easel(Transform parent, Vector3 position, float yaw, string image, Rect uv, string title,
            Vector3 target, float targetYaw, string lightingId)
        {
            var root = Root("PaintingEasel", parent, position);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Color frame = Hex("8A6240");
            // 画架腿
            Box("LegL", root.transform, new Vector3(-0.7f, 1.0f, 0.2f), new Vector3(0.08f, 2.1f, 0.08f), frame, "WoodPlanks").transform.localRotation = Quaternion.Euler(6f, 0f, 8f);
            Box("LegR", root.transform, new Vector3(0.7f, 1.0f, 0.2f), new Vector3(0.08f, 2.1f, 0.08f), frame, "WoodPlanks").transform.localRotation = Quaternion.Euler(6f, 0f, -8f);
            Box("LegB", root.transform, new Vector3(0f, 1.0f, 0.55f), new Vector3(0.08f, 2.0f, 0.08f), frame, "WoodPlanks").transform.localRotation = Quaternion.Euler(-22f, 0f, 0f);
            Box("Tray", root.transform, new Vector3(0f, 0.7f, 0.1f), new Vector3(1.5f, 0.06f, 0.2f), frame, "WoodPlanks");
            // 画框：按原图（裁剪后）的长宽比决定尺寸，面积约 3.2 m²
            var tex = Resources.Load<Texture2D>("Art/References/" + image) ?? Resources.Load<Texture2D>("Art/" + image);
            float aspect = tex != null ? (tex.width * uv.width) / Mathf.Max(1f, tex.height * uv.height) : 0.8f;
            aspect = Mathf.Clamp(aspect, 0.55f, 1.9f);
            float h = Mathf.Sqrt(3.2f / aspect), w = h * aspect;
            Box("Frame", root.transform, new Vector3(0f, 0.72f + h * 0.5f, 0.12f), new Vector3(w + 0.16f, h + 0.16f, 0.08f), Hex("D9B06A"));
            var canvas = GameObject.CreatePrimitive(PrimitiveType.Quad);
            canvas.name = "Canvas";
            canvas.transform.SetParent(root.transform, false);
            canvas.transform.localPosition = new Vector3(0f, 0.72f + h * 0.5f, 0.07f);
            canvas.transform.localScale = new Vector3(w, h, 1f);
            Kill(canvas.GetComponent<Collider>());
            var canvasMat = new Material(Shader.Find("Unlit/Texture") ?? Shader.Find("Standard")) { name = "Canvas_" + image };
            if (tex != null)
            {
                canvasMat.mainTexture = tex;
                canvasMat.mainTextureOffset = new Vector2(uv.x, uv.y);
                canvasMat.mainTextureScale = new Vector2(uv.width, uv.height);
            }
            canvas.GetComponent<Renderer>().sharedMaterial = canvasMat;
            // 轻微发光描边，让画框在场景里醒目
            Part(PrimitiveType.Cube, "Glow", root.transform, new Vector3(0f, 0.72f + h * 0.5f, 0.14f), new Vector3(w + 0.3f, h + 0.3f, 0.02f), Cream, null, null, false, true);

            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.72f + h * 0.5f, 0.2f);
            col.size = new Vector3(w + 0.3f, h + 0.9f, 0.9f);
            var portal = root.AddComponent<PaintingPortal>();
            portal.displayName = title;
            portal.destinationTitle = title;
            portal.targetPosition = target;
            portal.targetYaw = targetYaw;
            portal.lightingId = lightingId;
            portal.interactRadius = 3.2f;
            WorldLabel.Attach(root, title, 0.72f + h + 0.5f);
            return root;
        }

        private static void MakePianoPlayable(GameObject piano)
        {
            if (piano == null || piano.GetComponent<PlayableInstrument>() != null) return;
            if (piano.GetComponent<Collider>() == null)
            {
                var col = piano.AddComponent<BoxCollider>();
                col.center = new Vector3(0f, 0.7f, 0f);
                col.size = new Vector3(1.7f, 1.4f, 1.0f);
            }
            var instrument = piano.AddComponent<PlayableInstrument>();
            instrument.instrumentId = 2;
            instrument.timbre = InstrumentAudio.Timbre.Piano;
            instrument.noteCount = 13;
            instrument.displayName = GameLanguage.IsEnglish ? "piano" : "钢琴";
            instrument.interactRadius = 3.5f;
        }

        private static GameObject Floor(Transform parent, string name, Vector3 center, Vector2 size, Color tint, string texture, float tiling)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Plane);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localScale = new Vector3(size.x / 10f, 1f, size.y / 10f);
            var mat = Material(name, tint, texture);
            mat.mainTextureScale = new Vector2(tiling, tiling);
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.isStatic = true;
            return go;
        }

        private static Material GlassMaterial(Color color)
        {
            var shader = Shader.Find("Legacy Shaders/Transparent/Diffuse") ?? Shader.Find("Standard");
            var mat = new Material(shader) { name = "Glass", color = color };
            return mat;
        }

        private static Material AdditiveMaterial(Texture2D tex, Color tint)
        {
            var shader = Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "Additive", mainTexture = tex };
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", tint);
            else mat.color = tint;
            return mat;
        }

        private static Texture2D ShaftTexture()
        {
            if (_shaftTex != null) return _shaftTex;
            var tex = new Texture2D(32, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 128; y++)
            for (int x = 0; x < 32; x++)
            {
                float u = x / 31f, v = y / 127f;
                float across = Mathf.Sin(u * Mathf.PI);
                float along = Mathf.Sin(v * Mathf.PI);
                float a = Mathf.Pow(across, 1.6f) * Mathf.Pow(along, 0.7f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _shaftTex = tex;
            return tex;
        }

        private static Texture2D GlowTexture()
        {
            if (_glowTex != null) return _glowTex;
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 31.5f;
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 1.8f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            _glowTex = tex;
            return tex;
        }

        /// <summary>沿太阳方向的体积光柱。position 为光柱中心。</summary>
        private static GameObject Shaft(Transform parent, Vector3 position, Vector3 sunEuler, float length, float width, Color color, float alpha)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "SunShaft";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            Vector3 dir = Quaternion.Euler(sunEuler) * Vector3.forward;   // 光的行进方向（向下）
            Vector3 side = Vector3.Cross(dir, Vector3.right);
            if (side.sqrMagnitude < 0.01f) side = Vector3.forward;
            go.transform.rotation = Quaternion.LookRotation(side, -dir);
            go.transform.localScale = new Vector3(width, length, 1f);
            Kill(go.GetComponent<Collider>());
            var c = color; c.a = alpha;
            go.GetComponent<Renderer>().sharedMaterial = AdditiveMaterial(ShaftTexture(), c);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<LightShaft>();
            return go;
        }

        /// <summary>地面上的柔光斑。</summary>
        private static GameObject GlowPatch(Transform parent, Vector3 position, float size, Color color, float alpha)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "LightPatch";
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position + Vector3.up * 0.02f;
            go.transform.localRotation = Quaternion.Euler(90f, Random.Range(0f, 180f), 0f);
            go.transform.localScale = new Vector3(size * Random.Range(0.8f, 1.3f), size, 1f);
            Kill(go.GetComponent<Collider>());
            var c = color; c.a = alpha;
            go.GetComponent<Renderer>().sharedMaterial = AdditiveMaterial(GlowTexture(), c);
            go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        private static void SkyBackdrop(Transform parent, float radius, float height, Color lower, Color upper, string texture = "Clouds")
        {
            var root = Root("SkyBackdrop", parent, Vector3.zero);
            int panels = 10;
            float width = 2f * Mathf.PI * radius / panels * 1.08f;
            for (int i = 0; i < panels; i++)
            {
                float a = i / (float)panels * Mathf.PI * 2f;
                Vector3 p = new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                float yaw = -a * Mathf.Rad2Deg + 90f;
                var low = Box("SkyLow", root.transform, p + Vector3.up * height * 0.25f, new Vector3(width, height * 0.5f, 0.2f), lower, texture);
                low.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                var high = Box("SkyHigh", root.transform, p + Vector3.up * height * 0.75f, new Vector3(width, height * 0.5f, 0.2f), upper, texture);
                high.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            }
            // 顶盖
            var cap = Box("SkyCap", root.transform, Vector3.up * height, new Vector3(radius * 2.2f, 0.2f, radius * 2.2f), upper, texture);
            cap.isStatic = true;
        }

        private static void Bounds(Transform parent, float halfX, float halfZ, float height = 6f)
        {
            var root = Root("Bounds", parent, Vector3.zero);
            var walls = new List<KeyValuePair<Vector3, Vector3>>
            {
                new KeyValuePair<Vector3, Vector3>(new Vector3(0f, height * 0.5f, halfZ), new Vector3(halfX * 2f, height, 0.5f)),
                new KeyValuePair<Vector3, Vector3>(new Vector3(0f, height * 0.5f, -halfZ), new Vector3(halfX * 2f, height, 0.5f)),
                new KeyValuePair<Vector3, Vector3>(new Vector3(halfX, height * 0.5f, 0f), new Vector3(0.5f, height, halfZ * 2f)),
                new KeyValuePair<Vector3, Vector3>(new Vector3(-halfX, height * 0.5f, 0f), new Vector3(0.5f, height, halfZ * 2f)),
            };
            foreach (var data in walls)
            {
                var go = new GameObject("Wall");
                go.transform.SetParent(root.transform, false);
                go.transform.localPosition = data.Key;
                var col = go.AddComponent<BoxCollider>();
                col.size = data.Value;
            }
        }

        private static void GlassWall(Transform parent, Vector3 center, float yaw, float width, float height, Color mullion, Color glass)
        {
            var root = Root("GlassWall", parent, center);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            int cols = Mathf.Max(1, Mathf.RoundToInt(width / 1.4f));
            int rows = Mathf.Max(1, Mathf.RoundToInt(height / 1.25f));
            float cw = width / cols, rh = height / rows;
            for (int c = 0; c <= cols; c++)
                Box("Mullion", root.transform, new Vector3(-width * 0.5f + c * cw, height * 0.5f, 0f), new Vector3(0.1f, height, 0.12f), mullion);
            for (int r = 0; r <= rows; r++)
                Box("Rail", root.transform, new Vector3(0f, r * rh, 0f), new Vector3(width, 0.1f, 0.12f), mullion);
            var pane = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pane.name = "GlassPane";
            pane.transform.SetParent(root.transform, false);
            pane.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            pane.transform.localScale = new Vector3(width, height, 0.04f);
            pane.GetComponent<Renderer>().sharedMaterial = GlassMaterial(glass);
            pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private static void FoliageWall(Transform parent, Vector3 center, float yaw, float width, float height, float depth, int seed)
        {
            var root = Root("FoliageWall", parent, center);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var random = new System.Random(seed);
            Color[] leaves = { Sage, LeafLight, Hex("3F6B3A"), Hex("B7CF7A") };
            int count = Mathf.RoundToInt(width * height * 0.9f);
            for (int i = 0; i < count; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -width * 0.5f, width * 0.5f), RandomRange(random, 0.3f, height), RandomRange(random, 0f, depth));
                float s = RandomRange(random, 0.9f, 1.8f);
                Part(PrimitiveType.Sphere, "Leaves", root.transform, p, new Vector3(s, s * 0.8f, s), leaves[random.Next(leaves.Length)], "Leaves");
            }
        }

        // ---------------- 1. 温室 ----------------

        private static void BuildGreenhouse(Transform root)
        {
            Color mullion = Hex("F4F1E8");
            Color glass = new Color(0.92f, 0.98f, 0.86f, 0.22f);
            Vector3 sun = LightingPreset.Greenhouse().sunEuler;

            Floor(root, "GreenhouseFloor", Vector3.zero, new Vector2(16f, 16f), Hex("B9AE98"), "WoodPlanks", 6f);
            // 三面玻璃墙，玩家进入侧敞开
            GlassWall(root, new Vector3(0f, 0f, 6f), 0f, 14f, 5f, mullion, glass);
            GlassWall(root, new Vector3(7f, 0f, 0f), 90f, 12f, 5f, mullion, glass);
            GlassWall(root, new Vector3(-7f, 0f, 2.5f), 90f, 7f, 5f, mullion, glass);
            // 左侧白纱帘
            Box("Curtain", root, new Vector3(-6.7f, 2.6f, -3.6f), new Vector3(0.35f, 5.2f, 5.2f), Hex("F6F2E8"), "Parchment");
            // 玻璃屋顶骨架
            for (int i = -5; i <= 5; i++)
                Box("RoofBeam", root, new Vector3(i * 1.4f, 5.05f, 0f), new Vector3(0.1f, 0.1f, 12.4f), mullion);
            for (int i = -4; i <= 4; i++)
                Box("RoofBeam", root, new Vector3(0f, 5.05f, i * 1.5f), new Vector3(14.2f, 0.1f, 0.1f), mullion);
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "RoofGlass";
            roof.transform.SetParent(root, false);
            roof.transform.localPosition = new Vector3(0f, 5.1f, 0f);
            roof.transform.localScale = new Vector3(14.2f, 0.04f, 12.4f);
            roof.GetComponent<Renderer>().sharedMaterial = GlassMaterial(new Color(0.95f, 1f, 0.9f, 0.14f));
            roof.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Kill(roof.GetComponent<Collider>());

            // 玻璃外茂密的植物墙与逆光亮面
            FoliageWall(root, new Vector3(0f, 0f, 6.6f), 0f, 18f, 6f, 3f, 11);
            FoliageWall(root, new Vector3(7.6f, 0f, 0f), 90f, 14f, 6f, 3f, 12);
            FoliageWall(root, new Vector3(-7.6f, 0f, 3f), 90f, 8f, 5f, 2.5f, 13);
            Part(PrimitiveType.Cube, "Backlight", root, new Vector3(0f, 4f, 11f), new Vector3(30f, 10f, 0.2f), Hex("FFF6D2"), null, null, false, true);
            Part(PrimitiveType.Cube, "Backlight", root, new Vector3(12f, 4f, 0f), new Vector3(0.2f, 10f, 30f), Hex("FFF3C8"), null, null, false, true);
            SkyBackdrop(root, 40f, 24f, Hex("F2F3DE"), Hex("E6EED8"));

            // 室内悬垂藤蔓
            var random = new System.Random(21);
            for (int i = 0; i < 14; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -6.5f, 6.5f), 5f, RandomRange(random, -2f, 5.5f));
                float len = RandomRange(random, 0.8f, 2.2f);
                Part(PrimitiveType.Capsule, "Vine", root, p - Vector3.up * len * 0.5f, new Vector3(0.08f, len * 0.5f, 0.08f), Hex("5E8A4E"));
                Part(PrimitiveType.Sphere, "VineLeaf", root, p - Vector3.up * len, new Vector3(0.35f, 0.25f, 0.35f), LeafLight, "Leaves");
            }
            // 窗边的叶影植物
            for (int i = 0; i < 6; i++)
                Part(PrimitiveType.Sphere, "Fern", root, new Vector3(-6.2f + i * 2.4f, 0.5f, 5.3f), new Vector3(1.2f, 0.9f, 0.8f), i % 2 == 0 ? Sage : Hex("3F6B3A"), "Leaves");

            // 体积光与地面光斑
            Color shaftColor = Hex("FFF1C2");
            for (int i = 0; i < 7; i++)
                Shaft(root, new Vector3(-3f + i * 1.2f, 3.2f, 1.5f - i * 0.4f), sun, 9f, 0.9f + (i % 3) * 0.4f, shaftColor, 0.22f);
            GlowPatch(root, new Vector3(-0.5f, 0f, -1.5f), 7f, Hex("FFF4CC"), 0.35f);
            GlowPatch(root, new Vector3(2.5f, 0f, -4f), 4.5f, Hex("FFF4CC"), 0.25f);
            GlowPatch(root, new Vector3(-3f, 0f, 1f), 3.5f, Hex("E8F2B8"), 0.2f);
            Bounds(root, 7.3f, 7.5f);
        }

        // ---------------- 2. 海边 ----------------

        private static void BuildSeaside(Transform root)
        {
            Vector3 sun = LightingPreset.Seaside().sunEuler;
            Floor(root, "Meadow", Vector3.zero, new Vector2(44f, 20f), Hex("8FB06A"), "Grass", 8f);
            Box("CliffEdge", root, new Vector3(0f, -1.0f, 10.2f), new Vector3(44f, 2.2f, 0.6f), Hex("A9B59A"), "Grass", true);
            // 岸边的浅滩与白浪
            Floor(root, "Shallows", new Vector3(0f, -1.22f, 12.5f), new Vector2(60f, 5f), Hex("E6EEF2"), "Sand", 6f);

            // 海面：大平面 + 滚动水纹，近岸再铺一层浪花
            var sea = Floor(root, "Sea", new Vector3(0f, -1.3f, 130f), new Vector2(420f, 240f), Hex("C3D6E6"), "Water", 60f);
            sea.AddComponent<UVScroll>().speed = new Vector2(0.015f, 0.006f);
            Kill(sea.GetComponent<Collider>());
            var nearSea = Floor(root, "SeaNear", new Vector3(0f, -1.28f, 34f), new Vector2(120f, 48f), Hex("A3BFD8"), "Water", 24f);
            nearSea.AddComponent<UVScroll>().speed = new Vector2(0.02f, 0.012f);
            Kill(nearSea.GetComponent<Collider>());
            var foam = Floor(root, "Foam", new Vector3(0f, -1.25f, 16f), new Vector2(60f, 12f), Hex("DCE9F0"), "Water", 10f);
            foam.AddComponent<UVScroll>().speed = new Vector2(-0.02f, 0.01f);
            Kill(foam.GetComponent<Collider>());

            // 太阳在海面的反光带
            var glitter = GlowPatch(root, new Vector3(6f, -1.2f, 70f), 60f, Hex("FFFCEE"), 0.55f);
            glitter.transform.localScale = new Vector3(26f, 70f, 1f);
            GlowPatch(root, new Vector3(4f, -1.2f, 30f), 24f, Hex("FFFFFF"), 0.3f);

            // 远山与天空
            Part(PrimitiveType.Sphere, "FarHills", root, new Vector3(-40f, -6f, 230f), new Vector3(220f, 24f, 60f), Hex("8FA6BD"));
            Part(PrimitiveType.Sphere, "FarHills", root, new Vector3(90f, -8f, 240f), new Vector3(180f, 20f, 60f), Hex("9BB0C4"));
            SkyBackdrop(root, 260f, 120f, Hex("DCE9F4"), Hex("A9C9EA"));
            // 云
            var random = new System.Random(5);
            for (int i = 0; i < 12; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -120f, 120f), RandomRange(random, 30f, 70f), RandomRange(random, 120f, 220f));
                float s = RandomRange(random, 14f, 34f);
                Part(PrimitiveType.Sphere, "Cloud", root, p, new Vector3(s, s * 0.35f, s * 0.6f), Hex("F7FAFD"));
            }

            // 海鸥群
            for (int i = 0; i < 16; i++)
            {
                var gull = ReferenceProps.Seagull(root, Vector3.zero);
                var flight = gull.AddComponent<CircleFlight>();
                flight.center = root.position + new Vector3(RandomRange(random, -14f, 14f), 0f, RandomRange(random, 8f, 40f));
                flight.radius = RandomRange(random, 6f, 18f);
                flight.height = RandomRange(random, 4f, 14f);
                flight.speed = RandomRange(random, 0.25f, 0.55f) * (i % 2 == 0 ? 1f : -1f);
                flight.phase = RandomRange(random, 0f, 6.28f);
                gull.transform.localScale = Vector3.one * RandomRange(random, 0.8f, 1.3f);
            }

            // 阴天海边不要明显光柱，只留一道很淡的天光
            Shaft(root, new Vector3(2f, 9f, 12f), sun, 20f, 7f, Hex("FFFFFF"), 0.03f);
            Bounds(root, 21f, 10f);
        }

        // ---------------- 3. 林荫街道 ----------------

        private static void BuildStreet(Transform root)
        {
            Vector3 sun = LightingPreset.ShadedStreet().sunEuler;
            Floor(root, "StreetGround", Vector3.zero, new Vector2(40f, 40f), Hex("98A27A"), "Dirt", 10f);
            // 弯曲的砖路
            for (int i = 0; i < 7; i++)
            {
                float z = -15f + i * 5f;
                float x = Mathf.Sin(i * 0.6f) * 1.6f;
                var seg = Box("BrickRoad", root, new Vector3(x, 0.03f, z), new Vector3(6.5f, 0.06f, 5.4f), Hex("B9A58E"), "Cobblestone");
                seg.transform.localRotation = Quaternion.Euler(0f, Mathf.Cos(i * 0.6f) * 9f, 0f);
                seg.GetComponent<Renderer>().sharedMaterial.mainTextureScale = new Vector2(3f, 3f);
            }
            // 左侧低矮石墙与灌木
            Box("StoneWall", root, new Vector3(-9f, 0.5f, 2f), new Vector3(0.6f, 1.0f, 26f), Hex("B7B2A6"), "LavenderStone", true);
            var random = new System.Random(33);
            for (int i = 0; i < 10; i++)
                Part(PrimitiveType.Sphere, "Hedge", root, new Vector3(-8.2f, 0.55f, -11f + i * 2.5f), new Vector3(1.6f, 1.1f, 1.6f), i % 2 == 0 ? Sage : LeafLight, "Leaves");
            // 右侧围栏与草坪
            BuildFence(root, new Vector3(8.5f, 0f, -4f), 9, 90f);
            for (int i = 0; i < 8; i++)
                BuildFlowers(root, new Vector3(RandomRange(random, 6f, 10f), 0f, RandomRange(random, -12f, 2f)), 200 + i);
            // 远处的树墙与天空
            FoliageWall(root, new Vector3(0f, 0f, 19f), 0f, 44f, 12f, 4f, 41);
            FoliageWall(root, new Vector3(-19f, 0f, 0f), 90f, 44f, 12f, 4f, 42);
            FoliageWall(root, new Vector3(19f, 0f, 0f), 90f, 44f, 12f, 4f, 43);
            FoliageWall(root, new Vector3(0f, 0f, -19f), 0f, 44f, 10f, 4f, 44);
            SkyBackdrop(root, 60f, 36f, Hex("EEF2DC"), Hex("DCE8C6"));

            // 树冠漏下的光柱和斑驳光斑
            for (int i = 0; i < 9; i++)
                Shaft(root, new Vector3(RandomRange(random, -7f, 7f), 5.5f, RandomRange(random, -8f, 8f)), sun, 11f, RandomRange(random, 0.6f, 1.6f), Hex("FFF3C4"), 0.12f);
            for (int i = 0; i < 30; i++)
                GlowPatch(root, new Vector3(RandomRange(random, -8f, 8f), 0.05f, RandomRange(random, -14f, 12f)), RandomRange(random, 0.8f, 2.6f), Hex("FFF5CF"), 0.3f);
            GlowPatch(root, new Vector3(-1f, 0.05f, -4f), 9f, Hex("FFF8DC"), 0.35f);
            Bounds(root, 18f, 17f);
        }

        // ---------------- 4. 图书馆 ----------------

        private static void BuildLibrary(Transform root)
        {
            Vector3 sun = LightingPreset.Library().sunEuler;
            Color wall = Hex("5A4A3E");
            Floor(root, "LibraryFloor", Vector3.zero, new Vector2(18.6f, 15f), Hex("4E3A2E"), "WoodPlanks", 7f);
            Box("Rug", root, new Vector3(0f, 0.015f, 3f), new Vector3(7f, 0.03f, 5f), Hex("6E3B3B"), "Parchment");
            Box("WallL", root, new Vector3(-9.3f, 2.6f, 0f), new Vector3(0.3f, 5.2f, 15f), wall, "WoodPlanks", true);
            Box("WallR", root, new Vector3(9.3f, 2.6f, 0f), new Vector3(0.3f, 5.2f, 15f), wall, "WoodPlanks", true);
            Box("Ceiling", root, new Vector3(0f, 5.2f, 0f), new Vector3(18.6f, 0.3f, 15f), Hex("E9E2D2"), "CreamPlaster");
            // 前墙带门洞（回村画框在门洞前）
            Box("WallF_L", root, new Vector3(-5.5f, 2.6f, -7.5f), new Vector3(7.6f, 5.2f, 0.3f), wall, "WoodPlanks", true);
            Box("WallF_R", root, new Vector3(5.5f, 2.6f, -7.5f), new Vector3(7.6f, 5.2f, 0.3f), wall, "WoodPlanks", true);
            Box("WallF_Top", root, new Vector3(0f, 4.4f, -7.5f), new Vector3(3.6f, 1.6f, 0.3f), wall, "WoodPlanks");
            // 后墙：巨大窗洞
            Box("WallB_L", root, new Vector3(-8.3f, 2.6f, 7.5f), new Vector3(2.0f, 5.2f, 0.3f), wall, "WoodPlanks", true);
            Box("WallB_R", root, new Vector3(8.3f, 2.6f, 7.5f), new Vector3(2.0f, 5.2f, 0.3f), wall, "WoodPlanks", true);
            Box("WallB_Top", root, new Vector3(0f, 4.95f, 7.5f), new Vector3(14.8f, 0.5f, 0.3f), wall, "WoodPlanks");
            Box("WallB_Bottom", root, new Vector3(0f, 0.35f, 7.5f), new Vector3(14.8f, 0.7f, 0.3f), wall, "WoodPlanks", true);
            Box("WindowSill", root, new Vector3(0f, 0.72f, 7.5f), new Vector3(15f, 0.08f, 0.6f), Hex("EFE7D6"));
            Color white = Hex("F3EEE2");
            for (int i = -3; i <= 3; i++)
                Box("WindowPost", root, new Vector3(i * 2.4f, 2.7f, 7.5f), new Vector3(0.12f, 4.0f, 0.2f), white);
            Box("WindowRail", root, new Vector3(0f, 3.4f, 7.5f), new Vector3(14.8f, 0.1f, 0.2f), white);
            // 纱帘（半透明）
            foreach (float cx in new[] { -6.0f, 6.0f })
            {
                var sheer = GameObject.CreatePrimitive(PrimitiveType.Cube);
                sheer.name = "SheerCurtain";
                sheer.transform.SetParent(root, false);
                sheer.transform.localPosition = new Vector3(cx, 2.75f, 7.2f);
                sheer.transform.localScale = new Vector3(2.6f, 4.3f, 0.1f);
                sheer.GetComponent<Renderer>().sharedMaterial = GlassMaterial(new Color(1f, 0.98f, 0.92f, 0.55f));
                Kill(sheer.GetComponent<Collider>());
            }
            Box("Valance", root, new Vector3(0f, 4.55f, 7.1f), new Vector3(15f, 0.5f, 0.2f), Hex("F6F1E6"), "Parchment");
            // 窗外：露台、草原、远山、云、天空
            Floor(root, "Terrace", new Vector3(0f, 0f, 9.5f), new Vector2(20f, 4f), Hex("D8D1C0"), "LavenderStone", 4f);
            Floor(root, "Meadow", new Vector3(0f, -0.3f, 60f), new Vector2(240f, 110f), Hex("8FB36A"), "Grass", 24f);
            var random = new System.Random(77);
            for (int i = 0; i < 9; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -80f, 80f), -3f, RandomRange(random, 30f, 90f));
                float s = RandomRange(random, 24f, 60f);
                Part(PrimitiveType.Sphere, "Hill", root, p, new Vector3(s, s * 0.18f, s * 0.7f), i % 2 == 0 ? Hex("86AC63") : Hex("9BBE74"), "Grass");
            }
            for (int i = 0; i < 6; i++)
                Part(PrimitiveType.Sphere, "Mountain", root, new Vector3(-100f + i * 42f, -8f, 150f), new Vector3(90f, 34f, 50f), Hex("7E93A8"));
            for (int i = 0; i < 10; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -90f, 90f), RandomRange(random, 26f, 48f), RandomRange(random, 70f, 140f));
                float s = RandomRange(random, 14f, 40f);
                Part(PrimitiveType.Sphere, "Cloud", root, p, new Vector3(s, s * 0.4f, s * 0.55f), Hex("FAFCFF"));
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -50f, 50f), -0.3f, RandomRange(random, 24f, 60f));
                BuildTree(root, p, RandomRange(random, 4f, 7f), 300 + i);
            }
            Part(PrimitiveType.Cube, "SkyGlow", root, new Vector3(0f, 20f, 180f), new Vector3(400f, 60f, 0.5f), Hex("DCEBF8"), null, null, false, true);
            SkyBackdrop(root, 220f, 110f, Hex("E8F1FA"), Hex("8FB6E2"));

            // 从窗进来的光柱与地上的亮带
            for (int i = 0; i < 4; i++)
                Shaft(root, new Vector3(-4.5f + i * 3f, 2.6f, 4.5f), sun, 8f, 2.6f, Hex("FFF7E6"), 0.07f);
            GlowPatch(root, new Vector3(0f, 0.02f, 4.5f), 9f, Hex("FFF6E2"), 0.25f);
            Bounds(root, 9f, 7.3f);
        }

        // ---------------- 5. 橘子树院子 ----------------

        private static void BuildOrangeCourtyard(Transform root)
        {
            Vector3 sun = LightingPreset.OrangeCourtyard().sunEuler;
            Floor(root, "CourtyardGround", Vector3.zero, new Vector2(26f, 24f), Hex("E3E0D6"), "CreamPlaster", 5f);
            Color wall = Hex("B9B6AD");
            Box("WallBack", root, new Vector3(0f, 1.7f, 10.2f), new Vector3(26f, 3.4f, 0.5f), wall, "CreamPlaster", true);
            Box("WallRight", root, new Vector3(12.8f, 1.4f, 0f), new Vector3(0.5f, 2.8f, 24f), wall, "CreamPlaster", true);
            Box("WallLeft", root, new Vector3(-12.8f, 1.4f, 0f), new Vector3(0.5f, 2.8f, 24f), wall, "CreamPlaster", true);
            Box("WallFront", root, new Vector3(0f, 1.0f, -11.8f), new Vector3(26f, 2.0f, 0.5f), wall, "CreamPlaster", true);
            // 墙上的爬藤
            var random = new System.Random(88);
            for (int i = 0; i < 9; i++)
                Part(PrimitiveType.Sphere, "WallVine", root, new Vector3(RandomRange(random, -12f, 12f), RandomRange(random, 2.2f, 3.4f), 9.9f), new Vector3(RandomRange(random, 1.4f, 3f), 1.2f, 0.8f), i % 2 == 0 ? Sage : Hex("3F6B3A"), "Leaves");
            // 墙外更多树冠，形成环绕的绿色
            FoliageWall(root, new Vector3(0f, 2f, 12f), 0f, 34f, 9f, 5f, 61);
            FoliageWall(root, new Vector3(14f, 2f, 0f), 90f, 28f, 8f, 5f, 62);
            FoliageWall(root, new Vector3(-14f, 2f, 0f), 90f, 28f, 8f, 5f, 63);
            SkyBackdrop(root, 50f, 30f, Hex("F3F2E6"), Hex("E2E9D4"));

            // 掉落的橘子与飘落的白花瓣
            for (int i = 0; i < 16; i++)
                Part(PrimitiveType.Sphere, "FallenOrange", root, new Vector3(RandomRange(random, -9f, 9f), 0.13f, RandomRange(random, -8f, 8f)), Vector3.one * 0.26f, Hex("F0922F"));
            for (int i = 0; i < 40; i++)
                Part(PrimitiveType.Cube, "Petal", root, new Vector3(RandomRange(random, -11f, 11f), RandomRange(random, 0.02f, 3.5f), RandomRange(random, -10f, 9f)),
                    new Vector3(0.07f, 0.01f, 0.05f), Hex("FBFAF4"), null, new Vector3(RandomRange(random, -40f, 40f), RandomRange(random, 0f, 180f), RandomRange(random, -40f, 40f)));

            // 正午顶光：强光斑与穿过橘子树的光柱
            GlowPatch(root, new Vector3(0f, 0.03f, -3f), 16f, Hex("FFFCEF"), 0.5f);
            for (int i = 0; i < 34; i++)
                GlowPatch(root, new Vector3(RandomRange(random, -10f, 10f), 0.04f, RandomRange(random, -9f, 8f)), RandomRange(random, 0.7f, 2.2f), Hex("FFF8DC"), 0.35f);
            for (int i = 0; i < 8; i++)
                Shaft(root, new Vector3(RandomRange(random, -7f, 7f), 4.5f, RandomRange(random, -4f, 7f)), sun, 9f, RandomRange(random, 0.7f, 1.6f), Hex("FFF6D0"), 0.16f);
            Bounds(root, 12.5f, 11.5f);
        }
    }
}
