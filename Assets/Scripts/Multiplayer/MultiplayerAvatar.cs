using UnityEngine;
using VN.World;

namespace VN.Multiplayer
{
    /// <summary>本地/远程玩家的统一身份与远程插值表现。</summary>
    public class MultiplayerAvatar : MonoBehaviour
    {
        private static readonly Color[] PlayerColors =
        {
            new Color(0.39f, 0.63f, 0.86f),
            new Color(0.91f, 0.48f, 0.56f),
            new Color(0.48f, 0.72f, 0.45f),
            new Color(0.86f, 0.68f, 0.28f),
            new Color(0.63f, 0.48f, 0.82f),
            new Color(0.35f, 0.76f, 0.76f),
        };

        public int playerId;
        public int peerId;
        public int localSlot;
        public int colorIndex;
        public bool isLocal;
        public string displayName = "Player";

        private Vector3 _targetPosition;
        private float _targetYaw;
        private bool _hasRemoteState;
        private WorldLabel _label;

        public PlayerController Controller { get; private set; }
        public PlayerInputSlot InputSlot { get; private set; }
        public InteractionDetector Detector { get; private set; }

        public void Configure(int id, int ownerPeer, int slot, bool local,
            LocalInputScheme scheme, string playerName, int color)
        {
            playerId = id;
            peerId = ownerPeer;
            localSlot = slot;
            isLocal = local;
            displayName = string.IsNullOrEmpty(playerName) ? "Player " + (slot + 1) : playerName;
            colorIndex = Mathf.Abs(color) % PlayerColors.Length;
            name = (local ? "LocalPlayer_" : "RemotePlayer_") + playerId;

            Controller = GetComponent<PlayerController>();
            Detector = GetComponent<InteractionDetector>();
            InputSlot = GetComponent<PlayerInputSlot>();
            if (InputSlot == null) InputSlot = gameObject.AddComponent<PlayerInputSlot>();
            InputSlot.localSlot = slot;
            InputSlot.scheme = local ? scheme : LocalInputScheme.Remote;
            InputSlot.enabledForGameplay = local;

            if (Controller != null) Controller.enabled = local;
            if (Detector != null) Detector.enabled = local;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = local;

            AddColorMarker(PlayerColors[colorIndex]);
            _label = WorldLabel.Attach(gameObject, displayName, 2.35f);
            if (!local)
            {
                _targetPosition = transform.position;
                _targetYaw = transform.eulerAngles.y;
            }
        }

        public void SetRemoteState(Vector3 position, float yaw)
        {
            _targetPosition = position;
            _targetYaw = yaw;
            if (!_hasRemoteState)
            {
                transform.position = position;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                _hasRemoteState = true;
            }
        }

        private void Update()
        {
            if (isLocal || !_hasRemoteState) return;
            float positionBlend = 1f - Mathf.Exp(-14f * Time.deltaTime);
            float rotationBlend = 1f - Mathf.Exp(-18f * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _targetPosition, positionBlend);
            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.Euler(0f, _targetYaw, 0f), rotationBlend);
        }

        private void AddColorMarker(Color color)
        {
            var marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = "PlayerColor";
            marker.transform.SetParent(transform, false);
            marker.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            marker.transform.localScale = new Vector3(0.58f, 0.025f, 0.58f);
            marker.GetComponent<Renderer>().sharedMaterial =
                FairytaleArt.Material("PlayerColor" + colorIndex, color, null, true);
            var col = marker.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }
    }
}
