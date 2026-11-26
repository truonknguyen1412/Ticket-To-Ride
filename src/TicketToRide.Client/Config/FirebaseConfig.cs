namespace TicketToRide.Client.Config;

public static class FirebaseConfig
{
    public const string ApiKey = "AIzaSyDMLO2ZNCVTSVHSJruyu711JkI3Vr4Anzc";

    public static readonly string ServerHost;
    public static readonly int ServerPort;

    static FirebaseConfig()
    {
        ServerHost = "127.0.0.1";
        ServerPort = 5000;

        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "server.txt");
            var resolved = ServerEndpointResolver.ResolveServerAddress(path, localTimeoutMs: 400);
            var parts = resolved.Split(':', 2);

            if (parts.Length == 2 && int.TryParse(parts[1].Trim(), out var p))
            {
                ServerHost = parts[0].Trim();
                ServerPort = p;
            }
        }
        catch { /* lỗi đọc file thì dùng mặc định */ }
    }
}