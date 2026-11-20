# Xác thực phía Server — cách chạy và kiểm thử

## Luồng
Mật khẩu chỉ đi giữa client và Firebase. Server không bao giờ thấy mật khẩu.

| Tính năng | Client gửi | Server làm | Token |
|---|---|---|---|
| Đăng ký | `AUTH_REGISTER {idToken, username}` | Nhờ Firebase xác minh idToken, lưu hồ sơ vào bảng `Accounts` trong SQL Server, tự đăng nhập | Cấp token phiên (`WELCOME`) |
| Đăng nhập | `AUTH_LOGIN {idToken}` | Nhờ Firebase xác minh idToken. Cùng tài khoản đăng nhập nơi khác thì xoay token và đá kết nối cũ (`SESSION_REPLACED`) | Cấp/xoay token |
| Quên mật khẩu | `AUTH_FORGOT {email}` (chưa cần đăng nhập) | Kiểm tra email, giới hạn 60 giây/email, nhờ Firebase gửi mail | Không dùng token |
| Đăng xuất | `AUTH_LOGOUT` | Báo phòng cho người chơi rời, **thu hồi token ngay** | Token chết |

Token phiên (`resumeToken`) do server cấp, sống 12 giờ, gia hạn khi nối lại hoặc nhận PING. Mọi thao tác cấp/xoay/thu hồi token đều in ra console (nhãn `TOKEN`). Token chỉ in dạng che bớt.
Chưa đăng nhập thì chỉ được gửi: các thông điệp AUTH_*, HELLO kèm resumeToken, PING, BYE. Sai xác thực 5 lần trên một kết nối thì server đóng kết nối.

## Phân công lưu trữ
- **Firebase Authentication**: giữ email, mật khẩu (đã băm), tên hiển thị, uid. Mật khẩu không bao giờ đến server game.
- **SQL Server** (bảng `dbo.Accounts`): giữ hồ sơ game gồm `Uid` (nối với Firebase), `Email`, `Username`, `CreatedUtc`, `LastLoginUtc`, `LoginCount`.
- **Bộ nhớ server**: token phiên (`resumeToken`), mất khi khởi động lại server.

## Chuẩn bị SQL Server
1. Cần một SQL Server trên máy. Cách nhanh nhất: LocalDB đi kèm Visual Studio (Visual Studio Installer → Modify → thành phần "SQL Server Express LocalDB"). Hoặc cài SQL Server Express.
2. Mặc định server game kết nối `(localdb)\MSSQLLocalDB`, database `TicketToRide`, đăng nhập bằng tài khoản Windows, và **tự tạo database + bảng `Accounts`** khi khởi động. Nếu dùng SQL Server khác, truyền chuỗi kết nối:
   ```powershell
   $env:SQLSERVER_CONNECTION = "Server=.\SQLEXPRESS;Database=TicketToRide;Integrated Security=True;TrustServerCertificate=True;"
   # hoặc dùng tài khoản sa:
   # $env:SQLSERVER_CONNECTION = "Server=localhost;Database=TicketToRide;User Id=sa;Password=<mật khẩu>;TrustServerCertificate=True;"
   ```
3. Nếu server báo không tạo được database (thiếu quyền), mở SSMS và chạy `docs/sql/tao-bang-accounts.sql`.
4. Xem dữ liệu khi demo: SSMS → kết nối cùng server → `TicketToRide` → `dbo.Accounts` (chuột phải → Select Top 1000 Rows).

## Chạy server trong PowerShell
```powershell
cd TicketToRide
$env:FIREBASE_API_KEY = "<Web API key của dự án Firebase>"   # cùng key trong FirebaseAuthService.cs
dotnet run --project src/TicketToRide.Server
```
Tùy chọn: `[cổng]` (mặc định 5000), `--firebase-key=KEY`, `--sql=CHUỖI_KẾT_NỐI` (hoặc biến `SQLSERVER_CONNECTION`), `--dev` (cho HELLO chỉ bằng tên, chỉ để chạy `rooms` của TestClient).
Không kết nối được SQL Server thì server vẫn chạy nhưng từ chối đăng ký/đăng nhập với mã `DB_UNAVAILABLE` và in lý do ra console.
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
- Server: `AuthService.cs`, `FirebaseIdentity.cs`, `AccountStore.cs` (lưu hồ sơ bằng SQL Server), `ServerLogger.cs`, `ServerOptions.cs` (mới); `Dispatcher.cs`, `Connection.cs`, `GameServer.cs`, `Program.cs`, `Session.cs`, `SessionManager.cs`, `Room.cs`, `RoomCommand.cs` (sửa); `TicketToRide.Server.csproj` (thêm gói `Microsoft.Data.SqlClient`)
- Shared: `Net/Protocol.cs`, `Net/Payloads.cs` (thêm 4 thông điệp AUTH_*, mã lỗi gồm `DB_UNAVAILABLE`, payload)
- Docs: `docs/sql/tao-bang-accounts.sql` (script tạo bảng, xem dữ liệu)
- ClientCore: `GameClient.cs` (LoginAsync, RegisterAsync, ForgotPasswordAsync, LogoutAsync, ResumeAsync)
- Client: `TcpClientService.cs`, `AuthForm.cs`
- TestClient: `Program.cs` (thêm chế độ `auth`)
