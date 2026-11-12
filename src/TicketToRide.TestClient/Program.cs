using TicketToRide.ClientCore;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

// Kiểm thử dòng 12/13/20: hai client kết nối, tạo phòng, vào phòng, sẵn sàng, bắt đầu.
var host = args.Length > 0 ? args[0] : "127.0.0.1";
var port = args.Length > 1 ? int.Parse(args[1]) : 5000;

var a = new GameClient();
var b = new GameClient();
a.MessageReceived += m => Console.WriteLine($"[Tuan nhận] {m.Type}");
b.MessageReceived += m => Console.WriteLine($"[Phuc nhận] {m.Type}");

await a.ConnectAsync(host, port, "Tuan");
await b.ConnectAsync(host, port, "Phuc");
Console.WriteLine("Hai client đã kết nối (HELLO/WELCOME ok)");

var code = await a.CreateRoomAsync();
Console.WriteLine($"Tuan tạo phòng {code}");

await b.JoinRoomAsync(code);
await b.SetReadyAsync(true);
await a.StartGameAsync();
Console.WriteLine("Phòng đã sang IN_GAME");

// Các ca lỗi
try { await b.JoinRoomAsync(code); }
catch (ServerErrorException e) { Console.WriteLine($"Vào phòng lần 2 bị từ chối đúng: {e.Code}"); }

var c = new GameClient();
await c.ConnectAsync(host, port, "Quang");
try { await c.JoinRoomAsync("000000"); }
catch (ServerErrorException e) { Console.WriteLine($"Mã phòng sai bị từ chối đúng: {e.Code}"); }
try { await c.JoinRoomAsync(code); }
catch (ServerErrorException e) { Console.WriteLine($"Phòng đã vào ván bị từ chối đúng: {e.Code}"); }

await Task.Delay(500);
await a.DisconnectAsync();
await b.DisconnectAsync();
await c.DisconnectAsync();
Console.WriteLine("Xong");
