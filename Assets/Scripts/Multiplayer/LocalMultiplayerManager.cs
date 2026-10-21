using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VN.World;

namespace VN.Multiplayer
{
    /// <summary>
    /// 一台设备上的本地玩家与远程玩家：
    /// P1 = WASD；Enter 加入方向键 P2；手柄 Start 加入；最多四名本地玩家。
    /// 使用共享第三人称相机，远程角色由快照插值驱动。
    /// </summary>
    public class LocalMultiplayerManager : MonoBehaviour
    {
        public IReadOnlyList<MultiplayerAvatar> LocalPlayers => _localPlayers;
        public IReadOnlyDictionary<int, MultiplayerAvatar> RemotePlayers => _remotePlayers;
        public MultiplayerAvatar PrimaryPlayer => _localPlayers.Count > 0 ? _localPlayers[0] : null;

        private readonly List<MultiplayerAvatar> _localPlayers = new List<MultiplayerAvatar>();
        private readonly Dictionary<int, MultiplayerAvatar> _remotePlayers = new Dictionary<int, MultiplayerAvatar>();
        private readonly HashSet<LocalInputScheme> _usedSchemes = new HashSet<LocalInputScheme>();

        private WorldController _world;
        private MultiplayerSession _session;
        private Camera _camera;
        private GroupCameraTarget _cameraTarget;
        private Vector3 _spawn;
        private float _spawnYaw;
        private bool _gameplayEnabled = true;

        public void Init(WorldController world, MultiplayerSession session, Camera camera,
            GameObject firstPlayer, Vector3 spawn, float yaw)
        {
            _world = world;
            _session = session;
            _camera = camera;
            _spawn = spawn;
            _spawnYaw = yaw;

            ConfigureLocalPlayer(firstPlayer, LocalInputScheme.KeyboardWASD, 0);
            _session.MessageReceived += OnNetworkMessage;
            _session.SessionChanged += OnSessionChanged;

            var targetGO = new GameObject("LocalPlayersCameraTarget");
            targetGO.transform.SetParent(transform, false);
            _cameraTarget = targetGO.AddComponent<GroupCameraTarget>();
            RefreshCameraTarget();
        }

        private void Update()
        {
            if (_world == null) return;

            // 本地加入不依赖大厅，游玩中随时加入。
            if (_gameplayEnabled && _localPlayers.Count < 4)
            {
                if (Input.GetKeyDown(KeyCode.Return) && !_usedSchemes.Contains(LocalInputScheme.KeyboardArrows))
                    AddLocalPlayer(LocalInputScheme.KeyboardArrows);

                for (int joy = 1; joy <= 4 && _localPlayers.Count < 4; joy++)
                {
                    var scheme = (LocalInputScheme)((int)LocalInputScheme.Gamepad1 + joy - 1);
                    if (!_usedSchemes.Contains(scheme) && PlayerInputSlot.GetGamepadButtonDown(joy, 7))
                        AddLocalPlayer(scheme);
                }
            }

            if (!_gameplayEnabled || !_world.IsExploring) return;
            foreach (var player in _localPlayers)
            {
                if (player == null || player.InputSlot == null) continue;
                if (player.InputSlot.InteractDown && player.Detector != null && player.Detector.Current != null)
                    player.Detector.Current.Interact(_world);
                if (player.InputSlot.InstrumentDown)
                    TryPlayNearbyInstrument(player);
            }
        }

        public MultiplayerAvatar AddLocalPlayer(LocalInputScheme scheme)
        {
            if (_localPlayers.Count >= 4 || _usedSchemes.Contains(scheme)) return null;
            int slot = Enumerable.Range(0, 4).First(index => _localPlayers.All(p => p.localSlot != index));
            Vector3 offset = new Vector3((slot % 2) * 1.3f - 0.65f, 0f, (slot / 2) * 1.3f);
            GameObject playerGO = LevelBuilder.BuildPlayer(_spawn + offset, _spawnYaw);
            var avatar = ConfigureLocalPlayer(playerGO, scheme, slot);
            _world.Hud?.Toast("P" + (slot + 1) + " joined · " + avatar.InputSlot.DisplayName);
            RefreshCameraTarget();
            return avatar;
        }

        private MultiplayerAvatar ConfigureLocalPlayer(GameObject go, LocalInputScheme scheme, int slot)
        {
            var avatar = go.AddComponent<MultiplayerAvatar>();
            avatar.Configure(_session.LocalPeerId * 10 + slot, _session.LocalPeerId, slot,
                true, scheme, "P" + (slot + 1), slot);
            if (avatar.Controller != null && _camera != null)
                avatar.Controller.CameraReference = _camera.transform;
            _localPlayers.Add(avatar);
            _usedSchemes.Add(scheme);
            _session.RegisterLocalPlayer(avatar);
            return avatar;
        }

        public void SetGameplayEnabled(bool enabled)
        {
            _gameplayEnabled = enabled;
            foreach (var player in _localPlayers)
            {
                if (player == null) continue;
                if (player.Controller != null)
                {
                    player.Controller.InputEnabled = enabled;
                    if (!enabled) player.Controller.Halt();
                }
                if (player.InputSlot != null) player.InputSlot.enabledForGameplay = enabled;
                if (player.Detector != null)
                {
                    player.Detector.enabled = enabled;
                    if (!enabled) player.Detector.Clear();
                }
            }
        }

        public string CurrentPrompt
        {
            get
            {
                foreach (var player in _localPlayers)
                    if (player != null && player.Detector != null && player.Detector.Current != null)
                        return "P" + (player.localSlot + 1) + " · " + player.Detector.Current.Prompt;
                return "";
            }
        }

        public Transform CameraTarget => _cameraTarget != null ? _cameraTarget.transform : PrimaryPlayer?.transform;

        private void RefreshCameraTarget()
        {
            _cameraTarget?.SetTargets(_localPlayers);
        }

        private void TryPlayNearbyInstrument(MultiplayerAvatar player)
        {
            var instruments = FindObjectsOfType<PlayableInstrument>();
            PlayableInstrument nearest = null;
            float best = 4f;
            foreach (var instrument in instruments)
            {
                float distance = Vector3.Distance(player.transform.position, instrument.transform.position);
                if (distance < best) { best = distance; nearest = instrument; }
            }
            nearest?.PlayBy(player);
        }

        private void OnNetworkMessage(MultiplayerMessage message)
        {
            if (message == null) return;
            switch (message.type)
            {
                case MultiplayerProtocol.PlayerJoin:
                    if (_localPlayers.Any(p => p.playerId == message.playerId)) return;
                    GetOrCreateRemote(message);
                    break;
                case MultiplayerProtocol.Snapshot:
                    if (_localPlayers.Any(p => p.playerId == message.playerId)) return;
                    GetOrCreateRemote(message).SetRemoteState(message.Position, message.yaw);
                    break;
                case MultiplayerProtocol.PlayerLeave:
                    if (_remotePlayers.TryGetValue(message.playerId, out var avatar))
                    {
                        Destroy(avatar.gameObject);
                        _remotePlayers.Remove(message.playerId);
                    }
                    break;
            }
        }

        private MultiplayerAvatar GetOrCreateRemote(MultiplayerMessage message)
        {
            if (_remotePlayers.TryGetValue(message.playerId, out var existing)) return existing;
            var go = LevelBuilder.BuildPlayer(message.Position, message.yaw);
            var avatar = go.AddComponent<MultiplayerAvatar>();
            avatar.Configure(message.playerId, message.peerId, message.localSlot, false,
                LocalInputScheme.Remote, message.name, message.colorIndex);
            avatar.SetRemoteState(message.Position, message.yaw);
            _remotePlayers[message.playerId] = avatar;
            return avatar;
        }

        private void OnSessionChanged()
        {
            if (_session.Mode != SessionMode.Offline && _session.Mode != SessionMode.Discovering) return;
            foreach (var remote in _remotePlayers.Values)
                if (remote != null) Destroy(remote.gameObject);
            _remotePlayers.Clear();
        }

        private void OnDestroy()
        {
            if (_session != null)
            {
                _session.MessageReceived -= OnNetworkMessage;
                _session.SessionChanged -= OnSessionChanged;
            }
            foreach (var player in _localPlayers)
                if (player != null)
                {
                    _session?.UnregisterLocalPlayer(player);
                    Destroy(player.gameObject);
                }
            foreach (var remote in _remotePlayers.Values)
                if (remote != null) Destroy(remote.gameObject);
        }
    }
}
