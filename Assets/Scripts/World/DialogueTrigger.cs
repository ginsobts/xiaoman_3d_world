using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 区域触发：玩家走进 Trigger Collider 时自动播放一段对话（如进入新区域的旁白）。
    /// once = true 时记录 trigger_&lt;triggerId&gt; = 1，读档后不会重复触发。
    /// 既响应 OnTriggerEnter，也在 Update 里用包围盒兜底（玩家出生在区域内时也能触发）。
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DialogueTrigger : MonoBehaviour
    {
        public string triggerId = "trigger";
        public string storyFile = "";
        public string startNode = "";
        public bool once = true;

        [Tooltip("非空时：该变量为真才触发")]
        public string requiredVariable = "";

        public string FiredVariable => "trigger_" + triggerId;

        private Collider _collider;
        private bool _playerInside;

        private void Reset()
        {
            var col = GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
        }

        private void Awake()
        {
            _collider = GetComponent<Collider>();
        }

        private void Update()
        {
            var world = WorldController.Instance;
            if (world == null || world.Player == null || _collider == null) return;

            bool inside = _collider.bounds.Contains(world.Player.transform.position + Vector3.up * 0.5f);
            if (inside && !_playerInside) TryFire(world);
            _playerInside = inside;
        }

        private void OnTriggerEnter(Collider other)
        {
            var world = WorldController.Instance;
            if (world == null || world.Player == null) return;
            if (!other.transform.IsChildOf(world.Player.transform)) return;
            _playerInside = true;
            TryFire(world);
        }

        private void OnTriggerExit(Collider other)
        {
            var world = WorldController.Instance;
            if (world == null || world.Player == null) return;
            if (other.transform.IsChildOf(world.Player.transform)) _playerInside = false;
        }

        private void TryFire(WorldController world)
        {
            if (!world.IsExploring) return;
            if (once && WorldState.Is(FiredVariable)) return;
            if (!string.IsNullOrEmpty(requiredVariable) && !WorldState.Is(requiredVariable)) return;

            if (once) WorldState.SetFlag(FiredVariable);
            world.StartDialogue(storyFile, startNode);
        }
    }
}
