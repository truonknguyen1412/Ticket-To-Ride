using TicketToRide.ClientCore;
using TicketToRide.Shared.Models;
using TicketToRide.Shared.Net;
using TicketToRide.Shared.Protocol;

namespace TicketToRide.Client.Services;

/// <summary>
/// Cầu nối giữa giao diện (AuthForm, LobbyForm) và Server thật, bên trong dùng GameClient (ClientCore).
/// Luồng xác thực: Firebase cấp idToken ở client -> gửi lên Server (AUTH_LOGIN / AUTH_REGISTER) ->
/// Server nhờ Firebase xác minh rồi cấp token phiên của riêng Server. Quên mật khẩu và đăng xuất cũng đi qua Server.
/// </summary>
public class TcpClientService
{
    // true = chạy giao diện không cần Server (đăng nhập giả).
    public static bool UseMockServer { get; set; } = false;

    public static string ServerHost { get; set; } = "127.0.0.1";
    public static int ServerPort { get; set; } = 5000;

    private const int MaxNameLength = 20; // khớp Protocol.MaxPlayerNameLength của Server

    /// <summary>Kết nối thật tới Server. Lobby dùng cái này để tạo/vào phòng (CreateRoomAsync, JoinRoomAsync...).</summary>
    public GameClient? Game { get; private set; }

    /// <param name="isRegistration">true: AUTH_REGISTER (vừa tạo tài khoản Firebase); false: AUTH_LOGIN.</param>
    public async Task<AuthResponse> AuthenticateWithServerAsync(string firebaseIdToken, string email, string? displayName, bool isRegistration = false)
    {
        var name = !string.IsNullOrWhiteSpace(displayName) ? displayName.Trim() : email.Split('@')[0];
        if (name.Length > MaxNameLength) name = name[..MaxNameLength];

        if (UseMockServer)
        {
            await Task.Delay(300);
            return new AuthResponse
            {
                Success = true,
                Message = "Đăng nhập thành công (Chế độ giả lập Server)",
                SessionId = Guid.NewGuid().ToString(),
                Account = new Account { AccountId = Guid.NewGuid(), Username = name }
            };
        }

        try
        {
            // Tạo GameClient ngay trên UI thread (trước mọi await) để sự kiện của nó về đúng UI thread.
            Game?.Dispose();
            Game = new GameClient();

            await Game.ConnectAnonymousAsync(ServerHost, ServerPort);
            var welcome = isRegistration
                ? await Game.RegisterAsync(firebaseIdToken, name)
                : await Game.LoginAsync(firebaseIdToken);

            var username = welcome.PlayerName ?? name;
            return new AuthResponse
            {
                Success = true,
                Message = isRegistration ? "Đăng ký thành công." : "Đăng nhập thành công.",
                SessionId = welcome.PlayerId,
                Account = new Account
                {
                    AccountId = Guid.TryParse(welcome.PlayerId, out var id) ? id : Guid.NewGuid(),
                    Username = username
                }
            };
        }
        catch (ServerErrorException ex)
        {
            Game?.Dispose();
            Game = null;
            return new AuthResponse { Success = false, Message = $"Server từ chối: {ex.Message}" };
        }
        catch (TimeoutException)
        {
            Game?.Dispose();
            Game = null;
            return new AuthResponse { Success = false, Message = "Server không phản hồi, vui lòng thử lại." };
        }
        catch (Exception ex)
        {
            Game?.Dispose();
            Game = null;
            return new AuthResponse { Success = false, Message = $"Không kết nối được Game Server: {ex.Message}" };
        }
    }

    /// <summary>Quên mật khẩu: mở một kết nối tạm tới Server, Server nhờ Firebase gửi email đặt lại mật khẩu.</summary>
    public async Task<AuthResponse> ForgotPasswordAsync(string email)
    {
        if (UseMockServer)
        {
            await Task.Delay(300);
            return new AuthResponse { Success = true, Message = "Đã gửi email đặt lại mật khẩu (giả lập)." };
        }

        GameClient? tmp = null;
        try
        {
            tmp = new GameClient();
            await tmp.ConnectAnonymousAsync(ServerHost, ServerPort);
            var message = await tmp.ForgotPasswordAsync(email);
            return new AuthResponse { Success = true, Message = message };
        }
        catch (ServerErrorException ex)
        {
            return new AuthResponse { Success = false, Message = ex.Message };
        }
        catch (TimeoutException)
        {
            return new AuthResponse { Success = false, Message = "Server không phản hồi, vui lòng thử lại." };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Không kết nối được Game Server: {ex.Message}" };
        }
        finally
        {
            tmp?.Dispose();
        }
    }

    /// <summary>Đăng xuất: Server thu hồi token phiên rồi client đóng kết nối.</summary>
    public async Task LogoutAsync()
    {
        try
        {
            if (Game != null)
                await Game.LogoutAsync();
        }
        catch
        {
            // Bỏ qua lỗi mạng khi đang đóng phiên (Server sẽ tự dọn khi thấy kết nối đóng)
        }
        finally
        {
            Game?.Dispose();
            Game = null;
        }
    }
}
