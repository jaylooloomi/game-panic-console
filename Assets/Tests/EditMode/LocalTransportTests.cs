using NUnit.Framework;
using PanicConsole.Core.Net;

public class LocalTransportTests
{
    [Test] public void PairIsConnectedWithRoles()
    {
        var (host, client) = LocalTransport.CreatePair();
        Assert.IsTrue(host.IsConnected);
        Assert.IsTrue(client.IsConnected);
        Assert.AreEqual(NetRole.Host, host.Role);
        Assert.AreEqual(NetRole.Client, client.Role);
    }

    [Test] public void SendFromHostArrivesAtClientOnPoll()
    {
        var (host, client) = LocalTransport.CreatePair();
        byte[] got = null;
        client.OnReceive += d => got = d;
        host.Send(new byte[] { 1, 2, 3 });
        Assert.IsNull(got);   // 尚未 Poll
        client.Poll();
        Assert.IsNotNull(got);
        Assert.AreEqual(3, got.Length);
        Assert.AreEqual(2, got[1]);
    }

    [Test] public void SendBothDirections()
    {
        var (host, client) = LocalTransport.CreatePair();
        int hostRecv = 0, clientRecv = 0;
        host.OnReceive += _ => hostRecv++;
        client.OnReceive += _ => clientRecv++;
        host.Send(new byte[] { 9 });
        client.Send(new byte[] { 8 });
        host.Poll(); client.Poll();
        Assert.AreEqual(1, hostRecv);
        Assert.AreEqual(1, clientRecv);
    }

    [Test] public void CloseRaisesDisconnect()
    {
        var (host, _) = LocalTransport.CreatePair();
        bool disc = false; host.OnDisconnected += () => disc = true;
        host.Close();
        Assert.IsTrue(disc);
        Assert.IsFalse(host.IsConnected);
    }
}
