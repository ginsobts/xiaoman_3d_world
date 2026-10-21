using System.Collections.Generic;
using UnityEngine;
using static VN.World.FairytaleArt;

namespace VN.World
{
    /// <summary>
    /// 参考画作里出现的道具的程序化低模版本。
    /// 这些是 AI 生成模型到位前的占位，也是 AI 失败时的保底；
    /// 每个方法的根节点尺寸都按真实米数制作，和 AI 资产可以互换。
    /// </summary>
    public static class ReferenceProps
    {
        private static readonly Color DarkWood = Hex("2E2520");
        private static readonly Color PianoBlack = Hex("1E1B1A");
        private static readonly Color Ivory = Hex("F7F1E1");
        private static readonly Color Hydrangea = Hex("8EA6DF");
        private static readonly Color HydrangeaPale = Hex("C7D3F2");
        private static readonly Color DeepLeaf = Hex("3F6B3A");
        private static readonly Color WhiteSuit = Hex("F1EDE2");
        private static readonly Color WarmWhite = Hex("FBF6EA");
        private static readonly Color Brass = Hex("C9A35A");
        private static readonly Color DeepGreen = Hex("38503F");
        private static readonly Color Orange = Hex("F0922F");
        private static readonly Color Lemon = Hex("F4C83A");
        private static readonly Color Concrete = Hex("CFCBC2");

        /// <summary>按 propId 构建程序化道具；未知 id 返回一个木箱，保证场景不缺位。</summary>
        public static GameObject Build(string propId, Transform parent, Vector3 position, float yaw, float scale)
        {
            GameObject go;
            switch (propId)
            {
                case "upright_piano": go = UprightPiano(parent, position); break;
                case "piano_stool": go = PianoStool(parent, position); break;
                case "hydrangea": go = HydrangeaBush(parent, position, Mathf.RoundToInt(position.x * 7 + position.z * 3)); break;
                case "potted_vine": go = PottedVine(parent, position); break;
                case "suit_man": go = Figure(parent, position, FigureStyle.Suit); break;
                case "piano_girl": go = Figure(parent, position, FigureStyle.SeatedDress); break;
                case "hooded_figure": go = Figure(parent, position, FigureStyle.Hooded); break;
                case "rabbit_girl": go = Figure(parent, position, FigureStyle.RabbitGirl); break;
                case "street_girl": go = Figure(parent, position, FigureStyle.SchoolGirl); break;
                case "courtyard_girl": go = Figure(parent, position, FigureStyle.BlueSkirt); break;
                case "seagull": go = Seagull(parent, position); break;
                case "bicycle_blue": go = Bicycle(parent, position, Hex("5B7A9C"), true); break;
                case "bicycle_yellow": go = Bicycle(parent, position, Lemon, false); break;
                case "swing_frame": go = SlideFrame(parent, position); break;
                case "street_tree": go = BigTree(parent, position, 7.5f, 14f, Sage, LeafLight, null); break;
                case "orange_tree": go = BigTree(parent, position, 5.5f, 11f, DeepLeaf, Sage, Orange); break;
                case "small_house": go = SmallHouse(parent, position); break;
                case "bookshelf": go = Bookshelf(parent, position); break;
                case "armchair": go = Armchair(parent, position); break;
                case "ottoman": go = Ottoman(parent, position); break;
                case "table_lamp": go = TableLamp(parent, position); break;
                case "statue": go = Statue(parent, position); break;
                case "long_table": go = LongTable(parent, position); break;
                case "wooden_chair": go = WoodenChair(parent, position); break;
                case "bell_lamp": go = BellLamp(parent, position); break;
                case "crystal_ball": go = CrystalBall(parent, position); break;
                case "candelabra": go = Candelabra(parent, position); break;
                case "side_cabinet": go = SideCabinet(parent, position); break;
                case "cat": go = Cat(parent, position); break;
                case "yellow_door": go = YellowDoor(parent, position); break;
                case "red_crate": go = Box("RedCrate", parent, position + Vector3.up * 0.18f, new Vector3(0.5f, 0.36f, 0.4f), Hex("C8453A"), null, true); break;
                case "grass_tuft": go = GrassTuft(parent, position, Mathf.RoundToInt(position.x * 13 + position.z * 29)); break;
                default: go = Box("Crate_" + propId, parent, position + Vector3.up * 0.5f, Vector3.one, Wood, "WoodPlanks", true); break;
            }
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        // ---------------- 温室 ----------------

        public static GameObject UprightPiano(Transform parent, Vector3 position)
        {
            var root = Root("UprightPiano", parent, position);
            Box("Body", root.transform, new Vector3(0f, 0.72f, 0.12f), new Vector3(1.5f, 1.3f, 0.55f), PianoBlack, null, true);
            Box("TopLid", root.transform, new Vector3(0f, 1.39f, 0.1f), new Vector3(1.56f, 0.06f, 0.62f), PianoBlack);
            Box("KeyBed", root.transform, new Vector3(0f, 0.78f, -0.3f), new Vector3(1.46f, 0.1f, 0.34f), DarkWood);
            Box("WhiteKeys", root.transform, new Vector3(0f, 0.85f, -0.32f), new Vector3(1.32f, 0.035f, 0.26f), Ivory);
            for (int i = 0; i < 20; i++)
            {
                if (i % 7 == 2 || i % 7 == 6) continue;
                float x = -0.6f + i * 0.063f;
                Box("BlackKey", root.transform, new Vector3(x, 0.875f, -0.27f), new Vector3(0.03f, 0.025f, 0.15f), PianoBlack);
            }
            Box("Fallboard", root.transform, new Vector3(0f, 1.0f, -0.2f), new Vector3(1.46f, 0.3f, 0.06f), PianoBlack);
            Box("MusicStand", root.transform, new Vector3(0f, 1.15f, -0.17f), new Vector3(0.7f, 0.2f, 0.03f), DarkWood);
            foreach (float x in new[] { -0.68f, 0.68f })
                Box("Leg", root.transform, new Vector3(x, 0.4f, -0.36f), new Vector3(0.08f, 0.8f, 0.08f), PianoBlack);
            foreach (float x in new[] { -0.12f, 0f, 0.12f })
                Box("Pedal", root.transform, new Vector3(x, 0.08f, -0.32f), new Vector3(0.05f, 0.03f, 0.1f), Brass);
            return root;
        }

        public static GameObject PianoStool(Transform parent, Vector3 position)
        {
            var root = Root("PianoStool", parent, position);
            Box("Seat", root.transform, new Vector3(0f, 0.5f, 0f), new Vector3(0.5f, 0.06f, 0.32f), PianoBlack, null, true);
            foreach (var p in new[] { new Vector2(-0.2f, -0.12f), new Vector2(0.2f, -0.12f), new Vector2(-0.2f, 0.12f), new Vector2(0.2f, 0.12f) })
                Box("Leg", root.transform, new Vector3(p.x, 0.24f, p.y), new Vector3(0.05f, 0.48f, 0.05f), PianoBlack);
            return root;
        }

        public static GameObject HydrangeaBush(Transform parent, Vector3 position, int seed)
        {
            var root = Root("Hydrangea", parent, position);
            var random = new System.Random(seed);
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -0.6f, 0.6f), RandomRange(random, 0.2f, 0.55f), RandomRange(random, -0.5f, 0.5f));
                float s = RandomRange(random, 0.5f, 0.9f);
                Part(PrimitiveType.Sphere, "Leaves", root.transform, p, new Vector3(s, s * 0.7f, s), i % 2 == 0 ? DeepLeaf : Sage, "Leaves");
            }
            for (int i = 0; i < 6; i++)
            {
                Vector3 p = new Vector3(RandomRange(random, -0.55f, 0.55f), RandomRange(random, 0.6f, 0.95f), RandomRange(random, -0.45f, 0.45f));
                float s = RandomRange(random, 0.26f, 0.4f);
                var bloom = Part(PrimitiveType.Sphere, "Bloom", root.transform, p, Vector3.one * s, i % 3 == 0 ? HydrangeaPale : Hydrangea);
                for (int k = 0; k < 4; k++)
                    Part(PrimitiveType.Sphere, "Floret", bloom.transform,
                        new Vector3(RandomRange(random, -0.35f, 0.35f), RandomRange(random, -0.2f, 0.4f), RandomRange(random, -0.35f, 0.35f)),
                        Vector3.one * 0.45f, k % 2 == 0 ? HydrangeaPale : Hex("A7B8E8"));
            }
            return root;
        }

        public static GameObject PottedVine(Transform parent, Vector3 position)
        {
            var root = Root("PottedVine", parent, position);
            Part(PrimitiveType.Cylinder, "Pot", root.transform, new Vector3(0f, 0.16f, 0f), new Vector3(0.36f, 0.16f, 0.36f), Hex("A86B4F"));
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Part(PrimitiveType.Sphere, "Leaf", root.transform, new Vector3(Mathf.Cos(a) * 0.18f, 0.42f, Mathf.Sin(a) * 0.18f),
                    new Vector3(0.3f, 0.22f, 0.3f), i % 2 == 0 ? Sage : LeafLight, "Leaves");
                Part(PrimitiveType.Capsule, "Strand", root.transform, new Vector3(Mathf.Cos(a) * 0.25f, 0.05f, Mathf.Sin(a) * 0.25f),
                    new Vector3(0.05f, 0.3f, 0.05f), DeepLeaf, null, new Vector3(Mathf.Sin(a) * 25f, 0f, Mathf.Cos(a) * 25f));
            }
            return root;
        }

        // ---------------- 人物 ----------------

        public enum FigureStyle { Suit, SeatedDress, Hooded, RabbitGirl, SchoolGirl, BlueSkirt, Marble }

        public static GameObject Figure(Transform parent, Vector3 position, FigureStyle style)
        {
            var root = Root("Figure_" + style, parent, position);
            Color skin = style == FigureStyle.Marble ? WarmWhite : Skin;
            Color hair = style == FigureStyle.Marble ? WarmWhite : Hex("5B3F2E");
            Color top, bottom;
            switch (style)
            {
                case FigureStyle.Suit: top = WhiteSuit; bottom = WhiteSuit; break;
                case FigureStyle.SeatedDress: top = WarmWhite; bottom = WarmWhite; hair = Hex("6C4A33"); break;
                case FigureStyle.Hooded: top = Hex("6F7483"); bottom = Hex("4F7A5A"); break;
                case FigureStyle.RabbitGirl: top = Hex("7E8290"); bottom = Hex("4A6A4F"); hair = Hex("E8C9A8"); break;
                case FigureStyle.SchoolGirl: top = WarmWhite; bottom = Hex("3E4F7A"); break;
                case FigureStyle.BlueSkirt: top = WarmWhite; bottom = Hex("35486B"); hair = Hex("B2733F"); break;
                default: top = WarmWhite; bottom = WarmWhite; break;
            }

            bool seated = style == FigureStyle.SeatedDress;
            float torsoY = seated ? 0.95f : 1.05f;
            if (seated)
            {
                Part(PrimitiveType.Cylinder, "Skirt", root.transform, new Vector3(0f, 0.55f, 0.05f), new Vector3(0.62f, 0.12f, 0.6f), bottom);
                Part(PrimitiveType.Capsule, "LegL", root.transform, new Vector3(-0.14f, 0.3f, 0.22f), new Vector3(0.16f, 0.3f, 0.16f), Hex("2A2A33"));
                Part(PrimitiveType.Capsule, "LegR", root.transform, new Vector3(0.14f, 0.3f, 0.22f), new Vector3(0.16f, 0.3f, 0.16f), Hex("2A2A33"));
                Box("Torso", root.transform, new Vector3(0f, torsoY, 0f), new Vector3(0.44f, 0.6f, 0.26f), top);
                Part(PrimitiveType.Capsule, "ArmL", root.transform, new Vector3(-0.26f, 0.95f, 0.18f), new Vector3(0.12f, 0.3f, 0.12f), top, null, new Vector3(60f, 0f, 0f));
                Part(PrimitiveType.Capsule, "ArmR", root.transform, new Vector3(0.26f, 0.95f, 0.18f), new Vector3(0.12f, 0.3f, 0.12f), top, null, new Vector3(60f, 0f, 0f));
            }
            else
            {
                bool skirt = style == FigureStyle.RabbitGirl || style == FigureStyle.SchoolGirl || style == FigureStyle.BlueSkirt;
                if (skirt)
                {
                    float skirtH = style == FigureStyle.BlueSkirt ? 0.42f : 0.26f;
                    Part(PrimitiveType.Cylinder, "Skirt", root.transform, new Vector3(0f, 0.62f + skirtH * 0.5f - 0.1f, 0f), new Vector3(0.52f, skirtH, 0.48f), bottom);
                    Part(PrimitiveType.Capsule, "LegL", root.transform, new Vector3(-0.12f, 0.35f, 0f), new Vector3(0.15f, 0.35f, 0.15f), style == FigureStyle.RabbitGirl ? Hex("3A3A40") : Skin);
                    Part(PrimitiveType.Capsule, "LegR", root.transform, new Vector3(0.12f, 0.35f, 0f), new Vector3(0.15f, 0.35f, 0.15f), style == FigureStyle.RabbitGirl ? Hex("3A3A40") : Skin);
                }
                else
                {
                    Part(PrimitiveType.Capsule, "LegL", root.transform, new Vector3(-0.14f, 0.48f, 0f), new Vector3(0.2f, 0.42f, 0.2f), bottom);
                    Part(PrimitiveType.Capsule, "LegR", root.transform, new Vector3(0.14f, 0.48f, 0f), new Vector3(0.2f, 0.42f, 0.2f), bottom);
                }
                Box("ShoeL", root.transform, new Vector3(-0.13f, 0.06f, 0.05f), new Vector3(0.18f, 0.12f, 0.3f), Hex("2A2320"));
                Box("ShoeR", root.transform, new Vector3(0.13f, 0.06f, 0.05f), new Vector3(0.18f, 0.12f, 0.3f), Hex("2A2320"));
                Box("Torso", root.transform, new Vector3(0f, torsoY + 0.08f, 0f), new Vector3(0.48f, 0.64f, 0.28f), top);
                if (style == FigureStyle.Suit)
                {
                    // 交叉的双臂
                    Part(PrimitiveType.Capsule, "ArmL", root.transform, new Vector3(-0.1f, 1.1f, -0.2f), new Vector3(0.13f, 0.28f, 0.13f), top, null, new Vector3(0f, 0f, 80f));
                    Part(PrimitiveType.Capsule, "ArmR", root.transform, new Vector3(0.1f, 1.0f, -0.2f), new Vector3(0.13f, 0.28f, 0.13f), top, null, new Vector3(0f, 0f, -80f));
                    Box("Tie", root.transform, new Vector3(0f, 1.2f, -0.15f), new Vector3(0.07f, 0.34f, 0.03f), Hex("8794B5"));
                }
                else
                {
                    Part(PrimitiveType.Capsule, "ArmL", root.transform, new Vector3(-0.32f, 1.05f, 0f), new Vector3(0.13f, 0.34f, 0.13f), top, null, new Vector3(0f, 0f, -8f));
                    Part(PrimitiveType.Capsule, "ArmR", root.transform, new Vector3(0.32f, 1.05f, 0f), new Vector3(0.13f, 0.34f, 0.13f), top, null, new Vector3(0f, 0f, 8f));
                }
                if (style == FigureStyle.Hooded)
                {
                    Box("Bag", root.transform, new Vector3(0.3f, 0.85f, -0.08f), new Vector3(0.22f, 0.3f, 0.16f), Hex("5A5F6E"));
                }
            }

            float headY = seated ? 1.5f : 1.68f;
            Part(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, headY, 0f), new Vector3(0.5f, 0.54f, 0.48f), skin);
            if (style == FigureStyle.Hooded)
            {
                Part(PrimitiveType.Sphere, "Hood", root.transform, new Vector3(0f, headY + 0.06f, -0.06f), new Vector3(0.62f, 0.66f, 0.6f), top);
            }
            else
            {
                Part(PrimitiveType.Sphere, "Hair", root.transform, new Vector3(0f, headY + 0.08f, -0.08f), new Vector3(0.58f, 0.58f, 0.54f), hair);
                if (style == FigureStyle.SeatedDress || style == FigureStyle.SchoolGirl || style == FigureStyle.RabbitGirl)
                    Part(PrimitiveType.Capsule, "LongHair", root.transform, new Vector3(0f, headY - 0.32f, -0.16f), new Vector3(0.42f, 0.42f, 0.2f), hair);
                if (style != FigureStyle.Marble)
                    Part(PrimitiveType.Sphere, "Face", root.transform, new Vector3(0f, headY - 0.02f, 0.16f), new Vector3(0.42f, 0.44f, 0.28f), skin);
            }
            if (style == FigureStyle.RabbitGirl)
            {
                Part(PrimitiveType.Capsule, "EarL", root.transform, new Vector3(-0.1f, headY + 0.6f, -0.02f), new Vector3(0.12f, 0.36f, 0.08f), WarmWhite, null, new Vector3(0f, 0f, 8f));
                Part(PrimitiveType.Capsule, "EarR", root.transform, new Vector3(0.1f, headY + 0.6f, -0.02f), new Vector3(0.12f, 0.36f, 0.08f), WarmWhite, null, new Vector3(0f, 0f, -8f));
            }
            return root;
        }

        // ---------------- 海边 ----------------

        public static GameObject Seagull(Transform parent, Vector3 position)
        {
            var root = Root("Seagull", parent, position);
            Part(PrimitiveType.Capsule, "Body", root.transform, Vector3.zero, new Vector3(0.18f, 0.22f, 0.18f), WarmWhite, null, new Vector3(90f, 0f, 0f));
            Part(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 0.05f, 0.22f), Vector3.one * 0.14f, WarmWhite);
            Box("Beak", root.transform, new Vector3(0f, 0.04f, 0.32f), new Vector3(0.04f, 0.03f, 0.1f), Mustard);
            var wingL = Box("WingL", root.transform, new Vector3(-0.32f, 0.03f, 0f), new Vector3(0.6f, 0.02f, 0.2f), WarmWhite);
            var wingR = Box("WingR", root.transform, new Vector3(0.32f, 0.03f, 0f), new Vector3(0.6f, 0.02f, 0.2f), WarmWhite);
            Box("TipL", root.transform, new Vector3(-0.62f, 0.05f, -0.02f), new Vector3(0.14f, 0.02f, 0.16f), Hex("6B6B70"));
            Box("TipR", root.transform, new Vector3(0.62f, 0.05f, -0.02f), new Vector3(0.14f, 0.02f, 0.16f), Hex("6B6B70"));
            var flap = root.AddComponent<WingFlap>();
            flap.left = wingL.transform;
            flap.right = wingR.transform;
            return root;
        }

        public static GameObject GrassTuft(Transform parent, Vector3 position, int seed)
        {
            var root = Root("GrassTuft", parent, position);
            var random = new System.Random(seed);
            for (int i = 0; i < 7; i++)
            {
                float h = RandomRange(random, 0.3f, 0.75f);
                Part(PrimitiveType.Cube, "Blade", root.transform,
                    new Vector3(RandomRange(random, -0.3f, 0.3f), h * 0.5f, RandomRange(random, -0.3f, 0.3f)),
                    new Vector3(0.05f, h, 0.015f), i % 2 == 0 ? Sage : LeafLight, null,
                    new Vector3(RandomRange(random, -14f, 14f), RandomRange(random, 0f, 180f), RandomRange(random, -14f, 14f)));
            }
            var sway = root.AddComponent<StorybookMotion>();
            sway.mode = StorybookMotion.Mode.Sway;
            sway.amount = 0.12f;
            sway.speed = RandomRange(random, 1.1f, 1.9f);
            return root;
        }

        // ---------------- 街道 / 院子 ----------------

        public static GameObject Bicycle(Transform parent, Vector3 position, Color frame, bool withBasket)
        {
            var root = Root("Bicycle", parent, position);
            Color tire = Hex("2B2B2E");
            foreach (float z in new[] { -0.55f, 0.55f })
            {
                Part(PrimitiveType.Cylinder, "Tire", root.transform, new Vector3(0f, 0.34f, z), new Vector3(0.68f, 0.025f, 0.68f), tire, null, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cylinder, "Rim", root.transform, new Vector3(0f, 0.34f, z), new Vector3(0.58f, 0.03f, 0.58f), Hex("D8D8D0"), null, new Vector3(0f, 0f, 90f));
                Part(PrimitiveType.Cylinder, "Hub", root.transform, new Vector3(0f, 0.34f, z), new Vector3(0.1f, 0.05f, 0.1f), Hex("8A8A86"), null, new Vector3(0f, 0f, 90f));
            }
            Box("TopTube", root.transform, new Vector3(0f, 0.78f, 0.05f), new Vector3(0.04f, 0.04f, 0.6f), frame);
            Box("DownTube", root.transform, new Vector3(0f, 0.6f, 0.08f), new Vector3(0.04f, 0.04f, 0.75f), frame).transform.localRotation = Quaternion.Euler(-35f, 0f, 0f);
            Box("SeatTube", root.transform, new Vector3(0f, 0.6f, -0.22f), new Vector3(0.04f, 0.5f, 0.04f), frame).transform.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Box("Fork", root.transform, new Vector3(0f, 0.6f, 0.5f), new Vector3(0.04f, 0.55f, 0.04f), frame).transform.localRotation = Quaternion.Euler(18f, 0f, 0f);
            Box("ChainStay", root.transform, new Vector3(0f, 0.36f, -0.3f), new Vector3(0.03f, 0.03f, 0.5f), frame);
            Box("Handlebar", root.transform, new Vector3(0f, 0.98f, 0.46f), new Vector3(0.52f, 0.03f, 0.03f), Hex("8A8A86"));
            Box("Saddle", root.transform, new Vector3(0f, 0.92f, -0.3f), new Vector3(0.16f, 0.06f, 0.26f), Hex("4A3A30"));
            Part(PrimitiveType.Cylinder, "Crank", root.transform, new Vector3(0f, 0.38f, -0.05f), new Vector3(0.18f, 0.02f, 0.18f), Hex("8A8A86"), null, new Vector3(0f, 0f, 90f));
            if (withBasket)
                Box("Basket", root.transform, new Vector3(0f, 0.95f, 0.72f), new Vector3(0.34f, 0.24f, 0.26f), Hex("D9B98A"), "WoodPlanks");
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.5f, 1f, 1.8f);
            return root;
        }

        public static GameObject SlideFrame(Transform parent, Vector3 position)
        {
            var root = Root("RedSlide", parent, position);
            Color red = Hex("C8503C");
            Color post = Hex("B8B0A2");
            Box("PostA", root.transform, new Vector3(-0.5f, 1.1f, -0.9f), new Vector3(0.1f, 2.2f, 0.1f), post);
            Box("PostB", root.transform, new Vector3(0.5f, 1.1f, -0.9f), new Vector3(0.1f, 2.2f, 0.1f), post);
            Box("Platform", root.transform, new Vector3(0f, 2.15f, -0.9f), new Vector3(1.3f, 0.1f, 0.9f), red);
            var slide = Box("Slide", root.transform, new Vector3(0f, 1.15f, 0.6f), new Vector3(0.9f, 0.08f, 3.2f), red, null, true);
            slide.transform.localRotation = Quaternion.Euler(36f, 0f, 0f);
            Box("RailL", root.transform, new Vector3(-0.47f, 1.35f, 0.6f), new Vector3(0.06f, 0.3f, 3.1f), red).transform.localRotation = Quaternion.Euler(36f, 0f, 0f);
            Box("RailR", root.transform, new Vector3(0.47f, 1.35f, 0.6f), new Vector3(0.06f, 0.3f, 3.1f), red).transform.localRotation = Quaternion.Euler(36f, 0f, 0f);
            for (int i = 0; i < 5; i++)
                Box("Rung", root.transform, new Vector3(0f, 0.4f + i * 0.4f, -1.4f), new Vector3(0.9f, 0.05f, 0.05f), post);
            Box("LadderL", root.transform, new Vector3(-0.45f, 1.1f, -1.4f), new Vector3(0.06f, 2.2f, 0.06f), post);
            Box("LadderR", root.transform, new Vector3(0.45f, 1.1f, -1.4f), new Vector3(0.06f, 2.2f, 0.06f), post);
            return root;
        }

        public static GameObject BigTree(Transform parent, Vector3 position, float trunkHeight, float canopyWidth,
            Color leafA, Color leafB, Color? fruit)
        {
            var root = Root("BigTree", parent, position);
            int seed = Mathf.RoundToInt(position.x * 11f + position.z * 17f);
            var random = new System.Random(seed);
            Part(PrimitiveType.Cylinder, "Trunk", root.transform, new Vector3(0f, trunkHeight * 0.5f, 0f),
                new Vector3(0.9f, trunkHeight * 0.5f, 0.9f), Bark, "Bark", new Vector3(0f, 0f, RandomRange(random, -3f, 3f)), true);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f + 0.4f;
                var branch = Part(PrimitiveType.Cylinder, "Branch", root.transform,
                    new Vector3(Mathf.Cos(a) * canopyWidth * 0.18f, trunkHeight * 0.95f, Mathf.Sin(a) * canopyWidth * 0.18f),
                    new Vector3(0.28f, canopyWidth * 0.2f, 0.28f), Bark, "Bark");
                branch.transform.localRotation = Quaternion.Euler(Mathf.Sin(a) * 55f, 0f, -Mathf.Cos(a) * 55f);
            }
            int clusters = 16;
            for (int i = 0; i < clusters; i++)
            {
                float a = i / (float)clusters * Mathf.PI * 2f;
                float r = (i % 2 == 0 ? 0.42f : 0.26f) * canopyWidth + RandomRange(random, -0.6f, 0.6f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, trunkHeight + RandomRange(random, 0.2f, 2.6f) - r * 0.08f, Mathf.Sin(a) * r);
                float s = RandomRange(random, 2.6f, 4.2f);
                Part(PrimitiveType.Sphere, "Canopy", root.transform, p, new Vector3(s, s * 0.6f, s), i % 2 == 0 ? leafA : leafB, "Leaves");
                if (fruit.HasValue)
                    for (int k = 0; k < 3; k++)
                        Part(PrimitiveType.Sphere, "Fruit", root.transform,
                            p + new Vector3(RandomRange(random, -s * 0.4f, s * 0.4f), RandomRange(random, -s * 0.3f, 0f), RandomRange(random, -s * 0.4f, s * 0.4f)),
                            Vector3.one * 0.26f, fruit.Value);
            }
            Part(PrimitiveType.Sphere, "Crown", root.transform, new Vector3(0f, trunkHeight + 2.2f, 0f),
                new Vector3(canopyWidth * 0.55f, canopyWidth * 0.3f, canopyWidth * 0.55f), leafA, "Leaves");
            return root;
        }

        public static GameObject SmallHouse(Transform parent, Vector3 position)
        {
            var root = Root("ShadedHouse", parent, position);
            Box("Walls", root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(6f, 3.2f, 5f), Hex("E9E6DC"), "CreamPlaster", true);
            Box("Roof", root.transform, new Vector3(0f, 3.4f, 0f), new Vector3(6.6f, 0.35f, 5.6f), Hex("8C7A6B"), "RoofTiles");
            Box("Door", root.transform, new Vector3(-1.2f, 1.05f, -2.52f), new Vector3(1f, 2.1f, 0.1f), Hex("6A5A4A"), "WoodPlanks");
            foreach (float x in new[] { 1.0f, 2.2f })
            {
                Box("Window", root.transform, new Vector3(x, 1.8f, -2.52f), new Vector3(0.8f, 1.0f, 0.08f), Hex("7E9AB0"));
                Box("Sill", root.transform, new Vector3(x, 1.28f, -2.58f), new Vector3(0.95f, 0.08f, 0.2f), WarmWhite);
            }
            Box("AC", root.transform, new Vector3(2.4f, 2.5f, -2.6f), new Vector3(0.7f, 0.5f, 0.25f), Hex("D5D5D0"));
            return root;
        }

        public static GameObject Cat(Transform parent, Vector3 position)
        {
            var root = Root("Cat", parent, position);
            Color fur = Hex("E8D3B8");
            Color patch = Hex("D9924A");
            Part(PrimitiveType.Capsule, "Body", root.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.2f, 0.2f, 0.2f), fur);
            Part(PrimitiveType.Sphere, "Head", root.transform, new Vector3(0f, 0.42f, 0.06f), Vector3.one * 0.2f, fur);
            Part(PrimitiveType.Sphere, "Patch", root.transform, new Vector3(0.05f, 0.47f, 0.05f), new Vector3(0.12f, 0.08f, 0.12f), patch);
            Box("EarL", root.transform, new Vector3(-0.06f, 0.53f, 0.04f), new Vector3(0.05f, 0.07f, 0.03f), fur).transform.localRotation = Quaternion.Euler(0f, 0f, 20f);
            Box("EarR", root.transform, new Vector3(0.06f, 0.53f, 0.04f), new Vector3(0.05f, 0.07f, 0.03f), fur).transform.localRotation = Quaternion.Euler(0f, 0f, -20f);
            Part(PrimitiveType.Capsule, "Tail", root.transform, new Vector3(0.08f, 0.14f, -0.16f), new Vector3(0.04f, 0.16f, 0.04f), patch, null, new Vector3(-40f, 0f, 30f));
            foreach (var p in new[] { new Vector3(-0.06f, 0.05f, 0.05f), new Vector3(0.06f, 0.05f, 0.05f) })
                Part(PrimitiveType.Capsule, "Paw", root.transform, p, new Vector3(0.05f, 0.06f, 0.05f), fur);
            var motion = root.AddComponent<StorybookMotion>();
            motion.mode = StorybookMotion.Mode.Sway;
            motion.amount = 0.03f;
            return root;
        }

        public static GameObject YellowDoor(Transform parent, Vector3 position)
        {
            var root = Root("YellowDoor", parent, position);
            Box("Frame", root.transform, new Vector3(0f, 1.15f, 0f), new Vector3(1.3f, 2.3f, 0.18f), Hex("A9A79F"));
            Box("Panel", root.transform, new Vector3(0f, 1.1f, -0.06f), new Vector3(1.05f, 2.15f, 0.08f), Lemon, null, true);
            Box("Inset", root.transform, new Vector3(0f, 1.4f, -0.11f), new Vector3(0.7f, 0.9f, 0.02f), Hex("E3B52D"));
            Box("Inset", root.transform, new Vector3(0f, 0.55f, -0.11f), new Vector3(0.7f, 0.6f, 0.02f), Hex("E3B52D"));
            Part(PrimitiveType.Sphere, "Knob", root.transform, new Vector3(0.4f, 1.05f, -0.13f), Vector3.one * 0.08f, Brass);
            Box("Step", root.transform, new Vector3(0f, 0.06f, -0.35f), new Vector3(1.6f, 0.12f, 0.6f), Concrete);
            return root;
        }

        // ---------------- 图书馆 ----------------

        public static GameObject Bookshelf(Transform parent, Vector3 position)
        {
            var root = Root("Bookshelf", parent, position);
            Color frame = Hex("4A3426");
            Box("Back", root.transform, new Vector3(0f, 1.6f, 0.2f), new Vector3(2.0f, 3.2f, 0.05f), frame, "WoodPlanks");
            Box("SideL", root.transform, new Vector3(-0.98f, 1.6f, 0f), new Vector3(0.05f, 3.2f, 0.45f), frame, "WoodPlanks");
            Box("SideR", root.transform, new Vector3(0.98f, 1.6f, 0f), new Vector3(0.05f, 3.2f, 0.45f), frame, "WoodPlanks");
            Box("Top", root.transform, new Vector3(0f, 3.2f, 0f), new Vector3(2.05f, 0.06f, 0.48f), frame, "WoodPlanks");
            var random = new System.Random(Mathf.RoundToInt(position.x * 3f + position.z * 5f));
            Color[] spines = { Hex("7A3B2E"), Hex("2F4A6B"), Hex("5B6B3A"), Hex("B59A5C"), Hex("3B3B4A"), Hex("8E5A3C"), Hex("A33E3E"), Hex("4F6F6A") };
            for (int shelf = 0; shelf < 6; shelf++)
            {
                float y = 0.45f + shelf * 0.5f;
                Box("Shelf", root.transform, new Vector3(0f, y, 0f), new Vector3(1.95f, 0.04f, 0.42f), frame, "WoodPlanks");
                float x = -0.9f;
                while (x < 0.85f)
                {
                    float w = RandomRange(random, 0.05f, 0.11f);
                    float h = RandomRange(random, 0.26f, 0.42f);
                    if (random.NextDouble() > 0.12)
                        Box("Book", root.transform, new Vector3(x + w * 0.5f, y + 0.02f + h * 0.5f, -0.02f), new Vector3(w, h, 0.3f), spines[random.Next(spines.Length)]);
                    x += w + 0.005f;
                }
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1.6f, 0f);
            col.size = new Vector3(2.05f, 3.2f, 0.5f);
            return root;
        }

        public static GameObject Armchair(Transform parent, Vector3 position)
        {
            var root = Root("Armchair", parent, position);
            Color fabric = Hex("E6E2D6");
            Box("Seat", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.8f, 0.3f, 0.8f), fabric, "Parchment", true);
            Box("Back", root.transform, new Vector3(0f, 0.85f, 0.33f), new Vector3(0.8f, 0.7f, 0.18f), fabric, "Parchment");
            Box("ArmL", root.transform, new Vector3(-0.36f, 0.62f, 0.02f), new Vector3(0.1f, 0.26f, 0.76f), fabric, "Parchment");
            Box("ArmR", root.transform, new Vector3(0.36f, 0.62f, 0.02f), new Vector3(0.1f, 0.26f, 0.76f), fabric, "Parchment");
            Box("Cushion", root.transform, new Vector3(0f, 0.6f, -0.02f), new Vector3(0.6f, 0.08f, 0.6f), Hex("D9CFBB"), "Parchment");
            foreach (var p in new[] { new Vector2(-0.32f, -0.32f), new Vector2(0.32f, -0.32f), new Vector2(-0.32f, 0.32f), new Vector2(0.32f, 0.32f) })
                Box("Leg", root.transform, new Vector3(p.x, 0.13f, p.y), new Vector3(0.06f, 0.26f, 0.06f), DarkWood);
            return root;
        }

        public static GameObject Ottoman(Transform parent, Vector3 position)
        {
            var root = Root("Ottoman", parent, position);
            Box("Top", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1.1f, 0.35f, 0.7f), Hex("4E4A4A"), null, true);
            Box("Tray", root.transform, new Vector3(0f, 0.49f, 0f), new Vector3(0.5f, 0.03f, 0.35f), DarkWood);
            Part(PrimitiveType.Sphere, "Flowers", root.transform, new Vector3(-0.2f, 0.62f, 0f), new Vector3(0.22f, 0.2f, 0.22f), Rose);
            Part(PrimitiveType.Cylinder, "Vase", root.transform, new Vector3(-0.2f, 0.55f, 0f), new Vector3(0.08f, 0.06f, 0.08f), WarmWhite);
            return root;
        }

        public static GameObject TableLamp(Transform parent, Vector3 position)
        {
            var root = Root("TableLamp", parent, position);
            Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.22f, 0.02f, 0.22f), Brass);
            Part(PrimitiveType.Cylinder, "Stem", root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(0.04f, 0.28f, 0.04f), Brass);
            Part(PrimitiveType.Cylinder, "Shade", root.transform, new Vector3(0f, 0.68f, 0f), new Vector3(0.42f, 0.16f, 0.42f), Hex("F3E9D4"), null, null, false, true);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 3.5f;
            light.intensity = 0.9f;
            light.color = new Color(1f, 0.86f, 0.62f);
            root.transform.localPosition += Vector3.zero;
            root.AddComponent<StorybookMotion>().mode = StorybookMotion.Mode.Glow;
            return root;
        }

        public static GameObject Statue(Transform parent, Vector3 position)
        {
            var root = Root("MarbleStatue", parent, position);
            Box("Pedestal", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.9f, 0.6f), Hex("D8D4CC"), "LavenderStone", true);
            var figure = Figure(root.transform, new Vector3(0f, 0.9f, 0f), FigureStyle.Marble);
            figure.transform.localScale = Vector3.one * 0.9f;
            return root;
        }

        public static GameObject LongTable(Transform parent, Vector3 position)
        {
            var root = Root("LongTable", parent, position);
            Box("Top", root.transform, new Vector3(0f, 0.76f, 0f), new Vector3(2.6f, 0.06f, 0.9f), Hex("6B4A34"), "WoodPlanks", true);
            foreach (var p in new[] { new Vector2(-1.2f, -0.38f), new Vector2(1.2f, -0.38f), new Vector2(-1.2f, 0.38f), new Vector2(1.2f, 0.38f) })
                Box("Leg", root.transform, new Vector3(p.x, 0.37f, p.y), new Vector3(0.08f, 0.74f, 0.08f), Hex("4A3426"));
            Box("Papers", root.transform, new Vector3(0.4f, 0.8f, 0.1f), new Vector3(0.5f, 0.02f, 0.35f), WarmWhite, "Parchment");
            Box("Book", root.transform, new Vector3(-0.6f, 0.82f, -0.1f), new Vector3(0.3f, 0.06f, 0.22f), Hex("7A3B2E"));
            return root;
        }

        public static GameObject WoodenChair(Transform parent, Vector3 position)
        {
            var root = Root("WoodenChair", parent, position);
            Color paint = DeepGreen;
            Box("Seat", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.04f, 0.44f), paint, null, true);
            foreach (var p in new[] { new Vector2(-0.19f, -0.19f), new Vector2(0.19f, -0.19f), new Vector2(-0.19f, 0.19f), new Vector2(0.19f, 0.19f) })
                Box("Leg", root.transform, new Vector3(p.x, 0.22f, p.y), new Vector3(0.04f, 0.44f, 0.04f), paint);
            Box("BackPostL", root.transform, new Vector3(-0.19f, 0.75f, 0.2f), new Vector3(0.04f, 0.6f, 0.04f), paint);
            Box("BackPostR", root.transform, new Vector3(0.19f, 0.75f, 0.2f), new Vector3(0.04f, 0.6f, 0.04f), paint);
            for (int i = 0; i < 3; i++)
                Box("Slat", root.transform, new Vector3(0f, 0.62f + i * 0.14f, 0.2f), new Vector3(0.36f, 0.05f, 0.03f), paint);
            return root;
        }

        public static GameObject BellLamp(Transform parent, Vector3 position)
        {
            var root = Root("BellLamp", parent, position);
            Part(PrimitiveType.Cylinder, "Chain", root.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.02f, 0.6f, 0.02f), Brass);
            Part(PrimitiveType.Cylinder, "Bell", root.transform, new Vector3(0f, -0.1f, 0f), new Vector3(0.44f, 0.16f, 0.44f), Hex("F0E6D2"), null, null, false, true);
            Part(PrimitiveType.Sphere, "Dome", root.transform, new Vector3(0f, 0.04f, 0f), new Vector3(0.34f, 0.26f, 0.34f), Hex("F0E6D2"), null, null, false, true);
            Part(PrimitiveType.Sphere, "Bulb", root.transform, new Vector3(0f, -0.2f, 0f), Vector3.one * 0.12f, Cream, null, null, false, true);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 5f;
            light.intensity = 0.8f;
            light.color = new Color(1f, 0.9f, 0.72f);
            root.AddComponent<StorybookMotion>().mode = StorybookMotion.Mode.Glow;
            return root;
        }

        public static GameObject CrystalBall(Transform parent, Vector3 position)
        {
            var root = Root("CrystalBall", parent, position);
            Part(PrimitiveType.Cylinder, "Stand", root.transform, new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.05f, 0.22f), Brass);
            Part(PrimitiveType.Sphere, "Ball", root.transform, new Vector3(0f, 0.26f, 0f), Vector3.one * 0.34f, Hex("9FC4E8"), null, null, false, true);
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            light.intensity = 0.6f;
            light.color = Hex("9FC4E8");
            return root;
        }

        public static GameObject Candelabra(Transform parent, Vector3 position)
        {
            var root = Root("Candelabra", parent, position);
            Part(PrimitiveType.Cylinder, "Base", root.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.18f, 0.02f, 0.18f), Brass);
            Part(PrimitiveType.Cylinder, "Stem", root.transform, new Vector3(0f, 0.25f, 0f), new Vector3(0.03f, 0.23f, 0.03f), Brass);
            Box("Arm", root.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.36f, 0.025f, 0.025f), Brass);
            foreach (float x in new[] { -0.16f, 0f, 0.16f })
            {
                Part(PrimitiveType.Cylinder, "Candle", root.transform, new Vector3(x, 0.55f, 0f), new Vector3(0.035f, 0.12f, 0.035f), WarmWhite);
                Part(PrimitiveType.Sphere, "Flame", root.transform, new Vector3(x, 0.7f, 0f), new Vector3(0.04f, 0.07f, 0.04f), Mustard, null, null, false, true);
            }
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 2.5f;
            light.intensity = 0.7f;
            light.color = new Color(1f, 0.8f, 0.5f);
            root.AddComponent<StorybookMotion>().mode = StorybookMotion.Mode.Glow;
            return root;
        }

        public static GameObject SideCabinet(Transform parent, Vector3 position)
        {
            var root = Root("SideCabinet", parent, position);
            Box("Body", root.transform, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.9f, 0.5f), Hex("C9C2B4"), "CreamPlaster", true);
            for (int i = 0; i < 3; i++)
                Box("Drawer", root.transform, new Vector3(0f, 0.2f + i * 0.26f, -0.26f), new Vector3(1.4f, 0.2f, 0.02f), Hex("B8B0A2"));
            return root;
        }

    }
}
