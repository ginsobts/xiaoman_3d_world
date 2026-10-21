using System;
using UnityEngine;

namespace VN.Multiplayer
{
    public static class MultiplayerProtocol
    {
        public const int Version = 1;
        public const int DefaultPort = 7777;
        public const float SnapshotInterval = 1f / 15f;

        public const string Discover = "discover";
        public const string Offer = "offer";
        public const string Hello = "hello";
        public const string Welcome = "welcome";
        public const string PlayerJoin = "player_join";
        public const string PlayerLeave = "player_leave";
        public const string Snapshot = "snapshot";
        public const string Instrument = "instrument";
        public const string EditRequest = "edit_request";
        public const string EditSpawn = "edit_spawn";
        public const string EditDelete = "edit_delete";
        public const string EditTransform = "edit_transform";
        public const string Ping = "ping";
        public const string Pong = "pong";
    }

    /// <summary>
    /// UDP 上的统一小消息。避免多态序列化，使协议以后能由 WebSocket/Relay 原样承载。
    /// 未使用字段保持默认值；protocol 不匹配的包会被丢弃。
    /// </summary>
    [Serializable]
    public class MultiplayerMessage
    {
        public int protocol = MultiplayerProtocol.Version;
        public string type = "";
        public int sequence;
        public int peerId;
        public int playerId;
        public int localSlot;
        public int objectId;
        public int eventId;
        public int colorIndex;
        public string name = "";
        public string payload = "";
        public float x;
        public float y;
        public float z;
        public float yaw;
        public float value;

        public Vector3 Position
        {
            get => new Vector3(x, y, z);
            set { x = value.x; y = value.y; z = value.z; }
        }
    }

    [Serializable]
    public class MultiplayerConfig
    {
        public int port = MultiplayerProtocol.DefaultPort;
        public string lastAddress = "127.0.0.1";
        public string playerName = "Player";
        public int maxLocalPlayers = 4;
        public bool allowSceneEditing = true;
    }

    /// <summary>传输端点。Relay/Steam 实现可以把 Address 当作房间成员 token，而非 IP。</summary>
    public struct NetEndpoint : IEquatable<NetEndpoint>
    {
        public string Address;
        public int Port;

        public NetEndpoint(string address, int port)
        {
            Address = address ?? "";
            Port = port;
        }

        public bool Equals(NetEndpoint other) => Address == other.Address && Port == other.Port;
        public override bool Equals(object obj) => obj is NetEndpoint other && Equals(other);
        public override int GetHashCode() => (Address ?? "").GetHashCode() * 397 ^ Port;
        public override string ToString() => Address + ":" + Port;
    }
}
