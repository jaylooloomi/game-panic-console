using System;

namespace PanicConsole.Core.Net
{
    public enum NetRole { None, Host, Client }

    /// <summary>連線傳輸抽象層。對戰邏輯只依賴此介面；本機/測試用 <see cref="LocalTransport"/>，
    /// 正式 1v1 用 SteamRelayTransport（基於 Steam Datagram Relay，待實作—見 handoff 文件）。
    /// 重點：把「第二位玩家的輸入/狀態」做成可從本介面收發，之後換成連線對手即可。</summary>
    public interface INetTransport
    {
        NetRole Role { get; }
        bool IsConnected { get; }

        void Host();                  // 開房（SteamRelay：CreateRelaySocket）
        void Join(string sessionId);  // 加入（SteamRelay：ConnectRelay(steamId)）
        void Send(byte[] data);       // 送一個封包給對手
        void Poll();                  // 每幀呼叫：派發收到的封包、處理連線事件
        void Close();

        event Action<byte[]> OnReceive;
        event Action OnConnected;
        event Action OnDisconnected;
    }
}
