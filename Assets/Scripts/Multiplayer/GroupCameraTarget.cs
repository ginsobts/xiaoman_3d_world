using System.Collections.Generic;
using UnityEngine;

namespace VN.Multiplayer
{
    /// <summary>共享屏本地多人相机中心；所有本地玩家都保持在同一画面附近。</summary>
    public class GroupCameraTarget : MonoBehaviour
    {
        private readonly List<Transform> _targets = new List<Transform>();

        public void SetTargets(IEnumerable<MultiplayerAvatar> players)
        {
            _targets.Clear();
            foreach (var player in players)
                if (player != null && player.isLocal)
                    _targets.Add(player.transform);
            Snap();
        }

        public void Snap()
        {
            if (_targets.Count == 0) return;
            Vector3 center = Vector3.zero;
            foreach (var target in _targets) center += target.position;
            transform.position = center / _targets.Count;
        }

        private void LateUpdate()
        {
            if (_targets.Count == 0) return;
            Vector3 center = Vector3.zero;
            int count = 0;
            foreach (var target in _targets)
            {
                if (target == null) continue;
                center += target.position;
                count++;
            }
            if (count > 0)
                transform.position = Vector3.Lerp(transform.position, center / count,
                    1f - Mathf.Exp(-10f * Time.deltaTime));
        }
    }
}
