using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN.World
{
    /// <summary>需要 AI 生成（或程序化回退）的单个道具规格。crop 为参考图上的归一化裁剪框（左上为原点）。</summary>
    [Serializable]
    public class ReferencePropSpec
    {
        public string id;
        public string sceneId;
        public string displayName;
        public string prompt;
        public Rect crop;
        public float targetHeight = 1f;   // 导入后把模型等比缩放到这个高度（米）
        public bool aiCandidate = true;
        /// <summary>人形角色：生成时用 A-pose，完成后自动绑骨并套用 animationActionIds 里的预设动画。</summary>
        public bool isCharacter;
        /// <summary>Meshy 动画库 action_id（0=Idle, 11/12=Idle 变体, 32/33=坐姿 Idle, 56=站着聊天, 30=散步）。</summary>
        public int[] animationActionIds = System.Array.Empty<int>();

        public ReferencePropSpec(string id, string sceneId, string displayName, string prompt, Rect crop, float targetHeight, bool ai = true)
        {
            this.id = id; this.sceneId = sceneId; this.displayName = displayName; this.prompt = prompt;
            this.crop = crop; this.targetHeight = targetHeight; aiCandidate = ai;
        }

        public static ReferencePropSpec Character(string id, string sceneId, string displayName, string prompt, float height, params int[] actions)
        {
            return new ReferencePropSpec(id, sceneId, displayName, prompt, Rect.zero, height, true)
            {
                isCharacter = true,
                animationActionIds = actions != null && actions.Length > 0 ? actions : new[] { 0 },
            };
        }
    }

    [Serializable]
    public class ReferencePlacement
    {
        public string propId;
        public Vector3 position;
        public float yaw;
        public float scale = 1f;

        public ReferencePlacement(string propId, Vector3 position, float yaw = 0f, float scale = 1f)
        {
            this.propId = propId; this.position = position; this.yaw = yaw; this.scale = scale;
        }
    }

    /// <summary>一幅参考画作对应的可游玩场景定义。</summary>
    [Serializable]
    public class ReferenceSceneDef
    {
        public string id;
        public string title;
        public string imageName;           // Resources/Art/References/ 下的贴图名
        public Rect imageUv = new Rect(0, 0, 1, 1); // 画框显示用的 UV 子区域
        public Vector3 origin;             // 世界中的放置位置（远离村庄）
        public Vector3 spawnLocal;         // 传送到达点
        public float spawnYaw;
        public Vector3 portalInVillage;    // 村庄里的画框位置
        public float portalYaw;
        public string lightingId;
        public List<ReferencePlacement> props = new List<ReferencePlacement>();
        /// <summary>World Labs Marble 生成整幅环境时附带的文字提示。</summary>
        public string marblePrompt;
        /// <summary>有 Marble 环境时仍然单独摆放的道具（通常是会动的角色 / 可交互物）。</summary>
        public List<string> marbleKeepProps = new List<string>();

        public LightingPreset Lighting => ReferenceSceneLibrary.PresetById(lightingId);
    }

    /// <summary>五幅参考画的场景与道具清单。编辑器 AI 工具和运行时搭建都读这里。</summary>
    public static class ReferenceSceneLibrary
    {
        public const string GalleryRootName = "ReferenceGalleries";
        public const string PortalRowName = "PaintingPortals";

        public static LightingPreset PresetById(string id)
        {
            switch (id)
            {
                case "greenhouse": return LightingPreset.Greenhouse();
                case "seaside": return LightingPreset.Seaside();
                case "street": return LightingPreset.ShadedStreet();
                case "library": return LightingPreset.Library();
                case "orange": return LightingPreset.OrangeCourtyard();
                default: return LightingPreset.Village();
            }
        }

        private static List<ReferenceSceneDef> _scenes;
        private static List<ReferencePropSpec> _props;

        public static IReadOnlyList<ReferenceSceneDef> Scenes => _scenes ?? (_scenes = BuildScenes());
        public static IReadOnlyList<ReferencePropSpec> Props => _props ?? (_props = BuildProps());

        public static ReferenceSceneDef Scene(string id)
        {
            foreach (var s in Scenes) if (s.id == id) return s;
            return null;
        }

        public static ReferencePropSpec Prop(string id)
        {
            foreach (var p in Props) if (p.id == id) return p;
            return null;
        }

        private static List<ReferencePropSpec> BuildProps()
        {
            const string style = " Soft hand-drawn storybook illustration style, colored pencil and crayon texture, gentle pastel colors, clean silhouette, single object, no background.";
            const string charStyle = " Gentle pastel colors, clean readable face, detailed clothing folds, no background.";
            return new List<ReferencePropSpec>
            {
                // 温室钢琴
                new ReferencePropSpec("upright_piano", "greenhouse", "黑色立式钢琴", "An old black upright piano with ivory keys and a music stand, side view." + style, new Rect(0.36f, 0.38f, 0.52f, 0.30f), 1.3f),
                new ReferencePropSpec("piano_stool", "greenhouse", "钢琴凳", "A simple black wooden piano stool." + style, new Rect(0.50f, 0.58f, 0.22f, 0.18f), 0.5f),
                new ReferencePropSpec("hydrangea", "greenhouse", "蓝色绣球花丛", "A lush blue and lavender hydrangea bush with big round blooms and dark green leaves." + style, new Rect(0.0f, 0.60f, 0.42f, 0.36f), 1.1f),
                new ReferencePropSpec("potted_vine", "greenhouse", "盆栽藤蔓", "A terracotta pot with trailing green vines and leaves." + style, new Rect(0.60f, 0.36f, 0.28f, 0.18f), 0.6f),
                // 林荫街道
                new ReferencePropSpec("bicycle_blue", "street", "带车篮的自行车", "A classic city bicycle with a front wicker basket, blue-grey frame." + style, new Rect(0.24f, 0.58f, 0.24f, 0.22f), 1.0f),
                new ReferencePropSpec("swing_frame", "street", "红色小滑梯", "A small red metal children's slide with a ladder, playground equipment." + style, new Rect(0.40f, 0.46f, 0.24f, 0.30f), 2.2f),
                // 图书馆
                new ReferencePropSpec("bookshelf", "library", "满书的高书架", "A tall dark wooden bookshelf filled with colorful old books." + style, new Rect(0.0f, 0.08f, 0.14f, 0.62f), 3.2f),
                new ReferencePropSpec("armchair", "library", "布艺扶手椅", "A cozy cream plaid upholstered armchair with wooden legs." + style, new Rect(0.42f, 0.54f, 0.14f, 0.20f), 1.0f),
                new ReferencePropSpec("table_lamp", "library", "台灯", "A vintage brass table lamp with a cream fabric shade." + style, new Rect(0.14f, 0.46f, 0.12f, 0.16f), 0.75f),
                new ReferencePropSpec("statue", "library", "白色大理石雕像", "A small white marble statue of a standing woman in a draped dress on a square pedestal." + style, new Rect(0.03f, 0.45f, 0.10f, 0.22f), 1.9f),
                new ReferencePropSpec("long_table", "library", "长木桌", "A long dark wooden library reading table." + style, new Rect(0.24f, 0.60f, 0.30f, 0.28f), 0.78f),
                new ReferencePropSpec("wooden_chair", "library", "深绿木椅", "A simple dark green painted wooden dining chair with slatted back." + style, new Rect(0.60f, 0.66f, 0.14f, 0.26f), 0.95f),
                new ReferencePropSpec("bell_lamp", "library", "铃铛吊灯", "A hanging pendant lamp shaped like a cream glass bell on a brass chain." + style, new Rect(0.02f, 0.0f, 0.14f, 0.22f), 1.2f),
                new ReferencePropSpec("crystal_ball", "library", "水晶球", "A glowing pale blue crystal ball on a small brass stand." + style, new Rect(0.88f, 0.58f, 0.12f, 0.16f), 0.45f),
                new ReferencePropSpec("candelabra", "library", "烛台", "A brass three-arm candelabra with white candles." + style, new Rect(0.58f, 0.50f, 0.10f, 0.18f), 0.75f),
                // 橘子树院子
                new ReferencePropSpec("bicycle_yellow", "orange", "黄色自行车", "A bright yellow vintage ladies bicycle with a rear rack." + style, new Rect(0.19f, 0.64f, 0.32f, 0.28f), 1.0f),
                new ReferencePropSpec("cat", "orange", "小猫", "A small cream and orange cat sitting upright, looking up." + style, new Rect(0.53f, 0.78f, 0.10f, 0.16f), 0.55f),
                new ReferencePropSpec("yellow_door", "orange", "黄色木门", "A bright yellow wooden door in a plain concrete frame." + style, new Rect(0.25f, 0.36f, 0.11f, 0.28f), 2.3f),
                new ReferencePropSpec("red_crate", "orange", "红色小箱", "A small red plastic crate." + style, new Rect(0.17f, 0.58f, 0.07f, 0.09f), 0.36f),
                // 人物：用 Assets/Art/AI3D/Refs/<id>_front/back 的正反面设定图多视角重建，A-pose → 自动绑骨 → 预设动画
                ReferencePropSpec.Character("suit_man", "greenhouse", "白西装男子", "A young man in a cream linen suit with short dark brown hair and brown leather shoes, soft anime style, clean fabric." + charStyle, 1.8f, 56),
                ReferencePropSpec.Character("piano_girl", "greenhouse", "弹琴的少女", "A girl with long light brown wavy hair in a long cream white dress with a sash, soft anime style." + charStyle, 1.6f, 32),
                ReferencePropSpec.Character("hooded_figure", "seaside", "连帽衫的人", "A person in a grey hooded jacket with the hood up, dark green long skirt, brown shoulder bag, dark boots, soft anime style." + charStyle, 1.8f, 0),
                ReferencePropSpec.Character("rabbit_girl", "seaside", "兔耳少女", "A girl with long pale blonde hair and tall white rabbit ears, grey-blue jacket, dark green pleated skirt, yellow tights, green sneakers, soft anime style." + charStyle, 1.7f, 11),
                ReferencePropSpec.Character("street_girl", "street", "推车的少女", "A cheerful schoolgirl with long brown hair, white sailor blouse with navy collar, navy pleated skirt, white socks, brown loafers, soft anime style." + charStyle, 1.7f, 12),
                ReferencePropSpec.Character("courtyard_girl", "orange", "院子里的少女", "A young woman with auburn hair in a low bun, white long-sleeve blouse, long deep teal-blue skirt, dark flat shoes, soft anime style." + charStyle, 1.7f, 56),
                // 仅程序化（树木 / 建筑 / 海鸥等大体量或大量重复的元素交给手绘低模或 Marble 环境）
                new ReferencePropSpec("seagull", "seaside", "海鸥", "", Rect.zero, 0.3f, false),
                new ReferencePropSpec("grass_tuft", "seaside", "草丛", "", Rect.zero, 0.6f, false),
                new ReferencePropSpec("street_tree", "street", "大树", "", Rect.zero, 10f, false),
                new ReferencePropSpec("small_house", "street", "树荫下的房子", "", Rect.zero, 3.6f, false),
                new ReferencePropSpec("ottoman", "library", "脚凳", "", Rect.zero, 0.5f, false),
                new ReferencePropSpec("side_cabinet", "library", "边柜", "", Rect.zero, 0.9f, false),
                new ReferencePropSpec("orange_tree", "orange", "橘子树", "", Rect.zero, 8f, false),
            };
        }

        private static List<ReferenceSceneDef> BuildScenes()
        {
            var list = new List<ReferenceSceneDef>();

            // 1. 温室里的钢琴
            var greenhouse = new ReferenceSceneDef
            {
                id = "greenhouse", title = "温室里的钢琴", imageName = "GreenhousePiano",
                origin = new Vector3(200f, 0f, 0f), spawnLocal = new Vector3(0f, 0f, -7.5f), spawnYaw = 0f,
                portalInVillage = new Vector3(-16f, 0f, -25f), portalYaw = 0f, lightingId = "greenhouse",
                marblePrompt = "A sunlit glass greenhouse interior with an old black upright piano, blue hydrangea bushes, trailing vines and warm light shafts through the glass roof. Soft storybook illustration style, colored pencil texture, pastel colors, no people.",
                marbleKeepProps = { "piano_girl", "suit_man" },
            };
            greenhouse.props.AddRange(new[]
            {
                new ReferencePlacement("upright_piano", new Vector3(0.6f, 0f, 2.4f), 180f),
                new ReferencePlacement("piano_stool", new Vector3(0.6f, 0f, 1.35f), 0f),
                new ReferencePlacement("piano_girl", new Vector3(0.6f, 0f, 1.35f), 0f),
                new ReferencePlacement("suit_man", new Vector3(-2.6f, 0f, 1.2f), 150f),
                new ReferencePlacement("potted_vine", new Vector3(1.2f, 1.42f, 2.5f), 0f),
                new ReferencePlacement("hydrangea", new Vector3(-4.6f, 0f, -3.2f), 10f, 1.3f),
                new ReferencePlacement("hydrangea", new Vector3(-3.4f, 0f, -5.0f), 70f, 1.1f),
                new ReferencePlacement("hydrangea", new Vector3(4.4f, 0f, -1.4f), -20f, 1.0f),
                new ReferencePlacement("hydrangea", new Vector3(5.2f, 0f, -4.0f), 40f, 0.9f),
                new ReferencePlacement("hydrangea", new Vector3(-5.4f, 0f, 1.6f), 0f, 0.9f),
            });
            list.Add(greenhouse);

            // 2. 海边的两个人
            var seaside = new ReferenceSceneDef
            {
                id = "seaside", title = "海边与海鸥", imageName = "Seaside", imageUv = new Rect(0f, 0f, 1f, 0.5f),
                origin = new Vector3(400f, 0f, 0f), spawnLocal = new Vector3(0f, 0f, -9f), spawnYaw = 0f,
                portalInVillage = new Vector3(-8f, 0f, -25f), portalYaw = 0f, lightingId = "seaside",
                marblePrompt = "A bright grassy hill above a calm blue sea with white seagulls flying under a soft pale sky, wide open view, gentle breeze. Soft storybook illustration style, colored pencil texture, pastel colors, no people.",
                marbleKeepProps = { "hooded_figure", "rabbit_girl" },
            };
            seaside.props.AddRange(new[]
            {
                new ReferencePlacement("hooded_figure", new Vector3(-0.9f, 0f, 2.5f), 0f),
                new ReferencePlacement("rabbit_girl", new Vector3(0.7f, 0f, 2.6f), 0f),
            });
            for (int i = 0; i < 26; i++)
            {
                float a = i / 26f * Mathf.PI * 2f;
                float r = 4f + (i % 5) * 2.2f;
                seaside.props.Add(new ReferencePlacement("grass_tuft", new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r * 0.6f - 1f), i * 37f, 0.55f + (i % 3) * 0.1f));
            }
            list.Add(seaside);

            // 3. 林荫街道与自行车
            var street = new ReferenceSceneDef
            {
                id = "street", title = "攀云·林荫街道", imageName = "StreetBicycle", imageUv = new Rect(0f, 0f, 0.58f, 1f),
                origin = new Vector3(600f, 0f, 0f), spawnLocal = new Vector3(0f, 0f, -10f), spawnYaw = 0f,
                portalInVillage = new Vector3(0f, 0f, -25f), portalYaw = 0f, lightingId = "street",
                marblePrompt = "A quiet tree-shaded street with huge leafy trees, dappled sunlight on the pavement, a small house behind the trees, a red children's slide and a parked bicycle with a basket. Soft storybook illustration style, colored pencil texture, pastel greens, no people.",
                marbleKeepProps = { "street_girl" },
            };
            street.props.AddRange(new[]
            {
                new ReferencePlacement("street_tree", new Vector3(-6.5f, 0f, 3f), 0f, 1.1f),
                new ReferencePlacement("street_tree", new Vector3(5.5f, 0f, 8f), 40f, 0.95f),
                new ReferencePlacement("street_tree", new Vector3(-9f, 0f, -9f), 90f, 0.8f),
                new ReferencePlacement("small_house", new Vector3(2f, 0f, 14f), 0f),
                new ReferencePlacement("swing_frame", new Vector3(3.5f, 0f, 3.5f), -30f),
                new ReferencePlacement("bicycle_blue", new Vector3(-2.2f, 0f, -1.2f), 20f),
                new ReferencePlacement("street_girl", new Vector3(-2.9f, 0f, -1.4f), 110f),
            });
            list.Add(street);

            // 4. 图书馆与窗外草原
            var library = new ReferenceSceneDef
            {
                id = "library", title = "图书馆的窗", imageName = "LibraryWindow",
                origin = new Vector3(800f, 0f, 0f), spawnLocal = new Vector3(0f, 0f, -4.6f), spawnYaw = 0f,
                portalInVillage = new Vector3(8f, 0f, -25f), portalYaw = 0f, lightingId = "library",
                marblePrompt = "A cozy old library reading room with tall dark wooden bookshelves full of colorful books, long reading tables, cream plaid armchairs, brass table lamps, a small white marble statue, and a huge window opening onto a sunny green meadow under a blue sky. Soft storybook illustration style, colored pencil texture, warm pastel colors, no people.",
            };
            for (int i = 0; i < 5; i++)
            {
                float z = -6f + i * 3.1f;
                library.props.Add(new ReferencePlacement("bookshelf", new Vector3(-8.7f, 0f, z), 90f));
                library.props.Add(new ReferencePlacement("bookshelf", new Vector3(8.7f, 0f, z), -90f));
            }
            library.props.AddRange(new[]
            {
                new ReferencePlacement("armchair", new Vector3(-1.4f, 0f, 5.2f), 205f),
                new ReferencePlacement("armchair", new Vector3(1.5f, 0f, 5.2f), 155f),
                new ReferencePlacement("ottoman", new Vector3(0f, 0f, 4.0f), 0f),
                new ReferencePlacement("side_cabinet", new Vector3(-5.4f, 0f, 5.6f), 0f),
                new ReferencePlacement("side_cabinet", new Vector3(5.4f, 0f, 5.6f), 0f),
                new ReferencePlacement("table_lamp", new Vector3(-5.9f, 0.9f, 5.6f), 0f),
                new ReferencePlacement("table_lamp", new Vector3(-4.9f, 0.9f, 5.6f), 0f),
                new ReferencePlacement("table_lamp", new Vector3(4.9f, 0.9f, 5.6f), 0f),
                new ReferencePlacement("table_lamp", new Vector3(5.9f, 0.9f, 5.6f), 0f),
                new ReferencePlacement("statue", new Vector3(-6.8f, 0f, 2.0f), 30f),
                new ReferencePlacement("long_table", new Vector3(-4.2f, 0f, -1.0f), 90f),
                new ReferencePlacement("long_table", new Vector3(-4.2f, 0f, -4.6f), 90f),
                new ReferencePlacement("long_table", new Vector3(4.2f, 0f, -1.0f), 90f),
                new ReferencePlacement("long_table", new Vector3(4.2f, 0f, -4.6f), 90f),
                new ReferencePlacement("wooden_chair", new Vector3(-3.3f, 0f, -0.2f), -90f),
                new ReferencePlacement("wooden_chair", new Vector3(-3.3f, 0f, -1.8f), -90f),
                new ReferencePlacement("wooden_chair", new Vector3(-3.3f, 0f, -4.0f), -90f),
                new ReferencePlacement("wooden_chair", new Vector3(-5.1f, 0f, -5.4f), 90f),
                new ReferencePlacement("wooden_chair", new Vector3(3.3f, 0f, -0.2f), 90f),
                new ReferencePlacement("wooden_chair", new Vector3(3.3f, 0f, -1.8f), 90f),
                new ReferencePlacement("wooden_chair", new Vector3(3.3f, 0f, -4.0f), 90f),
                new ReferencePlacement("wooden_chair", new Vector3(5.1f, 0f, -5.4f), -90f),
                new ReferencePlacement("crystal_ball", new Vector3(4.6f, 0.79f, -1.3f), 0f),
                new ReferencePlacement("candelabra", new Vector3(-4.0f, 0.79f, -0.6f), 0f),
                new ReferencePlacement("candelabra", new Vector3(6.2f, 0.9f, 5.6f), 0f),
                new ReferencePlacement("bell_lamp", new Vector3(-6f, 4.4f, 4f), 0f),
                new ReferencePlacement("bell_lamp", new Vector3(-6.8f, 4.1f, 2.2f), 0f),
                new ReferencePlacement("bell_lamp", new Vector3(6f, 4.4f, 4f), 0f),
                new ReferencePlacement("bell_lamp", new Vector3(6.8f, 4.1f, 2.2f), 0f),
                new ReferencePlacement("bell_lamp", new Vector3(-4f, 4.6f, -3f), 0f),
                new ReferencePlacement("bell_lamp", new Vector3(4f, 4.6f, -3f), 0f),
            });
            list.Add(library);

            // 5. 橘子树院子
            var orange = new ReferenceSceneDef
            {
                id = "orange", title = "橘子树的院子", imageName = "OrangeCourtyard",
                origin = new Vector3(1000f, 0f, 0f), spawnLocal = new Vector3(0f, 0f, -9f), spawnYaw = 0f,
                portalInVillage = new Vector3(16f, 0f, -25f), portalYaw = 0f, lightingId = "orange",
                marblePrompt = "A sunny small courtyard with big orange trees full of fruit, a bright yellow wooden door in a plain wall, a red plastic crate, a yellow vintage bicycle, warm light and soft shadows on the ground. Soft storybook illustration style, colored pencil texture, pastel colors, no people, no cat.",
                marbleKeepProps = { "courtyard_girl", "cat" },
            };
            orange.props.AddRange(new[]
            {
                new ReferencePlacement("orange_tree", new Vector3(4f, 0f, 6f), 0f, 1.15f),
                new ReferencePlacement("orange_tree", new Vector3(-7f, 0f, 4f), 60f, 0.95f),
                new ReferencePlacement("yellow_door", new Vector3(-4.2f, 0f, 9.9f), 0f),
                new ReferencePlacement("red_crate", new Vector3(-6.6f, 0f, 9.3f), 15f),
                new ReferencePlacement("bicycle_yellow", new Vector3(-1.6f, 0f, 0.4f), 95f),
                new ReferencePlacement("courtyard_girl", new Vector3(-1.4f, 0f, 1.3f), 160f),
                new ReferencePlacement("cat", new Vector3(2.2f, 0f, -1.8f), -150f),
            });
            list.Add(orange);

            return list;
        }
    }
}
