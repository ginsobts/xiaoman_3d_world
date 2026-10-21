#if UNITY_EDITOR
using System;
using System.Threading;
using UnityEngine;
using VN.Multiplayer;

namespace VN.Editor
{
    /// <summary>可由 Unity -executeMethod 调用的双端 UDP 冒烟测试。</summary>
    public static class MultiplayerTransportSmokeTest
    {
        [UnityEditor.MenuItem("3D世界/测试联机 UDP")]
        public static void Run()
        {
            using (var host = new DirectUdpTransport())
            using (var client = new DirectUdpTransport())
            {
                host.StartHost(0);
                client.StartClient("127.0.0.1", host.LocalPort);

                const string request = "{\"type\":\"hello\",\"protocol\":1}";
                const string response = "{\"type\":\"welcome\",\"protocol\":1}";
                client.SendToServer(request);

                string hostReceived = null;
                NetEndpoint clientEndpoint = default(NetEndpoint);
                WaitUntil(() =>
                {
                    host.Poll((json, endpoint) =>
                    {
                        hostReceived = json;
                        clientEndpoint = endpoint;
                    });
                    return hostReceived != null;
                }, "Host did not receive client datagram");

                if (hostReceived != request)
                    throw new Exception("UDP payload changed in transit");
                host.Send(response, clientEndpoint);

                string clientReceived = null;
                WaitUntil(() =>
                {
                    client.Poll((json, endpoint) => clientReceived = json);
                    return clientReceived != null;
                }, "Client did not receive host datagram");

                if (clientReceived != response)
                    throw new Exception("UDP response changed in transit");
                Debug.Log("[Xiaoman Multiplayer] UDP host/client smoke test passed.");
            }
        }

        private static void WaitUntil(Func<bool> predicate, string failure)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(3);
            while (DateTime.UtcNow < deadline)
            {
                if (predicate()) return;
                Thread.Sleep(10);
            }
            throw new TimeoutException(failure);
        }
    }
}
#endif
