using UnityEngine;
using UnityEngine.UI;

namespace VN.World
{
    /// <summary>
    /// 头顶名字牌：World Space Canvas + Text，始终面向相机。
    /// 用 UI Text（而不是 TextMesh）是为了让动态中文字体正常刷新。
    /// </summary>
    public class WorldLabel : MonoBehaviour
    {
        public string labelText = "";
        public float heightOffset = 2.2f;
        public float scale = 0.012f;
        public float fadeDistance = 18f;

        private Text _text;
        private CanvasGroup _group;
        private Transform _canvasRoot;

        public static WorldLabel Attach(GameObject target, string text, float height = 2.2f)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(target.transform, false);
            var label = go.AddComponent<WorldLabel>();
            label.labelText = text;
            label.heightOffset = height;
            // 编辑器下先搭一份预览；运行时 Start 会重新搭（动态字体不能随场景保存）
            if (!Application.isPlaying) label.Build(text);
            return label;
        }

        public void SetText(string text)
        {
            labelText = text;
            if (_text != null) _text.text = text;
        }

        private void Start()
        {
            if (!Application.isPlaying) return;
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);
            Build(labelText);
        }

        private void Build(string text)
        {
            var canvasGO = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            canvasGO.transform.SetParent(transform, false);
            _canvasRoot = canvasGO.transform;
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = canvasGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(600, 120);
            rt.localScale = Vector3.one * scale;
            _group = canvasGO.GetComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            var bg = UITheme.AddImage("BG", canvasGO.transform, new Color(0f, 0f, 0f, 0.45f));
            UITheme.SetRect(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-150, -40), new Vector2(150, 40));
            bg.raycastTarget = false;

            _text = UITheme.AddText("Text", bg.transform, text, 44, Color.white, TextAnchor.MiddleCenter);
            UITheme.FullStretch(_text.rectTransform);
            _text.raycastTarget = false;
        }

        private void LateUpdate()
        {
            if (_canvasRoot == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            transform.position = transform.parent != null
                ? transform.parent.position + Vector3.up * heightOffset
                : transform.position;

            // 面向相机（只绕 Y，保持直立）
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
                _canvasRoot.rotation = Quaternion.LookRotation(-toCam, Vector3.up);

            if (_group != null)
            {
                float d = Vector3.Distance(cam.transform.position, transform.position);
                _group.alpha = Mathf.Clamp01(1f - (d - fadeDistance * 0.6f) / (fadeDistance * 0.4f));
            }
        }
    }
}
