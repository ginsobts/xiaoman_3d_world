using System.Collections.Generic;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 可交互物体基类。玩家靠近并按 E 时调用 Interact。
    /// 子类只需实现 Interact，并按需覆盖 Prompt / CanInteract。
    /// 物体需要有 Collider（可以是 Trigger），否则检测不到。
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [Tooltip("显示在提示里的名字，如：小满 / 木牌 / 门")]
        public string displayName = "";

        [Tooltip("可交互距离（米）")]
        public float interactRadius = 2.5f;

        [Tooltip("高亮时颜色变亮的程度")]
        public float highlightBoost = 0.35f;

        /// <summary>底部提示文字，如「[E] 和小满说话」。</summary>
        public virtual string Prompt => GameLanguage.InteractPrompt(string.IsNullOrEmpty(displayName) ? name : displayName);

        public virtual bool CanInteract() => enabled && gameObject.activeInHierarchy;

        public abstract void Interact(WorldController world);

        /// <summary>
        /// 世界开始（新游戏 / 读档）后由 WorldController 调用一次，此时 WorldState 已就绪。
        /// 需要根据变量恢复状态的物体（已拾取、已开门）在这里处理，而不是在 Start 里。
        /// </summary>
        public virtual void OnWorldStart() { }

        // ---------- 高亮 ----------
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private readonly List<Color> _baseColors = new List<Color>();
        private bool _highlighted;

        public void SetHighlighted(bool on)
        {
            if (_highlighted == on) return;
            _highlighted = on;

            if (_renderers.Count == 0)
            {
                foreach (var r in GetComponentsInChildren<Renderer>())
                {
                    if (r is ParticleSystemRenderer) continue;
                    if (r.material == null || !r.material.HasProperty("_Color")) continue;
                    _renderers.Add(r);
                    _baseColors.Add(r.material.color);
                }
            }

            for (int i = 0; i < _renderers.Count; i++)
            {
                if (_renderers[i] == null) continue;
                Color c = _baseColors[i];
                _renderers[i].material.color = on
                    ? Color.Lerp(c, Color.white, highlightBoost)
                    : c;
            }
        }

        protected virtual void OnDisable() => SetHighlighted(false);
    }
}
