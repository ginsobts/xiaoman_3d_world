using System;

namespace VN.Multiplayer
{
    /// <summary>
    /// 实时传输抽象。游戏同步层只依赖它；未来接 Unity Relay、Steam、
    /// Epic Online Services 或 WebSocket 时无需改玩家/场景逻辑。
    /// </summary>
    public interface IRealtimeTransport : IDisposable
    {
        bool IsRunning { get; }
        bool IsHost { get; }
        int LocalPort { get; }

        void StartHost(int port);
        void StartClient(string address, int port);
        void Send(string json, NetEndpoint endpoint);
        void SendToServer(string json);
        void BroadcastProbe(int port, string json);
        void Poll(Action<string, NetEndpoint> onDatagram);
        void Stop();
    }

    /// <summary>
    /// 可选房间服务接口。当前 Direct IP/LAN 不需要服务器；
    /// 后续 Relay 实现负责返回一个传输可消费的连接信息。
    /// </summary>
    public interface IRemoteRoomProvider
    {
        bool IsAvailable { get; }
        void CreateRoom(string displayName, Action<RemoteRoomInfo> completed);
        void JoinRoom(string joinCode, Action<RemoteRoomInfo> completed);
    }

    [Serializable]
    public class RemoteRoomInfo
    {
        public bool success;
        public string joinCode;
        public string address;
        public int port;
        public string relayToken;
        public string error;
    }
}
