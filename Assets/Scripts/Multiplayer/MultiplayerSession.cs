using System;
using System.Collections.Generic;
using UnityEngine;

namespace VN.Multiplayer
{
    public enum SessionMode { Offline, Discovering, Host, Client }

    /// <summary>
    /// 主机式空间会话。Host 是权威转发者，但也是普通玩家，不需要独立服务器。
    /// 本地多名玩家共用一个 peer，每个玩家 id = peerId * 10 + localSlot。
    /// </summary>
    public class MultiplayerSession : MonoBehaviour
    {
        public static MultiplayerSession Instance { get; private set; }

        public SessionMode Mode { get; private set; } = SessionMode.Offline;
        public int LocalPeerId { get; private set; } = 1;
        public int Port { get; private set; } = MultiplayerProtocol.DefaultPort;
        public string StatusText { get; private set; } = "Offline";
        public bool IsConnected => Mode == SessionMode.Host || Mode == SessionMode.Client;

        public event Action<MultiplayerMessage> MessageReceived;
        public event Action<string, string> HostDiscovered; // address, room name
        public event Action SessionChanged;

        private IRealtimeTransport _transport;
        private readonly Dictionary<NetEndpoint, int> _peers = new Dictionary<NetEndpoint, int>();
        private readonly Dictionary<int, NetEndpoint> _peerEndpoints = new Dictionary<int, NetEndpoint>();
        private readonly Dictionary<int, float> _peerLastSeen = new Dictionary<int, float>();
        private readonly List<MultiplayerAvatar> _localPlayers = new List<MultiplayerAvatar>();
        private readonly HashSet<int> _seenEvents = new HashSet<int>();
        private int _nextPeerId = 2;
        private int _sequence;
        private int _eventSequence;
        private float _snapshotTimer;
        private float _helloTimer;
        private float _heartbeatTimer;
        private float _lastServerPacketTime;
        private string _pendingName = "Player";
        private NetEndpoint _serverEndpoint;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _transport = new DirectUdpTransport();
        }

        private void Update()
        {
            _transport?.Poll(OnDatagram);
            UpdateHeartbeat();
            if (Mode == SessionMode.Client && LocalPeerId == 0)
            {
                _helloTimer -= Time.unscaledDeltaTime;
                if (_helloTimer <= 0f)
                {
                    _helloTimer = 1f;
                    SendToServer(new MultiplayerMessage
                    {
                        type = MultiplayerProtocol.Hello,
                        name = _pendingName
                    });
                }
            }

            if (!IsConnected) return;
            _snapshotTimer -= Time.unscaledDeltaTime;
            if (_snapshotTimer <= 0f)
            {
                _snapshotTimer = MultiplayerProtocol.SnapshotInterval;
                foreach (var player in _localPlayers)
                {
                    if (player == null) continue;
                    SendOrBroadcast(new MultiplayerMessage
                    {
                        type = MultiplayerProtocol.Snapshot,
                        playerId = player.playerId,
                        peerId = LocalPeerId,
                        localSlot = player.localSlot,
                        Position = player.transform.position,
                        yaw = player.transform.eulerAngles.y,
                        sequence = ++_sequence
                    }, applyLocally: false);
                }
            }
        }

        public void RegisterLocalPlayer(MultiplayerAvatar player)
        {
            if (player == null || _localPlayers.Contains(player)) return;
            _localPlayers.Add(player);
            if (Mode == SessionMode.Host || (Mode == SessionMode.Client && LocalPeerId > 0))
                AnnouncePlayer(player);
        }

        public void UnregisterLocalPlayer(MultiplayerAvatar player)
        {
            if (player == null || !_localPlayers.Remove(player)) return;
            SendOrBroadcast(new MultiplayerMessage
            {
                type = MultiplayerProtocol.PlayerLeave,
                playerId = player.playerId,
                peerId = LocalPeerId,
                eventId = NextEventId()
            }, true);
        }

        public void StartHost(int port, string roomName)
        {
            StopSession();
            try
            {
                Port = Mathf.Clamp(port, 1024, 65535);
                _pendingName = string.IsNullOrEmpty(roomName) ? "Xiaoman Space" : roomName;
                _transport.StartHost(Port);
                Mode = SessionMode.Host;
                LocalPeerId = 1;
                StatusText = "Host · UDP " + Port;
                ReassignLocalPlayerIds();
                foreach (var player in _localPlayers) AnnouncePlayer(player);
                SessionChanged?.Invoke();
            }
            catch (Exception e)
            {
                StatusText = "Host failed: " + e.Message;
                Mode = SessionMode.Offline;
                SessionChanged?.Invoke();
            }
        }

        public void StartClient(string address, int port, string playerName)
        {
            StopSession();
            try
            {
                Port = Mathf.Clamp(port, 1024, 65535);
                _pendingName = string.IsNullOrEmpty(playerName) ? "Player" : playerName;
                _transport.StartClient(address, Port);
                Mode = SessionMode.Client;
                LocalPeerId = 0;
                _serverEndpoint = new NetEndpoint(address, Port);
                StatusText = "Connecting to " + address + ":" + Port;
                _helloTimer = 0f;
                _lastServerPacketTime = Time.unscaledTime;
                SessionChanged?.Invoke();
            }
            catch (Exception e)
            {
                StatusText = "Join failed: " + e.Message;
                Mode = SessionMode.Offline;
                SessionChanged?.Invoke();
            }
        }

        public void DiscoverLan(int port)
        {
            StopSession();
            Port = Mathf.Clamp(port, 1024, 65535);
            _transport.StartClient("255.255.255.255", Port);
            Mode = SessionMode.Discovering;
            LocalPeerId = 0;
            StatusText = "Searching LAN...";
            var message = new MultiplayerMessage { type = MultiplayerProtocol.Discover };
            _transport.BroadcastProbe(Port, JsonUtility.ToJson(message));
            SessionChanged?.Invoke();
        }

        public void StopSession()
        {
            if (Mode == SessionMode.Host || Mode == SessionMode.Client)
            {
                foreach (var player in _localPlayers)
                    if (player != null)
                        SendOrBroadcast(new MultiplayerMessage
                        {
                            type = MultiplayerProtocol.PlayerLeave,
                            playerId = player.playerId,
                            peerId = LocalPeerId,
                            eventId = NextEventId()
                        }, false);
            }
            _transport?.Stop();
            _peers.Clear();
            _peerEndpoints.Clear();
            _peerLastSeen.Clear();
            _seenEvents.Clear();
            _nextPeerId = 2;
            Mode = SessionMode.Offline;
            LocalPeerId = 1;
            StatusText = "Offline";
            ReassignLocalPlayerIds();
            SessionChanged?.Invoke();
        }

        public void EmitInstrument(int playerId, int instrumentId, int note)
        {
            SendOrBroadcast(new MultiplayerMessage
            {
                type = MultiplayerProtocol.Instrument,
                playerId = playerId,
                objectId = instrumentId,
                eventId = NextEventId(),
                value = note
            }, true);
        }

        public void EmitEdit(MultiplayerMessage edit)
        {
            if (edit == null) return;
            edit.eventId = NextEventId();
            if (Mode == SessionMode.Client)
            {
                edit.name = edit.type; // 请求中 name 保存最终操作类型，payload 保留物件种类。
                edit.type = MultiplayerProtocol.EditRequest;
                SendToServer(edit);
            }
            else
            {
                SendOrBroadcast(edit, true);
            }
        }

        private void OnDatagram(string json, NetEndpoint endpoint)
        {
            MultiplayerMessage message;
            try { message = JsonUtility.FromJson<MultiplayerMessage>(json); }
            catch { return; }
            if (message == null || message.protocol != MultiplayerProtocol.Version) return;

            if (Mode == SessionMode.Host)
            {
                HandleHostMessage(message, endpoint);
                return;
            }

            if (Mode == SessionMode.Discovering && message.type == MultiplayerProtocol.Offer)
            {
                HostDiscovered?.Invoke(endpoint.Address, message.name);
                return;
            }

            if (Mode != SessionMode.Client) return;
            _lastServerPacketTime = Time.unscaledTime;
            if (message.type == MultiplayerProtocol.Welcome)
            {
                LocalPeerId = message.peerId;
                _serverEndpoint = endpoint;
                StatusText = "Connected · peer " + LocalPeerId;
                ReassignLocalPlayerIds();
                foreach (var player in _localPlayers) AnnouncePlayer(player);
                SessionChanged?.Invoke();
                return;
            }
            ApplyIncoming(message);
        }

        private void HandleHostMessage(MultiplayerMessage message, NetEndpoint endpoint)
        {
            if (message.type == MultiplayerProtocol.Discover)
            {
                SendDirect(new MultiplayerMessage
                {
                    type = MultiplayerProtocol.Offer,
                    name = _pendingName,
                    peerId = 1
                }, endpoint);
                return;
            }

            if (message.type == MultiplayerProtocol.Hello)
            {
                if (!_peers.TryGetValue(endpoint, out int peerId))
                {
                    peerId = _nextPeerId++;
                    _peers[endpoint] = peerId;
                    _peerEndpoints[peerId] = endpoint;
                }
                _peerLastSeen[peerId] = Time.unscaledTime;
                SendDirect(new MultiplayerMessage
                {
                    type = MultiplayerProtocol.Welcome,
                    peerId = peerId,
                    name = _pendingName
                }, endpoint);
                // 新客户端先认识 Host 的所有本地角色。
                foreach (var player in _localPlayers)
                    SendDirect(PlayerJoinMessage(player), endpoint);
                return;
            }

            if (!_peers.TryGetValue(endpoint, out int ownerPeer)) return;
            _peerLastSeen[ownerPeer] = Time.unscaledTime;
            if (message.type == MultiplayerProtocol.Ping)
            {
                SendDirect(new MultiplayerMessage { type = MultiplayerProtocol.Pong }, endpoint);
                return;
            }
            message.peerId = ownerPeer;
            if (message.type == MultiplayerProtocol.PlayerJoin
                || message.type == MultiplayerProtocol.Snapshot
                || message.type == MultiplayerProtocol.PlayerLeave
                || message.type == MultiplayerProtocol.Instrument)
            {
                int slot = message.type == MultiplayerProtocol.PlayerJoin
                    || message.type == MultiplayerProtocol.Snapshot
                    ? message.localSlot
                    : Mathf.Abs(message.playerId) % 10;
                message.localSlot = Mathf.Clamp(slot, 0, 3);
                message.playerId = ownerPeer * 10 + message.localSlot;
            }

            if (message.type == MultiplayerProtocol.EditRequest)
            {
                message.type = message.name;
                if (message.type != MultiplayerProtocol.EditSpawn
                    && message.type != MultiplayerProtocol.EditDelete
                    && message.type != MultiplayerProtocol.EditTransform)
                    return;
                if (message.objectId == 0) message.objectId = ownerPeer * 100000 + Mathf.Abs(message.eventId % 99999);
            }

            ApplyIncoming(message);
            // 也回送请求发起者：乐器和 Host 分配 objectId 的编辑事件需要权威回包。
            // 玩家/快照回包会由 LocalMultiplayerManager 根据 playerId 自动忽略。
            Broadcast(message, default(NetEndpoint));
        }

        private void ApplyIncoming(MultiplayerMessage message)
        {
            if (message.eventId != 0 && IsDuplicateEvent(message.eventId)) return;
            MessageReceived?.Invoke(message);
        }

        private bool IsDuplicateEvent(int eventId)
        {
            if (_seenEvents.Contains(eventId)) return true;
            _seenEvents.Add(eventId);
            if (_seenEvents.Count > 4096) _seenEvents.Clear();
            return false;
        }

        private void AnnouncePlayer(MultiplayerAvatar player)
        {
            if (player == null) return;
            player.peerId = LocalPeerId;
            player.playerId = LocalPeerId * 10 + player.localSlot;
            SendOrBroadcast(PlayerJoinMessage(player), false);
        }

        private MultiplayerMessage PlayerJoinMessage(MultiplayerAvatar player)
        {
            return new MultiplayerMessage
            {
                type = MultiplayerProtocol.PlayerJoin,
                peerId = LocalPeerId,
                playerId = player.playerId,
                localSlot = player.localSlot,
                name = player.displayName,
                colorIndex = player.colorIndex,
                Position = player.transform.position,
                yaw = player.transform.eulerAngles.y,
                eventId = NextEventId()
            };
        }

        private void ReassignLocalPlayerIds()
        {
            foreach (var player in _localPlayers)
            {
                if (player == null) continue;
                player.peerId = LocalPeerId;
                player.playerId = LocalPeerId * 10 + player.localSlot;
            }
        }

        private void SendOrBroadcast(MultiplayerMessage message, bool applyLocally)
        {
            if (message == null) return;
            message.protocol = MultiplayerProtocol.Version;
            if (message.peerId == 0) message.peerId = LocalPeerId;
            if (Mode == SessionMode.Offline)
            {
                if (applyLocally) ApplyIncoming(message);
                return;
            }
            if (Mode == SessionMode.Host)
            {
                if (applyLocally) ApplyIncoming(message);
                Broadcast(message, default(NetEndpoint));
                if (message.eventId != 0) Broadcast(message, default(NetEndpoint));
            }
            else if (Mode == SessionMode.Client)
            {
                SendToServer(message);
                if (message.eventId != 0) SendToServer(message);
            }
        }

        private void UpdateHeartbeat()
        {
            if (Mode == SessionMode.Client)
            {
                if (Time.unscaledTime - _lastServerPacketTime > 10f)
                {
                    StatusText = "Connection timed out";
                    StopSession();
                    return;
                }
                _heartbeatTimer -= Time.unscaledDeltaTime;
                if (_heartbeatTimer <= 0f)
                {
                    _heartbeatTimer = 2f;
                    SendToServer(new MultiplayerMessage { type = MultiplayerProtocol.Ping });
                }
            }
            else if (Mode == SessionMode.Host)
            {
                var timedOut = new List<int>();
                foreach (var pair in _peerLastSeen)
                    if (Time.unscaledTime - pair.Value > 10f) timedOut.Add(pair.Key);
                foreach (int peerId in timedOut)
                {
                    _peerLastSeen.Remove(peerId);
                    if (_peerEndpoints.TryGetValue(peerId, out var endpoint))
                    {
                        _peerEndpoints.Remove(peerId);
                        _peers.Remove(endpoint);
                    }
                    for (int slot = 0; slot < 4; slot++)
                        SendOrBroadcast(new MultiplayerMessage
                        {
                            type = MultiplayerProtocol.PlayerLeave,
                            peerId = peerId,
                            playerId = peerId * 10 + slot,
                            eventId = NextEventId()
                        }, true);
                }
            }
        }

        private void SendToServer(MultiplayerMessage message)
        {
            message.protocol = MultiplayerProtocol.Version;
            message.peerId = LocalPeerId;
            _transport.SendToServer(JsonUtility.ToJson(message));
        }

        private void Broadcast(MultiplayerMessage message, NetEndpoint except)
        {
            string json = JsonUtility.ToJson(message);
            foreach (var endpoint in _peerEndpoints.Values)
                if (!endpoint.Equals(except))
                    _transport.Send(json, endpoint);
        }

        private void SendDirect(MultiplayerMessage message, NetEndpoint endpoint)
        {
            message.protocol = MultiplayerProtocol.Version;
            _transport.Send(JsonUtility.ToJson(message), endpoint);
        }

        private int NextEventId()
        {
            _eventSequence = (_eventSequence + 1) & 0xFFFFF;
            return Mathf.Max(1, LocalPeerId) * 1000000 + _eventSequence;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _transport?.Dispose();
        }
    }
}
