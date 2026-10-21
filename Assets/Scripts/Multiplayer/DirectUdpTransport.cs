using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace VN.Multiplayer
{
    /// <summary>
    /// 无需中心服务器的 UDP 传输：
    /// - 局域网：客户端可广播 discover 自动发现 Host。
    /// - 互联网：输入 Host 公网 IP；Host 需映射 UDP 端口，或双方使用 Tailscale/ZeroTier。
    /// 收包在线程中进行，Poll 在 Unity 主线程分发。
    /// </summary>
    public sealed class DirectUdpTransport : IRealtimeTransport
    {
        private struct Datagram
        {
            public string Json;
            public NetEndpoint Endpoint;
        }

        private readonly ConcurrentQueue<Datagram> _inbox = new ConcurrentQueue<Datagram>();
        private UdpClient _udp;
        private Thread _receiveThread;
        private volatile bool _running;
        private IPEndPoint _server;

        public bool IsRunning => _running;
        public bool IsHost { get; private set; }
        public int LocalPort { get; private set; }

        public void StartHost(int port)
        {
            Stop();
            IsHost = true;
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, port));
            _udp.EnableBroadcast = true;
            LocalPort = ((IPEndPoint)_udp.Client.LocalEndPoint).Port;
            StartReceiver();
        }

        public void StartClient(string address, int port)
        {
            Stop();
            IsHost = false;
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            _udp.EnableBroadcast = true;
            LocalPort = ((IPEndPoint)_udp.Client.LocalEndPoint).Port;
            IPAddress ip;
            if (!IPAddress.TryParse(address, out ip))
            {
                var addresses = Dns.GetHostAddresses(address);
                if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
                ip = addresses[0];
            }
            _server = new IPEndPoint(ip, port);
            StartReceiver();
        }

        private void StartReceiver()
        {
            _running = true;
            _udp.Client.ReceiveTimeout = 300;
            _receiveThread = new Thread(ReceiveLoop)
            {
                IsBackground = true,
                Name = "Xiaoman UDP Receive"
            };
            _receiveThread.Start();
        }

        private void ReceiveLoop()
        {
            while (_running)
            {
                try
                {
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] bytes = _udp.Receive(ref remote);
                    if (bytes == null || bytes.Length == 0 || bytes.Length > 65507) continue;
                    _inbox.Enqueue(new Datagram
                    {
                        Json = Encoding.UTF8.GetString(bytes),
                        Endpoint = new NetEndpoint(remote.Address.ToString(), remote.Port)
                    });
                }
                catch (SocketException e)
                {
                    if (!_running) break;
                    if (e.SocketErrorCode != SocketError.TimedOut
                        && e.SocketErrorCode != SocketError.Interrupted)
                        _inbox.Enqueue(new Datagram { Json = "", Endpoint = default(NetEndpoint) });
                }
                catch (ObjectDisposedException) { break; }
                catch { if (!_running) break; }
            }
        }

        public void Send(string json, NetEndpoint endpoint)
        {
            if (!_running || _udp == null || string.IsNullOrEmpty(json)) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                _udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Parse(endpoint.Address), endpoint.Port));
            }
            catch { /* 上层用超时判断掉线；单个 UDP 包失败不终止会话。 */ }
        }

        public void SendToServer(string json)
        {
            if (_server == null) return;
            Send(json, new NetEndpoint(_server.Address.ToString(), _server.Port));
        }

        public void BroadcastProbe(int port, string json)
        {
            if (!_running || _udp == null || string.IsNullOrEmpty(json)) return;
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(json);
                _udp.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, port));
            }
            catch { }
        }

        public void Poll(Action<string, NetEndpoint> onDatagram)
        {
            if (onDatagram == null) return;
            int budget = 256;
            while (budget-- > 0 && _inbox.TryDequeue(out var datagram))
                if (!string.IsNullOrEmpty(datagram.Json))
                    onDatagram(datagram.Json, datagram.Endpoint);
        }

        public void Stop()
        {
            _running = false;
            try { _udp?.Close(); } catch { }
            _udp = null;
            if (_receiveThread != null && _receiveThread.IsAlive)
                _receiveThread.Join(400);
            _receiveThread = null;
            _server = null;
            LocalPort = 0;
            while (_inbox.TryDequeue(out _)) { }
        }

        public void Dispose() => Stop();
    }
}
