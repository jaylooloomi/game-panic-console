using System;
using System.Collections.Generic;

namespace PanicConsole.Core.Net
{
    /// <summary>本機 loopback 傳輸（同進程兩端互通），用於驗證連線抽象與離線測試。
    /// 正式連線把這個換成 SteamRelayTransport，對戰邏輯不需改動。</summary>
    public class LocalTransport : INetTransport
    {
        public NetRole Role { get; private set; }
        public bool IsConnected { get; private set; }

        public event Action<byte[]> OnReceive;
        public event Action OnConnected;
        public event Action OnDisconnected;

        private LocalTransport _peer;
        private readonly Queue<byte[]> _inbox = new Queue<byte[]>();

        /// <summary>建立互通的一對（host/client），並標記為已連線。</summary>
        public static (LocalTransport host, LocalTransport client) CreatePair()
        {
            var host = new LocalTransport { Role = NetRole.Host };
            var client = new LocalTransport { Role = NetRole.Client };
            host._peer = client;
            client._peer = host;
            host.IsConnected = client.IsConnected = true;
            return (host, client);
        }

        public void Host() { Role = NetRole.Host; }
        public void Join(string sessionId) { Role = NetRole.Client; }

        public void Send(byte[] data)
        {
            if (_peer != null) _peer._inbox.Enqueue(data);
        }

        public void Poll()
        {
            while (_inbox.Count > 0) OnReceive?.Invoke(_inbox.Dequeue());
        }

        public void Close()
        {
            if (!IsConnected) return;
            IsConnected = false;
            OnDisconnected?.Invoke();
        }
    }
}
