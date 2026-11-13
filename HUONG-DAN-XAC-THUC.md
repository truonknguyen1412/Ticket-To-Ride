# Xác thực phía Server — cách chạy và kiểm thử

## Luồng
Mật khẩu chỉ đi giữa client và Firebase. Server không bao giờ thấy mật khẩu.

| Tính năng | Client gửi | Server làm | Token |
|---|---|---|---|
| Đăng ký | `AUTH_REGISTER {idToken, username}` | Nhờ Firebase xác minh idToken, lưu hồ sơ vào `accounts.json`, tự đăng nhập | Cấp token phiên (`WELCOME`) |
| Đăng nhập | `AUTH_LOGIN {idToken}` | Nhờ Firebase xác minh idToken. Cùng tài khoản đăng nhập nơi khác thì xoay token và đá kết nối cũ (`SESSION_REPLACED`) | Cấp/xoay token |
| Quên mật khẩu | `AUTH_FORGOT {email}` (chưa cần đăng nhập) | Kiểm tra email, giới hạn 60 giây/email, nhờ Firebase gửi mail | Không dùng token |
| Đăng xuất | `AUTH_LOGOUT` | Báo phòng cho người chơi rời, **thu hồi token ngay** | Token chết |

Token phiên (`resumeToken`) do server cấp, sống 12 giờ, gia hạn khi nối lại hoặc nhận PING. Mọi thao tác cấp/xoay/thu hồi token đều in ra console (nhãn `TOKEN`). Token chỉ in dạng che bớt.
Chưa đăng nhập thì chỉ được gửi: các thông điệp AUTH_*, HELLO kèm resumeToken, PING, BYE. Sai xác thực 5 lần trên một kết nối thì server đóng kết nối.

## Chạy server trong PowerShell
```powershell
cd TicketToRide
$env:FIREBASE_API_KEY = "<Web API key của dự án Firebase>"   # cùng key trong FirebaseAuthService.cs
dotnet run --project src/TicketToRide.Server
```
Tùy chọn: `[cổng]` (mặc định 5000), `--firebase-key=KEY`, `--dev` (cho HELLO chỉ bằng tên, chỉ để chạy `rooms` của TestClient).
Console server hiển thị theo màu: xanh = thành công, đỏ = từ chối, vàng = cảnh báo (thu hồi/xoay token), xám = từng bước.

## Chạy client thật
```powershell
dotnet run --project src/TicketToRide.Client
```
Đăng ký, đăng nhập, quên mật khẩu, đăng xuất ngay trên giao diện, đồng thời xem log ở cửa sổ server.

## Kiểm thử tự động cả 4 luồng
```powershell
dotnet run --project src/TicketToRide.TestClient -- auth 127.0.0.1 5000 $env:FIREBASE_API_KEY email@cua-ban.com MatKhau [TenHienThi]
```
Lưu ý: bước 1 gửi email đặt lại mật khẩu thật tới email bạn nhập.
Các bước: quên MK (email sai/đúng/xin lại ngay), chặn khi chưa đăng nhập và token giả, đăng nhập hoặc đăng ký, đăng ký trùng, đăng nhập lần 2 (xoay token, đá máy cũ), nối lại bằng token cũ bị từ chối, nối lại bằng token mới rồi đăng xuất, dùng lại token sau đăng xuất bị từ chối, dò token sai quá 5 lần bị đóng kết nối. Mỗi dòng in `[DUNG]` hoặc `[SAI]`.

## Đã đổi những file nào
- Server: `AuthService.cs`, `FirebaseIdentity.cs`, `AccountStore.cs`, `ServerLogger.cs`, `ServerOptions.cs` (mới); `Dispatcher.cs`, `Connection.cs`, `GameServer.cs`, `Program.cs`, `Session.cs`, `SessionManager.cs`, `Room.cs`, `RoomCommand.cs` (sửa)
- Shared: `Net/Protocol.cs`, `Net/Payloads.cs` (thêm 4 thông điệp AUTH_*, mã lỗi, payload)
- ClientCore: `GameClient.cs` (LoginAsync, RegisterAsync, ForgotPasswordAsync, LogoutAsync, ResumeAsync)
- Client: `TcpClientService.cs`, `AuthForm.cs`
- TestClient: `Program.cs` (thêm chế độ `auth`)
