using System.Net.Http.Json;
using System.Text.Json;
using TicketToRide.ClientCore;
using TicketToRide.Shared;
using TicketToRide.Shared.Net;

// Cách dùng:
//   auth  <host> <port> <firebaseApiKey> <email> <password> [username]
//         Kiểm thử 4 luồng xác thực với server thật (server chạy bình thường, có FIREBASE_API_KEY).
//         Có [username] thì nếu email chưa có tài khoản Firebase sẽ tự đăng ký mới.
//   rooms [host] [port]
//         Kịch bản phòng chơi cũ (server phải chạy với cờ --dev vì dùng HELLO không xác thực).

Console.OutputEncoding = System.Text.Encoding.UTF8;
var http = new HttpClient();
var failed = 0;

if (args.Length == 0 || args[0] is "-h" or "--help") { Usage(); return; }

switch (args[0])
{
    case "auth": await AuthScenario(args); break;
    case "rooms": await RoomsScenario(args); break;
    default: Usage(); break;
}
return;

// ======================================================================= xác thực

async Task AuthScenario(string[] a)
{
    if (a.Length < 6) { Usage(); return; }
    var host = a[1];
    var port = int.Parse(a[2]);
    var apiKey = a[3];
    var email = a[4];
    var password = a[5];
    var username = a.Length > 6 ? a[6] : null;

    Section("1. QUÊN MẬT KHẨU (AUTH_FORGOT, không cần đăng nhập)");
    var f = new GameClient();
    await f.ConnectAnonymousAsync(host, port);
    await Expect("email sai định dạng", ErrorCodes.EmailInvalid, () => f.ForgotPasswordAsync("khong-phai-email"));
    try { Console.WriteLine($"  [DUNG] server báo: {await f.ForgotPasswordAsync(email)}"); }
    catch (ServerErrorException e) { Console.WriteLine($"  [SAI]  {e.Code}: {e.Message}"); failed++; }
    await Expect("xin lại ngay trong 60 giây", ErrorCodes.Cooldown, () => f.ForgotPasswordAsync(email));
    f.Dispose();

    Section("2. CHẶN KHI CHƯA ĐĂNG NHẬP / TOKEN GIẢ");
    var x = new GameClient();
    await x.ConnectAnonymousAsync(host, port);
    await Expect("tạo phòng khi chưa đăng nhập", ErrorCodes.AuthRequired, () => x.CreateRoomAsync());
    await Expect("đăng nhập bằng idToken giả", ErrorCodes.TokenInvalid, () => x.LoginAsync("abc.def.ghi"));
    x.Dispose();

    Section("3. ĐĂNG NHẬP / ĐĂNG KÝ (idToken Firebase -> token phiên của server)");
    var idToken = await FirebaseAsync(apiKey, "signInWithPassword", email, password);
    var isNew = false;
    if (idToken == null && username != null)
    {
        Console.WriteLine("  Chưa có tài khoản Firebase, đăng ký mới...");
        idToken = await FirebaseAsync(apiKey, "signUp", email, password);
        isNew = idToken != null;
    }
    if (idToken == null) { Console.WriteLine("  Không lấy được idToken từ Firebase, dừng."); failed++; return; }

    var a1 = new GameClient();
    a1.MessageReceived += m => Console.WriteLine($"    [máy A nhận] {m.Type} {m.Payload}");
    await a1.ConnectAnonymousAsync(host, port);
    var w1 = isNew ? await a1.RegisterAsync(idToken, username!) : await a1.LoginAsync(idToken);
    Console.WriteLine($"  [DUNG] {(isNew ? "Đăng ký" : "Đăng nhập")} thành công: tên \"{w1.PlayerName}\", playerId {w1.PlayerId[..8]}, token sống {w1.ExpiresInSeconds}s");
    Console.WriteLine($"  Phòng tạo thử bằng phiên này: {await a1.CreateRoomAsync()}");

    var y = new GameClient();
    await y.ConnectAnonymousAsync(host, port);
    await Expect("đăng ký lại tài khoản đã có", ErrorCodes.AccountExists, () => y.RegisterAsync(idToken, "trung-ten"));
    y.Dispose();

    Section("4. ĐĂNG NHẬP LẦN 2 CÙNG TÀI KHOẢN: XOAY TOKEN, ĐÁ MÁY CŨ");
    var b = new GameClient();
    await b.ConnectAnonymousAsync(host, port);
    var w2 = await b.LoginAsync(idToken);
    Console.WriteLine(w2.ResumeToken != w1.ResumeToken
        ? "  [DUNG] token mới khác token cũ"
        : "  [SAI]  token không được xoay");
    if (w2.ResumeToken == w1.ResumeToken) failed++;
    await Task.Delay(600);      // chờ máy A nhận SESSION_REPLACED
    var c = new GameClient();
    await Expect("nối lại bằng token cũ (đã xoay)", ErrorCodes.ResumeRejected, () => c.ResumeAsync(host, port, w1.ResumeToken));
    c.Dispose();

    Section("5. NỐI LẠI BẰNG TOKEN, RỒI ĐĂNG XUẤT (AUTH_LOGOUT)");
    var d = new GameClient();
    try
    {
        await d.ResumeAsync(host, port, w2.ResumeToken);
        Console.WriteLine($"  [DUNG] nối lại bằng token hợp lệ: \"{d.PlayerName}\"");
    }
    catch (ServerErrorException e) { Console.WriteLine($"  [SAI]  {e.Code}: {e.Message}"); failed++; }
    try { await d.LogoutAsync(); Console.WriteLine("  [DUNG] đăng xuất: server đã ACK"); }
    catch (Exception e) { Console.WriteLine($"  [SAI]  đăng xuất lỗi: {e.Message}"); failed++; }
    var e2 = new GameClient();
    await Expect("dùng lại token sau khi đăng xuất", ErrorCodes.ResumeRejected, () => e2.ResumeAsync(host, port, w2.ResumeToken));
    e2.Dispose();
    a1.Dispose();
    b.Dispose();

    Section($"6. CHẶN DÒ TOKEN: sai {Protocol.MaxAuthFailures} lần thì server đóng kết nối");
    var z = new GameClient();
    await z.ConnectAnonymousAsync(host, port);
    var closed = false;
    for (var i = 1; i <= Protocol.MaxAuthFailures + 1 && !closed; i++)
    {
        try { await z.LoginAsync("sai.token.thu"); }
        catch (ServerErrorException e) { Console.WriteLine($"    lần {i}: {e.Code}"); }
        catch (Exception e) { closed = true; Console.WriteLine($"    lần {i}: kết nối đã bị server đóng ({e.GetType().Name})"); }
    }
    Console.WriteLine(closed ? "  [DUNG] server đã đóng kết nối" : "  [SAI]  server không đóng kết nối");
    if (!closed) failed++;
    z.Dispose();

    Console.WriteLine();
    Console.WriteLine(failed == 0 ? "KẾT QUẢ: TẤT CẢ BƯỚC ĐẠT" : $"KẾT QUẢ: {failed} bước SAI, xem log ở cửa sổ server");
}

async Task Expect(string what, string expectedCode, Func<Task> act)
{
    try
    {
        await act();
        Console.WriteLine($"  [SAI]  {what}: lẽ ra phải bị từ chối {expectedCode}");
        failed++;
    }
    catch (ServerErrorException e) when (e.Code == expectedCode)
    {
        Console.WriteLine($"  [DUNG] {what}: server từ chối {e.Code} ({e.Message})");
    }
    catch (Exception e)
    {
        Console.WriteLine($"  [SAI]  {what}: nhận {e.GetType().Name} \"{e.Message}\", mong đợi {expectedCode}");
        failed++;
    }
}

async Task<string?> FirebaseAsync(string apiKey, string method, string email, string password)
{
    var url = $"https://identitytoolkit.googleapis.com/v1/accounts:{method}?key={Uri.EscapeDataString(apiKey)}";
    using var r = await http.PostAsJsonAsync(url, new { email, password, returnSecureToken = true });
    var json = await r.Content.ReadAsStringAsync();
    if (!r.IsSuccessStatusCode)
    {
        Console.WriteLine($"  Firebase {method} lỗi: {json}");
        return null;
    }
    using var doc = JsonDocument.Parse(json);
    return doc.RootElement.GetProperty("idToken").GetString();
}

void Section(string title) => Console.WriteLine($"\n=== {title} ===");

// ======================================================================= phòng chơi (cũ)

async Task RoomsScenario(string[] a)
{
    // Kiểm thử dòng 12/13/20: hai client kết nối, tạo phòng, vào phòng, sẵn sàng, bắt đầu.
    var host = a.Length > 1 ? a[1] : "127.0.0.1";
    var port = a.Length > 2 ? int.Parse(a[2]) : 5000;

    var p1 = new GameClient();
    var p2 = new GameClient();
    p1.MessageReceived += m => Console.WriteLine($"[Tuan nhận] {m.Type}");
    p2.MessageReceived += m => Console.WriteLine($"[Phuc nhận] {m.Type}");

    await p1.ConnectAsync(host, port, "Tuan");
    await p2.ConnectAsync(host, port, "Phuc");
    Console.WriteLine("Hai client đã kết nối (HELLO/WELCOME ok)");

    var code = await p1.CreateRoomAsync();
    Console.WriteLine($"Tuan tạo phòng {code}");

    await p2.JoinRoomAsync(code);
    await p2.SetReadyAsync(true);
    await p1.StartGameAsync();
    Console.WriteLine("Phòng đã sang IN_GAME");

    // Các ca lỗi
    try { await p2.JoinRoomAsync(code); }
    catch (ServerErrorException e) { Console.WriteLine($"Vào phòng lần 2 bị từ chối đúng: {e.Code}"); }

    var p3 = new GameClient();
    await p3.ConnectAsync(host, port, "Quang");
    try { await p3.JoinRoomAsync("000000"); }
    catch (ServerErrorException e) { Console.WriteLine($"Mã phòng sai bị từ chối đúng: {e.Code}"); }
    try { await p3.JoinRoomAsync(code); }
    catch (ServerErrorException e) { Console.WriteLine($"Phòng đã vào ván bị từ chối đúng: {e.Code}"); }

    await Task.Delay(500);
    await p1.DisconnectAsync();
    await p2.DisconnectAsync();
    await p3.DisconnectAsync();
    Console.WriteLine("Xong");
}

void Usage()
{
    Console.WriteLine("Cách dùng:");
    Console.WriteLine("  auth  <host> <port> <firebaseApiKey> <email> <password> [username]");
    Console.WriteLine("  rooms [host] [port]      (server phải chạy với --dev)");
}
