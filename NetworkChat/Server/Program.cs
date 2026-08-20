using System.Security.Cryptography.X509Certificates;
using Server.Udp;
using Server.Ws;

namespace Server;

internal static class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== NetworkChat Server ===");
        Console.WriteLine("Protocol:");
        Console.WriteLine("[1] UDP");
        Console.WriteLine("[2] WebSocket");
        Console.WriteLine("[3] WebSocket + TLS (WSS)");
        Console.Write("Select: ");

        string? choice = Console.ReadLine();
        Console.Write("Port (default 5000): ");
        string? portInput = Console.ReadLine();
        int port = int.TryParse(portInput, out int p) ? p : 5000;

        switch (choice)
        {
            case "1":
                await RunUdpAsync(port);
                break;
            case "2":
                await RunWebSocketAsync(port);
                break;
            case "3":
                await RunWebSocketAsync(port, LoadCertificate());
                break;
            default:
                Console.WriteLine("Invalid option");
                break;
        }
    }

    private static async Task RunUdpAsync(int port)
    {
        using UdpChatServer server = new(port);
        server.Log += msg => Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] {msg}");

        using CancellationTokenSource cts = new();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await server.StartAsync(cts.Token);
    }

    private static async Task RunWebSocketAsync(int port, X509Certificate2? cert = null)
    {
        using WebSocketChatServer server = new();
        server.Log += msg => Console.WriteLine($"  [{DateTime.Now:HH:mm:ss}] {msg}");

        await server.StartAsync(port, cert);

        Console.WriteLine("Press Enter to stop...");
        Console.ReadLine();
    }

    private static X509Certificate2? LoadCertificate()
    {
        string certPath = Path.Combine(AppContext.BaseDirectory, "cert.pfx");

        if (File.Exists(certPath))
        {
            Console.Write("Certificate password (empty if none): ");
            string? password = Console.ReadLine();
            return X509CertificateLoader.LoadPkcs12FromFile(certPath, password);
        }

        Console.WriteLine("No cert.pfx found. Running without TLS (ws:// instead of wss://)");
        return null;
    }
}
