using TicketToRide.ClientCore;
using TicketToRide.Shared.Models;
using TicketToRide.Shared.Protocol;

namespace TicketToRide.Client.Services;

/// <summary>
/// Cầu nối giữa giao diện (AuthForm, LobbyForm) và Server thật.
/// Bên trong dùng GameClient (ClientCore) nói giao thức HELLO/WELCOME của Server.
/// Đăng nhập Firebase vẫn làm ở client; Server hiện chỉ nhận tên hiển thị qua HELLO.
/// </summary>
public class TcpClientService
{
    // true = chạy giao diện không cần Server (đăng nhập giả). Đã đổi false để nối Server thật.
    public static bool UseMockServer { get; set; } = false;

    public static string ServerHost { get; set; } = "127.0.0.1";
    public static int ServerPort { get; set; } = 5000;

    private const int MaxNameLength = 20; // khớp Protocol.MaxPlayerNameLength của Server

    /// <summary>Kết nối thật tới Server. Lobby dùng cái này để tạo/vào phòng (CreateRoomAsync, JoinRoomAsync...).</summary>
    public GameClient? Game { get; private set; }

    public async Task<AuthResponse> AuthenticateWithServerAsync(string firebaseIdToken, string email, string? displayName)
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

            await Game.ConnectAsync(ServerHost, ServerPort, name);

            return new AuthResponse
            {
                Success = true,
                Message = "Đã kết nối Game Server.",
                SessionId = Game.PlayerId,
                Account = new Account
                {
                    AccountId = Guid.TryParse(Game.PlayerId, out var id) ? id : Guid.NewGuid(),
                    Username = name
                }
            };
        }
        catch (ServerErrorException ex)
        {
            Game?.Dispose();
            Game = null;
            return new AuthResponse { Success = false, Message = $"Server từ chối ({ex.Code}): {ex.Message}" };
        }
        catch (Exception ex)
        {
            Game?.Dispose();
            Game = null;
            return new AuthResponse { Success = false, Message = $"Không kết nối được Game Server: {ex.Message}" };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (Game != null)
                await Game.DisconnectAsync();
        }
        catch
        {
            // Bỏ qua lỗi mạng khi đang đóng phiên
        }
        finally
        {
            Game?.Dispose();
            Game = null;
        }
    }
}
