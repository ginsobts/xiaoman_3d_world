using UnityEngine;

namespace VN.World
{
    /// <summary>
    /// 按 E 后播放一段 AVG 对话（用现有 VNView）。
    /// 对话从 storyFile 的 startNode 开始；剧本内可用 conditionalNexts / setVariables
    /// 读写与世界共享的变量（例如第二次对话走不同分支）。
    /// </summary>
    public class DialogueInteractable : Interactable
    {
        [Header("对话")]
        [Tooltip("剧本文件名，如 world.json；空 = GameConfig.WorldStoryFile")]
        public string storyFile = "";

        [Tooltip("起始节点 id；空 = 剧本 startNode")]
        public string startNode = "";

        [Tooltip("提示动词用「说话」而不是「调查」")]
        public bool isCharacter = true;

        [Header("条件（可选）")]
        [Tooltip("非空时：该变量为真才可交互")]
        public string requiredVariable = "";

        [Tooltip("非空时：该变量为真则不可交互（例如只能触发一次）")]
        public string blockedByVariable = "";

        [Tooltip("对话结束后把这个变量设为 1（空 = 不设置）")]
        public string setVariableAfter = "";

        [Tooltip("对话时让角色转向玩家")]
        public bool facePlayer = true;

        public override string Prompt
        {
            get
            {
                string n = string.IsNullOrEmpty(displayName) ? name : displayName;
                return isCharacter ? GameLanguage.TalkPrompt(n) : GameLanguage.InteractPrompt(n);
            }
        }

        public override bool CanInteract()
        {
            if (!base.CanInteract()) return false;
            if (!string.IsNullOrEmpty(requiredVariable) && !WorldState.Is(requiredVariable)) return false;
            if (!string.IsNullOrEmpty(blockedByVariable) && WorldState.Is(blockedByVariable)) return false;
            return true;
        }

        public override void Interact(WorldController world)
        {
            if (facePlayer && world.Player != null)
            {
                Vector3 to = world.Player.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            }

            world.StartDialogue(storyFile, startNode, () =>
            {
                if (!string.IsNullOrEmpty(setVariableAfter))
                    WorldState.SetFlag(setVariableAfter);
            });
        }
    }
}
