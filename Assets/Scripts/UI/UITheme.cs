using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace VN
{
    /// <summary>
    /// 运行时构建 UGUI 的小工具集合。使用系统 CJK 字体以正确显示中文。
    /// </summary>
    public static class UITheme
    {
        private static Font _font;
        private static readonly Color PencilInk = new Color(0.25f, 0.22f, 0.31f, 1f);
        private static readonly Color Parchment = new Color(0.96f, 0.91f, 0.79f, 0.97f);
        private static readonly Color Rose = new Color(0.79f, 0.51f, 0.57f, 1f);

        public static Font CJKFont
        {
            get
            {
                if (_font == null)
                {
                    // 优先用笔画更硬朗的中文字体，缩放到小窗口时更清晰。
                    _font = Font.CreateDynamicFontFromOSFont(
                        new[] { "KaiTi", "楷体", "STKaiti", "Microsoft YaHei", "微软雅黑", "SimHei", "Arial" }, 48);
                    if (_font == null)
                        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        public static RectTransform FullStretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static GameObject NewUIObject(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static Image AddImage(string name, Transform parent, Color color)
        {
            var go = NewUIObject(name, parent);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        /// <summary>读取生成的童话 UI Sprite；资源缺失时返回 null，所有界面仍可用纯色回退。</summary>
        public static Sprite LoadArtSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            return Resources.Load<Sprite>("Art/UI/" + name);
        }

        public static Outline AddOutline(Graphic graphic, Color color, Vector2 distance)
        {
            var outline = graphic.gameObject.AddComponent<Outline>();
            outline.effectColor = color;
            outline.effectDistance = distance;
            outline.useGraphicAlpha = true;
            return outline;
        }

        public static Image AddPanel(string name, Transform parent, Color fillColor)
        {
            // 名字中含 Dialogue/Speaker/Prompt/Pause 时优先套生成的羊皮纸边框。
            string spriteName = name.Contains("Speaker") ? "SpeakerPanel"
                : name.Contains("Dialogue") ? "DialoguePanel"
                : "ParchmentPanel";
            var art = LoadArtSprite(spriteName);
            var img = AddImage(name, parent, art != null ? Color.white : Color.Lerp(fillColor, Parchment, 0.42f));
            if (art != null)
            {
                img.sprite = art;
                img.type = art.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
            }
            // 两层略微错开的描边，模拟彩铅反复勾线。
            AddOutline(img, new Color(PencilInk.r, PencilInk.g, PencilInk.b, 0.72f), new Vector2(3f, -3f));
            AddOutline(img, new Color(1f, 1f, 1f, 0.26f), new Vector2(-1.3f, 1.3f));
            return img;
        }

        public static Text AddText(string name, Transform parent, string content,
            int fontSize, Color color, TextAnchor anchor = TextAnchor.UpperLeft)
        {
            var go = NewUIObject(name, parent);
            var txt = go.AddComponent<Text>();
            txt.font = CJKFont;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.color = color;
            txt.alignment = anchor;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.supportRichText = true;
            txt.raycastTarget = false;
            return txt;
        }

        public static Button AddButton(string name, Transform parent, string label,
            int fontSize, UnityAction onClick)
        {
            var img = AddPanel(name, parent, Parchment);
            var buttonArt = LoadArtSprite("Button");
            if (buttonArt != null)
            {
                img.sprite = buttonArt;
                img.type = buttonArt.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
                img.color = Color.white;
            }
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;

            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.91f, 0.78f, 1f);
            colors.pressedColor = Rose;
            colors.disabledColor = new Color(0.75f, 0.75f, 0.75f, 1f);
            colors.fadeDuration = 0.08f;
            btn.colors = colors;

            var label2 = AddText("Label", img.transform, label, fontSize, PencilInk, TextAnchor.MiddleCenter);
            FullStretch(label2.rectTransform);

            if (onClick != null) btn.onClick.AddListener(onClick);
            return btn;
        }

        public static void SetRect(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
        }
    }
}
