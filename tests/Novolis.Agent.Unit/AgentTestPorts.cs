using System.Net;
using System.Net.Sockets;

namespace Novolis.Agent.Unit;

static class AgentTestPorts
{
    public static int GetFreePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}


