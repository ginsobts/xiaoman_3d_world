using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VN.World
{
    /// <summary>
    /// 探索时的屏幕 UI：底部互动提示、左上操作说明、居中 Toast、暂停菜单。
    /// 全部挂在 GameManager.Root 下，和 VNView 的 UI 同层（VN 创建得晚，会盖在上面）。
    /// </summary>
    public class WorldHUD : MonoBehaviour
    {
        private RectTransform _root;     // 组件所在物体（始终激活，保证协程不中断）
        private RectTransform _layer;    // 实际内容层，SetVisible 开关的是它
        private Image _promptPanel;
        private Text _promptText;
        private Text _hintText;
        private Text _toastText;
        private CanvasGroup _toastGroup;
        private Coroutine _toastRoutine;

        private RectTransform _pauseLayer;

        public static WorldHUD Create(RectTransform root)
        {
            var go = UITheme.NewUIObject("WorldHUD", root);
            var hud = go.AddComponent<WorldHUD>();
            hud._root = go.GetComponent<RectTransform>();
            UITheme.FullStretch(hud._root);
            hud._layer = UITheme.NewUIObject("Content", hud._root).GetComponent<RectTransform>();
            UITheme.FullStretch(hud._layer);
            hud.Build();
            return hud;
        }

        private void Build()
        {
            // 底部互动提示
            _promptPanel = UITheme.AddPanel("Prompt", _layer, new Color(1f, 1f, 1f, 0.92f));
            _promptPanel.raycastTarget = false;
            UITheme.SetRect(_promptPanel.rectTransform,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(-300, 60), new Vector2(300, 130));
            _promptText = UITheme.AddText("Text", _promptPanel.transform, "", 34, Color.black, TextAnchor.MiddleCenter);
            UITheme.FullStretch(_promptText.rectTransform);
            _promptPanel.gameObject.SetActive(false);

            // 左上操作提示
            _hintText = UITheme.AddText("Hint", _layer, GameLanguage.ControlsHint, 24,
                new Color(1f, 1f, 1f, 0.85f), TextAnchor.UpperLeft);
            UITheme.AddOutline(_hintText, new Color(0f, 0f, 0f, 0.8f), new Vector2(1.5f, -1.5f));
            UITheme.SetRect(_hintText.rectTransform,
                new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(24, -140), new Vector2(620, -24));

            // Toast
            var toastGO = UITheme.NewUIObject("Toast", _layer);
            var toastRT = toastGO.GetComponent<RectTransform>();
            UITheme.SetRect(toastRT,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-400, -200), new Vector2(400, -120));
            _toastGroup = toastGO.AddComponent<CanvasGroup>();
            _toastGroup.alpha = 0f;
            _toastGroup.blocksRaycasts = false;
            var toastBg = UITheme.AddImage("BG", toastGO.transform, new Color(0f, 0f, 0f, 0.6f));
            UITheme.FullStretch(toastBg.rectTransform);
            toastBg.raycastTarget = false;
            _toastText = UITheme.AddText("Text", toastGO.transform, "", 32, Color.white, TextAnchor.MiddleCenter);
            UITheme.FullStretch(_toastText.rectTransform);
        }

        public void SetPrompt(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (_promptPanel.gameObject.activeSelf != show)
                _promptPanel.gameObject.SetActive(show);
            if (show) _promptText.text = text;
        }

        public void SetVisible(bool visible)
        {
            _layer.gameObject.SetActive(visible);
        }

        public void Toast(string text, float seconds = 2.2f)
        {
            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastText.text = text;
            _toastRoutine = StartCoroutine(ToastRoutine(seconds));
        }

        private IEnumerator ToastRoutine(float seconds)
        {
            _toastGroup.alpha = 1f;
            yield return new WaitForSecondsRealtime(seconds);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                _toastGroup.alpha = 1f - t / 0.4f;
                yield return null;
            }
            _toastGroup.alpha = 0f;
        }

        // ---------- 暂停菜单 ----------

        public bool PauseShown => _pauseLayer != null;

        public void ShowPause(UnityEngine.Events.UnityAction onResume, UnityEngine.Events.UnityAction onSave,
            UnityEngine.Events.UnityAction onMainMenu)
        {
            if (_pauseLayer != null) return;
            var go = UITheme.NewUIObject("PauseMenu", _layer);
            _pauseLayer = go.GetComponent<RectTransform>();
            UITheme.FullStretch(_pauseLayer);

            var dim = UITheme.AddImage("Dim", _pauseLayer, new Color(0f, 0f, 0f, 0.6f));
            UITheme.FullStretch(dim.rectTransform);

            var title = UITheme.AddText("Title", _pauseLayer, GameLanguage.Paused, 52, Color.white, TextAnchor.MiddleCenter);
            UITheme.SetRect(title.rectTransform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-300, 150), new Vector2(300, 250));

            float y = 60f;
            foreach (var pair in new (string, UnityEngine.Events.UnityAction)[]
                     {
                         (GameLanguage.Resume, onResume),
                         (GameLanguage.SaveLabel, onSave),
                         (GameLanguage.BackToMainMenu, onMainMenu),
                     })
            {
                var btn = UITheme.AddButton("Btn", _pauseLayer, pair.Item1, 34, pair.Item2);
                UITheme.SetRect(btn.GetComponent<RectTransform>(),
                    new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-200, y - 40), new Vector2(200, y + 40));
                y -= 100f;
            }
        }

        public void HidePause()
        {
            if (_pauseLayer == null) return;
            Destroy(_pauseLayer.gameObject);
            _pauseLayer = null;
        }
    }
}
