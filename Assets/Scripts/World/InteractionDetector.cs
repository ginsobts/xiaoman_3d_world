using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 挂在玩家身上：每帧找出附近最合适的 Interactable（距离近、且大致在身前），
    /// 负责高亮切换。按键触发由 WorldController 处理。
    /// </summary>
    public class InteractionDetector : MonoBehaviour
    {
        public float searchRadius = 3.5f;
        public LayerMask mask = ~0;

        [Tooltip("只考虑身前多大角度范围内的物体（dot 阈值，-1 表示全方向）")]
        public float facingDotThreshold = -0.3f;

        public Interactable Current { get; private set; }

        private readonly Collider[] _hits = new Collider[32];

        public void Clear()
        {
            if (Current != null) Current.SetHighlighted(false);
            Current = null;
        }

        private void Update()
        {
            Interactable best = null;
            float bestScore = float.MaxValue;

            Vector3 origin = transform.position + Vector3.up * 0.9f;
            int n = Physics.OverlapSphereNonAlloc(origin, searchRadius, _hits, mask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < n; i++)
            {
                var col = _hits[i];
                if (col == null) continue;
                var it = col.GetComponentInParent<Interactable>();
                if (it == null || !it.CanInteract()) continue;
                if (it.transform.IsChildOf(transform)) continue;

                Vector3 closest = col.ClosestPoint(origin);
                Vector3 to = closest - origin;
                to.y = 0f;
                float dist = to.magnitude;
                if (dist > it.interactRadius) continue;

                float dot = dist > 0.01f ? Vector3.Dot(transform.forward, to / dist) : 1f;
                if (dot < facingDotThreshold) continue;

                // 越近、越正前方分数越低
                float score = dist - dot * 0.5f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = it;
                }
            }

            if (best != Current)
            {
                if (Current != null) Current.SetHighlighted(false);
                Current = best;
                if (Current != null) Current.SetHighlighted(true);
            }
        }

        private void OnDisable() => Clear();
    }
}
