using TicketToRide.Server;
Console.OutputEncoding = System.Text.Encoding.UTF8;
var port = args.Length > 0 && int.TryParse(args[0], out var p) ? p : 5000;

var server = new GameServer();
server.Log += Console.WriteLine;
server.Start(port);

Console.WriteLine("Nhấn Enter để dừng server...");
Console.ReadLine();
server.Stop();
