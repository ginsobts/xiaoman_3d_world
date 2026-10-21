using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace VN
{
    public static class MainMenuUI
    {
        public static void Build(RectTransform root, UnityAction onStart, UnityAction onQuit,
            UnityAction onToggleLanguage, bool storyOk, UnityAction onContinue = null)
        {
            var bg = UITheme.AddImage("MenuBG", root, Color.white);
            UITheme.FullStretch(bg.rectTransform);
            var sky = UITheme.LoadArtSprite("SkyPanorama");
            if (sky != null)
            {
                bg.sprite = sky;
                bg.preserveAspect = false;
            }

            // 半透明标题卡，给主菜单一个绘本封面的视觉中心。
            var titleCard = UITheme.AddPanel("TitleCard", root, new Color(1f, 1f, 1f, 0.9f));
            UITheme.SetRect(titleCard.rectTransform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-430, -190), new Vector2(430, -45));
            var title = UITheme.AddText("Title", titleCard.transform,
                GameLanguage.IsEnglish ? "Xiaoman's Storybook World" : "小满的绘本世界",
                48, new Color(0.28f, 0.24f, 0.34f), TextAnchor.MiddleCenter);
            UITheme.FullStretch(title.rectTransform);

            var silhouette = UITheme.AddImage("XiaoxingSilhouette", root, Color.black);
            silhouette.sprite = AssetCache.GetSprite("xiaoyi_silhouette");
            silhouette.preserveAspect = true;
            silhouette.raycastTarget = false;
            UITheme.SetRect(silhouette.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-260, -170), new Vector2(260, 430));

            // 按钮自下而上排列；有「继续」时整体多一行
            float y = 70f;
            const float h = 80f, gap = 25f;

            var quitBtn = UITheme.AddButton("QuitBtn", root, GameLanguage.MainMenuQuit, 36, onQuit);
            UITheme.SetRect(quitBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, y), new Vector2(220, y + h));
            y += h + gap;

            var langBtn = UITheme.AddButton("LanguageBtn", root, GameLanguage.LanguageButton, 30, onToggleLanguage);
            UITheme.SetRect(langBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, y), new Vector2(220, y + h));
            y += h + gap;

            var startBtn = UITheme.AddButton("StartBtn", root, GameLanguage.MainMenuStart, 40, onStart);
            UITheme.SetRect(startBtn.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-220, y), new Vector2(220, y + h + 5f));
            startBtn.interactable = storyOk;
            y += h + 5f + gap;

            if (onContinue != null)
            {
                var contBtn = UITheme.AddButton("ContinueBtn", root, GameLanguage.ContinueGame, 36, onContinue);
                UITheme.SetRect(contBtn.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-220, y), new Vector2(220, y + h));
                y += h + gap;
            }

            if (!storyOk)
            {
                var warn = UITheme.AddText("Warn", root,
                    GameLanguage.MissingStory, 28, new Color(0.9f, 0.4f, 0.4f), TextAnchor.MiddleCenter);
                UITheme.SetRect(warn.rectTransform,
                    new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                    new Vector2(-400, y), new Vector2(400, y + 60f));
            }
        }
    }
}
