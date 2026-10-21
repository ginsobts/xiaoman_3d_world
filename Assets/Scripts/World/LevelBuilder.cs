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
                _baseMaterial = Resources.Load<Material>("World/WorldBase");
                if (_baseMaterial == null)
                {
                    var shader = Shader.Find("Standard") ?? Shader.Find("Legacy Shaders/Diffuse");
                    _baseMaterial = new Material(shader);
                }
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
            var root = new GameObject("Tree");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = footPosition;
            Primitive(PrimitiveType.Cylinder, "Trunk", root.transform,
                Vector3.up * height * 0.5f, new Vector3(0.4f, height * 0.5f, 0.4f), new Color(0.45f, 0.3f, 0.18f));
            Primitive(PrimitiveType.Sphere, "Canopy", root.transform,
                Vector3.up * (height + 0.6f), Vector3.one * 2.6f, new Color(0.3f, 0.6f, 0.32f));
            return root;
        }

        // ---------------- 玩家 ----------------

        /// <summary>创建玩家：CharacterController + 占位胶囊 + 朝向指示。</summary>
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

            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            body.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.35f, 0.55f, 0.9f));
            Object.Destroy(body.GetComponent<Collider>());       // 碰撞交给 CharacterController

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            nose.transform.SetParent(go.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.45f, 0.38f);
            nose.transform.localScale = new Vector3(0.22f, 0.22f, 0.22f);
            nose.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.95f, 0.95f, 1f));
            Object.Destroy(nose.GetComponent<Collider>());

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

            // 地面 60x60
            var ground = Primitive(PrimitiveType.Plane, "Ground", root,
                Vector3.zero, new Vector3(6f, 1f, 6f), new Color(0.56f, 0.66f, 0.5f));
            ground.isStatic = true;

            // 外围围墙
            Color wallColor = new Color(0.76f, 0.73f, 0.68f);
            Box("Wall_N", root, new Vector3(0f, 0f, 30f), new Vector3(60f, 3f, 1f), wallColor);
            Box("Wall_S", root, new Vector3(0f, 0f, -30f), new Vector3(60f, 3f, 1f), wallColor);
            Box("Wall_E", root, new Vector3(30f, 0f, 0f), new Vector3(1f, 3f, 60f), wallColor);
            Box("Wall_W", root, new Vector3(-30f, 0f, 0f), new Vector3(1f, 3f, 60f), wallColor);

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

            // 跳台
            Color boxColor = new Color(0.8f, 0.62f, 0.45f);
            Box("Crate_A", root, new Vector3(-6f, 0f, -14f), new Vector3(2f, 0.6f, 2f), boxColor);
            Box("Crate_B", root, new Vector3(-8f, 0f, -14f), new Vector3(2f, 1.2f, 2f), boxColor);
            Box("Crate_C", root, new Vector3(-10f, 0f, -14f), new Vector3(2f, 1.8f, 2f), boxColor);

            // NPC：小满
            var npc = Primitive(PrimitiveType.Capsule, "NPC_Xiaoman", root,
                new Vector3(4f, 1f, 2f), new Vector3(0.8f, 1f, 0.8f), new Color(0.95f, 0.62f, 0.72f));
            npc.transform.localRotation = Quaternion.Euler(0f, 200f, 0f);
            var npcTalk = npc.AddComponent<DialogueInteractable>();
            npcTalk.displayName = GameLanguage.IsEnglish ? "Xiaoman" : "小满";
            npcTalk.storyFile = story;
            npcTalk.startNode = "xiaoman_meet";
            npcTalk.isCharacter = true;
            WorldLabel.Attach(npc, npcTalk.displayName, 1.6f);

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
                Primitive(PrimitiveType.Cylinder, "Grass", root,
                    new Vector3(14f + Mathf.Cos(a) * 1.6f, 0.25f, -14f + Mathf.Sin(a) * 1.6f),
                    new Vector3(1.1f, 0.25f, 1.1f), grass).isStatic = true;
            }
            var key = Primitive(PrimitiveType.Cube, "Pickup_Key", root,
                new Vector3(14f, 0.7f, -14f), new Vector3(0.35f, 0.35f, 0.35f), new Color(1f, 0.85f, 0.2f));
            key.transform.localRotation = Quaternion.Euler(35f, 0f, 45f);
            key.GetComponent<Collider>().isTrigger = true;
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

            var crystal = Primitive(PrimitiveType.Sphere, "Crystal", root,
                new Vector3(0f, 1.2f, 21f), Vector3.one * 0.9f, new Color(0.5f, 0.9f, 1f));
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
