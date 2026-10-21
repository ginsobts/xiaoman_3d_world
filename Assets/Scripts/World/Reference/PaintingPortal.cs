using UnityEngine;
using VN.Multiplayer;

namespace VN.World
{
    /// <summary>
    /// "走进画里"的画框传送门：按 E 把所有本地玩家一起传送到目标点，并立即切换到目标光照配方。
    /// 联机时每台机器各自传送自己的本地玩家，远程玩家位置由快照同步。
    /// </summary>
    public class PaintingPortal : Interactable
    {
        public Vector3 targetPosition;
        public float targetYaw;
        public string lightingId = "village";
        public string destinationTitle = "";

        public override string Prompt => GameLanguage.IsEnglish
            ? "[E] Step into " + (string.IsNullOrEmpty(destinationTitle) ? displayName : destinationTitle)
            : "[E] 走进「" + (string.IsNullOrEmpty(destinationTitle) ? displayName : destinationTitle) + "」";

        public override void Interact(WorldController world)
        {
            int index = 0;
            if (world.LocalMultiplayer != null)
            {
                foreach (var avatar in world.LocalMultiplayer.LocalPlayers)
                {
                    if (avatar == null || avatar.Controller == null) continue;
                    Vector3 offset = Quaternion.Euler(0f, targetYaw, 0f) * new Vector3((index % 2) * 1.4f - 0.7f * (index > 0 ? 1 : 0), 0f, -(index / 2) * 1.3f);
                    avatar.Controller.Teleport(targetPosition + offset, targetYaw);
                    index++;
                }
            }
            else if (world.Player != null)
            {
                world.Player.Teleport(targetPosition, targetYaw);
            }

            var group = world.LocalMultiplayer != null ? world.LocalMultiplayer.CameraTarget : null;
            var groupTarget = group != null ? group.GetComponent<GroupCameraTarget>() : null;
            groupTarget?.Snap();
            if (world.CameraRig != null) world.CameraRig.SnapBehind(targetYaw);

            LightingDirector.Instance?.SetImmediate(ReferenceSceneLibrary.PresetById(lightingId));
            FairytaleVFX.Spawn("DoorSpiral", targetPosition + Vector3.up * 1.2f, 3f, 1.2f);
            world.Hud?.Toast(string.IsNullOrEmpty(destinationTitle) ? displayName : destinationTitle, 2.4f);
        }
    }
}
