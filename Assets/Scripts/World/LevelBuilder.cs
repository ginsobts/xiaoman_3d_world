using System.Collections.Generic;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 用基础几何体搭「原型关卡」（白盒），以及玩家的占位模型。
    /// 运行时和编辑器菜单（3D世界/…）都会调用这里，方便把白盒存成场景再手动替换美术资源。
    /// 所有材质派生自 Resources/World/WorldBase.mat，确保打包后 Shader 不会被裁掉。
    /// </summary>
    public static class LevelBuilder
    {
        private static readonly Dictionary<Color, Material> _materials = new Dictionary<Color, Material>();
        private static Material _baseMaterial;

        public static Material Mat(Color color)
        {
            if (_materials.TryGetValue(color, out var cached) && cached != null) return cached;

            if (_baseMaterial == null)
            {
                var shader = Shader.Find("Xiaoman/Fairytale Pencil")
                    ?? Shader.Find("Standard")
                    ?? Shader.Find("Legacy Shaders/Diffuse");
                _baseMaterial = new Material(shader);
            }

            string matName = "World_" + ColorUtility.ToHtmlStringRGB(color);
            var m = new Material(_baseMaterial) { color = color, name = matName };

#if UNITY_EDITOR
            // 编辑器下（非运行）生成的关卡要能存进场景：材质必须是资源文件，否则保存后引用会丢。
            if (!Application.isPlaying)
            {
                const string dir = "Assets/Materials/World";
                string path = dir + "/" + matName + ".mat";
                var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                if (existing != null)
                {
                    _materials[color] = existing;
                    return existing;
                }
                if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Materials"))
                    UnityEditor.AssetDatabase.CreateFolder("Assets", "Materials");
                if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                    UnityEditor.AssetDatabase.CreateFolder("Assets/Materials", "World");
                UnityEditor.AssetDatabase.CreateAsset(m, path);
            }
#endif

            _materials[color] = m;
            return m;
        }

        // ---------------- 基础几何体 ----------------

        public static GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Mat(color);
            return go;
        }

        /// <summary>立方体，position 为底面中心。</summary>
        public static GameObject Box(string name, Transform parent, Vector3 footPosition, Vector3 size, Color color)
        {
            return Primitive(PrimitiveType.Cube, name, parent,
                footPosition + Vector3.up * size.y * 0.5f, size, color);
        }

        public static GameObject Tree(Transform parent, Vector3 footPosition, float height = 3.5f)
        {
            int seed = Mathf.RoundToInt(footPosition.x * 37f + footPosition.z * 71f + height * 13f);
            return FairytaleArt.BuildTree(parent, footPosition, height, seed);
        }

        // ---------------- 玩家 ----------------

        /// <summary>创建玩家：CharacterController + 彩铅童话探险者模型。</summary>
        public static GameObject BuildPlayer(Vector3 position, float yaw)
        {
            var go = new GameObject("Player");
            go.tag = "Player";
            go.transform.position = position;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.4f;

            FairytaleArt.BuildExplorer(go.transform);

            go.AddComponent<PlayerController>();
            go.AddComponent<InteractionDetector>();
            return go;
        }

        // ---------------- 原型关卡 ----------------

        /// <summary>
        /// 在 level 根节点下搭一个演示关卡：地面、围墙、树、NPC、木牌、钥匙、锁着的门、门后的水晶。
        /// 返回出生点 Transform。
        /// </summary>
        public static Transform BuildPrototype(WorldLevel level)
        {
            Transform root = level.transform;
            string story = GameConfig.WorldStoryFile;

            // 光
            if (Object.FindObjectOfType<Light>() == null)
            {
                var lightGO = new GameObject("Directional Light");
                lightGO.transform.SetParent(root, false);
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                var light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.color = new Color(1f, 0.96f, 0.88f);
                light.shadows = LightShadows.Soft;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.95f);
            RenderSettings.ambientEquatorColor = new Color(0.86f, 0.82f, 0.72f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.5f, 0.38f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.77f, 0.84f, 0.88f);
            RenderSettings.fogStartDistance = 42f;
            RenderSettings.fogEndDistance = 78f;
            FairytaleArt.BuildBackdrop(root);

            // 地面 60x60
            var ground = Primitive(PrimitiveType.Plane, "Ground", root,
                Vector3.zero, new Vector3(6f, 1f, 6f), new Color(0.56f, 0.66f, 0.5f));
            ground.GetComponent<Renderer>().sharedMaterial =
                FairytaleArt.Material("GrassGround", Color.white, "Grass");
            ground.GetComponent<Renderer>().sharedMaterial.mainTextureScale = new Vector2(12f, 12f);
            ground.isStatic = true;

            // 外围围墙
            Color wallColor = new Color(0.76f, 0.73f, 0.68f);
            Box("Wall_N", root, new Vector3(0f, 0f, 30f), new Vector3(60f, 3f, 1f), wallColor);
            Box("Wall_S", root, new Vector3(0f, 0f, -30f), new Vector3(60f, 3f, 1f), wallColor);
            Box("Wall_E", root, new Vector3(30f, 0f, 0f), new Vector3(1f, 3f, 60f), wallColor);
            Box("Wall_W", root, new Vector3(-30f, 0f, 0f), new Vector3(1f, 3f, 60f), wallColor);

            // 蜿蜒的绘本道路（几段略微旋转的扁平方块）
            for (int i = 0; i < 9; i++)
            {
                float z = -24f + i * 6f;
                float x = Mathf.Sin(i * 0.72f) * 2.2f;
                var path = Box("CrayonPath", root, new Vector3(x, 0.025f, z),
                    new Vector3(5.2f, 0.04f, 7.1f), Color.white);
                path.transform.localRotation = Quaternion.Euler(0, Mathf.Cos(i * 0.72f) * 12f, 0);
                path.GetComponent<Renderer>().sharedMaterial =
                    FairytaleArt.Material("DirtPath", Color.white, "Dirt");
            }

            // 出生点
            var spawn = new GameObject("SpawnPoint");
            spawn.transform.SetParent(root, false);
            spawn.transform.localPosition = new Vector3(0f, 0f, -8f);
            spawn.transform.localRotation = Quaternion.identity;
            level.spawnPoint = spawn.transform;

            // 开场旁白触发区
            var introZone = new GameObject("IntroTrigger");
            introZone.transform.SetParent(root, false);
            introZone.transform.localPosition = new Vector3(0f, 1.5f, -8f);
            var introCol = introZone.AddComponent<BoxCollider>();
            introCol.isTrigger = true;
            introCol.size = new Vector3(8f, 3f, 8f);
            var introTrig = introZone.AddComponent<DialogueTrigger>();
            introTrig.triggerId = "intro";
            introTrig.storyFile = story;
            introTrig.startNode = "intro_1";

            // 树
            foreach (var p in new[]
                     {
                         new Vector3(-10f, 0f, -4f), new Vector3(-14f, 0f, 6f), new Vector3(12f, 0f, 8f),
                         new Vector3(18f, 0f, -2f), new Vector3(-20f, 0f, -16f), new Vector3(22f, 0f, -20f),
                         new Vector3(-8f, 0f, 20f), new Vector3(9f, 0f, 22f),
                     })
                Tree(root, p);

            // 远景童话建筑与小品：让白盒第一眼就有完整的世界气氛。
            FairytaleArt.BuildCottage(root, new Vector3(-20f, 0f, 15f), 18f);
            FairytaleArt.BuildCottage(root, new Vector3(20f, 0f, 13f), -22f);
            FairytaleArt.BuildWell(root, new Vector3(-7f, 0f, 7f));
            FairytaleArt.BuildBench(root, new Vector3(8f, 0f, -5f), -25f);
            FairytaleArt.BuildFence(root, new Vector3(-19f, 0f, 8f), 7, 10f);
            FairytaleArt.BuildFence(root, new Vector3(19f, 0f, 6f), 6, -12f);
            FairytaleArt.BuildLantern(root, new Vector3(-3f, 0f, -10f));
            FairytaleArt.BuildLantern(root, new Vector3(5f, 0f, 10f));
            for (int i = 0; i < 9; i++)
            {
                float a = i / 9f * Mathf.PI * 2f;
                FairytaleArt.BuildFlowers(root,
                    new Vector3(Mathf.Cos(a) * (11f + i % 3), 0f, Mathf.Sin(a) * (10f + i % 2)), 100 + i);
            }
            FairytaleArt.BuildMushrooms(root, new Vector3(-12f, 0f, -7f), 41);
            FairytaleArt.BuildMushrooms(root, new Vector3(11f, 0f, 4f), 89);

            // 跳台
            Color boxColor = new Color(0.8f, 0.62f, 0.45f);
            Box("Crate_A", root, new Vector3(-6f, 0f, -14f), new Vector3(2f, 0.6f, 2f), boxColor);
            Box("Crate_B", root, new Vector3(-8f, 0f, -14f), new Vector3(2f, 1.2f, 2f), boxColor);
            Box("Crate_C", root, new Vector3(-10f, 0f, -14f), new Vector3(2f, 1.8f, 2f), boxColor);

            // NPC：小满（独立碰撞根 + 手绘低模外观）
            var npc = new GameObject("NPC_Xiaoman");
            npc.transform.SetParent(root, false);
            npc.transform.localPosition = new Vector3(4f, 0f, 2f);
            npc.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
            var npcCollider = npc.AddComponent<CapsuleCollider>();
            npcCollider.height = 2f;
            npcCollider.radius = 0.42f;
            npcCollider.center = new Vector3(0f, 1f, 0f);
            FairytaleArt.BuildXiaoman(npc.transform);
            var npcTalk = npc.AddComponent<DialogueInteractable>();
            npcTalk.displayName = GameLanguage.IsEnglish ? "Xiaoman" : "小满";
            npcTalk.storyFile = story;
            npcTalk.startNode = "xiaoman_meet";
            npcTalk.isCharacter = true;
            WorldLabel.Attach(npc, npcTalk.displayName, 2.25f);

            // 木牌
            var signRoot = new GameObject("Sign");
            signRoot.transform.SetParent(root, false);
            signRoot.transform.localPosition = new Vector3(-4f, 0f, 0f);
            Box("Post", signRoot.transform, Vector3.zero, new Vector3(0.15f, 1.2f, 0.15f), new Color(0.5f, 0.36f, 0.22f));
            Box("Board", signRoot.transform, new Vector3(0f, 1.1f, 0f), new Vector3(1.4f, 0.8f, 0.1f), new Color(0.7f, 0.52f, 0.32f));
            var sign = signRoot.AddComponent<DialogueInteractable>();
            sign.displayName = GameLanguage.IsEnglish ? "Sign" : "木牌";
            sign.storyFile = story;
            sign.startNode = "sign_1";
            sign.isCharacter = false;
            sign.facePlayer = false;

            // 草丛 + 钥匙
            Color grass = new Color(0.36f, 0.62f, 0.3f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                var bush = Primitive(PrimitiveType.Sphere, "CrayonBush", root,
                    new Vector3(14f + Mathf.Cos(a) * 1.6f, 0.45f, -14f + Mathf.Sin(a) * 1.6f),
                    new Vector3(1.3f, 0.7f, 1.1f), grass);
                bush.GetComponent<Renderer>().sharedMaterial =
                    FairytaleArt.Material("BushLeaves", FairytaleArt.LeafLight, "Leaves");
                bush.isStatic = true;
            }
            var key = FairytaleArt.BuildKey(root, new Vector3(14f, 0.9f, -14f));
            key.name = "Pickup_Key";
            var keyCollider = key.AddComponent<SphereCollider>();
            keyCollider.radius = 0.65f;
            keyCollider.isTrigger = true;
            var pickup = key.AddComponent<PickupInteractable>();
            pickup.itemId = "key";
            pickup.displayName = GameLanguage.IsEnglish ? "Key" : "钥匙";

            // 北面内墙 + 门
            Color innerWall = new Color(0.62f, 0.6f, 0.66f);
            Box("InnerWall_L", root, new Vector3(-7f, 0f, 14f), new Vector3(10f, 3.2f, 0.6f), innerWall);
            Box("InnerWall_R", root, new Vector3(7f, 0f, 14f), new Vector3(10f, 3.2f, 0.6f), innerWall);
            Box("InnerWall_Top", root, new Vector3(0f, 2.6f, 14f), new Vector3(4f, 0.6f, 0.6f), innerWall);

            var hinge = new GameObject("Door");
            hinge.transform.SetParent(root, false);
            hinge.transform.localPosition = new Vector3(-1f, 0f, 14f);       // 铰链在门的左侧
            var panel = Box("Panel", hinge.transform, new Vector3(1f, 0f, 0f), new Vector3(2f, 2.6f, 0.25f), new Color(0.4f, 0.26f, 0.16f));
            panel.GetComponent<Renderer>().sharedMaterial =
                FairytaleArt.Material("StoryDoor", Color.white, "WoodPlanks");
            Primitive(PrimitiveType.Sphere, "Knob", panel.transform,
                new Vector3(0.38f, 0f, -0.6f), new Vector3(0.1f, 0.08f, 0.6f), new Color(0.9f, 0.8f, 0.3f));
            var door = hinge.AddComponent<DoorInteractable>();
            door.doorId = "north";
            door.displayName = GameLanguage.IsEnglish ? "Door" : "门";
            door.requiredVariable = "has_key";
            door.storyFile = story;
            door.lockedDialogueNode = "door_locked";
            door.openAngle = -105f;
            door.interactRadius = 3f;

            // 门后的小房间 + 水晶
            Primitive(PrimitiveType.Cube, "RoomFloor", root,
                new Vector3(0f, 0.02f, 20f), new Vector3(12f, 0.05f, 12f), new Color(0.45f, 0.42f, 0.55f)).isStatic = true;
            Box("RoomWall_E", root, new Vector3(6f, 0f, 20f), new Vector3(0.6f, 3.2f, 12f), innerWall);
            Box("RoomWall_W", root, new Vector3(-6f, 0f, 20f), new Vector3(0.6f, 3.2f, 12f), innerWall);
            Box("RoomWall_N", root, new Vector3(0f, 0f, 26f), new Vector3(12.6f, 3.2f, 0.6f), innerWall);

            var crystal = FairytaleArt.BuildCrystal(root, new Vector3(0f, 0.55f, 21f));
            crystal.name = "Crystal";
            var crystalCollider = crystal.AddComponent<SphereCollider>();
            crystalCollider.radius = 1.25f;
            Primitive(PrimitiveType.Cylinder, "Pedestal", root,
                new Vector3(0f, 0.35f, 21f), new Vector3(1f, 0.35f, 1f), new Color(0.3f, 0.3f, 0.36f));
            var crystalTalk = crystal.AddComponent<DialogueInteractable>();
            crystalTalk.displayName = GameLanguage.IsEnglish ? "Crystal" : "水晶";
            crystalTalk.storyFile = story;
            crystalTalk.startNode = "crystal_1";
            crystalTalk.isCharacter = false;
            crystalTalk.facePlayer = false;

            return spawn.transform;
        }
    }
}
