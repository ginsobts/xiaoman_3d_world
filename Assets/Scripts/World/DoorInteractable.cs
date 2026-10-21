using System.Collections;
using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 门：需要某个变量为真才能打开；否则播放「锁着」的对话节点。
    /// 打开后记录 door_open_&lt;doorId&gt; = 1，读档时保持打开。
    /// </summary>
    public class DoorInteractable : Interactable
    {
        public string doorId = "door";

        [Tooltip("需要的变量（如 has_key）；空 = 无需钥匙")]
        public string requiredVariable = "";

        [Tooltip("锁着时播放的对话节点（可选）")]
        public string lockedDialogueNode = "";
        public string storyFile = "";

        [Header("开门表现")]
        [Tooltip("绕 Y 轴旋转多少度；0 = 直接向上滑开")]
        public float openAngle = 100f;
        public float openDuration = 0.8f;

        [Tooltip("实际转动/移动的部件；空 = 自己")]
        public Transform doorPanel;

        public string OpenedVariable => "door_open_" + doorId;
        public bool IsOpen { get; private set; }

        public override string Prompt => GameLanguage.OpenDoorPrompt(string.IsNullOrEmpty(displayName) ? doorId : displayName);

        public override bool CanInteract() => base.CanInteract() && !IsOpen;

        public override void OnWorldStart()
        {
            Transform panel = doorPanel != null ? doorPanel : transform;
            CachePose(panel);
            if (WorldState.Is(OpenedVariable))
                Open(instant: true);
            else
                Close(panel);
        }

        private void Close(Transform panel)
        {
            IsOpen = false;
            StopAllCoroutines();
            panel.localRotation = _closedRot;
            panel.localPosition = _closedPos;
            foreach (var c in panel.GetComponentsInChildren<Collider>())
                c.enabled = true;
        }

        public override void Interact(WorldController world)
        {
            if (!string.IsNullOrEmpty(requiredVariable) && !WorldState.Is(requiredVariable))
            {
                if (!string.IsNullOrEmpty(lockedDialogueNode))
                    world.StartDialogue(storyFile, lockedDialogueNode);
                else
                    world.Hud.Toast(GameLanguage.DoorLocked);
                return;
            }

            world.Hud.Toast(GameLanguage.DoorOpened);
            WorldState.SetFlag(OpenedVariable);
            FairytaleVFX.Spawn("DoorSpiral", transform.position + Vector3.up * 1.35f, 3f, 1.4f);
            Open(instant: false);
        }

        private void Open(bool instant)
        {
            IsOpen = true;
            SetHighlighted(false);
            Transform panel = doorPanel != null ? doorPanel : transform;

            // 开门后不再挡路
            foreach (var c in panel.GetComponentsInChildren<Collider>())
                c.enabled = false;

            if (instant || !gameObject.activeInHierarchy)
            {
                ApplyOpenPose(panel, 1f);
                return;
            }
            StartCoroutine(OpenRoutine(panel));
        }

        private Quaternion _closedRot;
        private Vector3 _closedPos;
        private bool _poseCached;

        private void CachePose(Transform panel)
        {
            if (_poseCached) return;
            _closedRot = panel.localRotation;
            _closedPos = panel.localPosition;
            _poseCached = true;
        }

        private void ApplyOpenPose(Transform panel, float t)
        {
            CachePose(panel);
            if (Mathf.Abs(openAngle) > 0.01f)
                panel.localRotation = _closedRot * Quaternion.Euler(0f, openAngle * t, 0f);
            else
                panel.localPosition = _closedPos + Vector3.up * (3f * t);
        }

        private IEnumerator OpenRoutine(Transform panel)
        {
            float t = 0f;
            while (t < openDuration)
            {
                t += Time.deltaTime;
                ApplyOpenPose(panel, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / openDuration)));
                yield return null;
            }
            ApplyOpenPose(panel, 1f);
        }
    }
}
