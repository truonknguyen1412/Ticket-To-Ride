using System.Text;
using TicketToRide.Server;

// Để PowerShell hiển thị đúng tiếng Việt
Console.OutputEncoding = Encoding.UTF8;

// Cách chạy:
//   $env:FIREBASE_API_KEY = "<Web API key của dự án Firebase>"
//   dotnet run --project src/TicketToRide.Server -- [cổng] [--dev] [--firebase-key=KEY]
var port = 5000;
var dev = false;
var key = Environment.GetEnvironmentVariable("FIREBASE_API_KEY");
foreach (var arg in args)
{
    if (arg == "--dev") dev = true;
    else if (arg.StartsWith("--firebase-key=", StringComparison.Ordinal)) key = arg["--firebase-key=".Length..];
    else if (int.TryParse(arg, out var p)) port = p;
}

var log = new ServerLogger();
var options = new ServerOptions
{
    Port = port,
    AllowAnonymous = dev,
    FirebaseApiKey = string.IsNullOrWhiteSpace(key) ? null : key.Trim(),
};

var server = new GameServer(options, log);
server.Start();

log.Info("HE THONG", "Nhấn Enter để dừng server...");
Console.ReadLine();
server.Stop();
