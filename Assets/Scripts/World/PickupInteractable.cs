using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 可拾取物：按 E 后设置变量 has_&lt;itemId&gt; = 1，弹提示，然后消失。
    /// 读档时若已拥有则直接隐藏。
    /// </summary>
    public class PickupInteractable : Interactable
    {
        [Tooltip("道具 id，对应变量 has_<itemId>")]
        public string itemId = "item";

        [Tooltip("拾取后播放的对话节点（可选，空=只弹提示）")]
        public string dialogueNode = "";
        public string storyFile = "";

        [Header("表现")]
        public bool spin = true;
        public float spinSpeed = 90f;
        public float bobAmplitude = 0.15f;

        public string VariableName => "has_" + itemId;

        private Vector3 _basePos;
        private bool _basePosCached;

        public override string Prompt => GameLanguage.PickupPrompt(string.IsNullOrEmpty(displayName) ? itemId : displayName);

        private void Awake()
        {
            CacheBasePos();
        }

        private void CacheBasePos()
        {
            if (_basePosCached) return;
            _basePos = transform.position;
            _basePosCached = true;
        }

        public override void OnWorldStart()
        {
            CacheBasePos();
            // 已拥有则隐藏；新游戏（变量被清空）则重新出现
            gameObject.SetActive(!WorldState.Is(VariableName));
        }

        private void Update()
        {
            if (!spin) return;
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
            transform.position = _basePos + Vector3.up * (Mathf.Sin(Time.time * 2f) * bobAmplitude);
        }

        public override void Interact(WorldController world)
        {
            WorldState.SetFlag(VariableName);
            string label = string.IsNullOrEmpty(displayName) ? itemId : displayName;
            world.Hud.Toast(GameLanguage.PickedUp(label));
            SetHighlighted(false);
            gameObject.SetActive(false);

            if (!string.IsNullOrEmpty(dialogueNode))
                world.StartDialogue(storyFile, dialogueNode);
        }
    }
}
