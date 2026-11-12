using System.Net.Sockets;
using System.Text.Json;
using TicketToRide.Shared.Models;
using TicketToRide.Shared.Protocol;

namespace TicketToRide.Client.Services;

public class TcpClientService
{
    // Đặt = true để test giao diện ngay khi Tuấn chưa chạy Server.
    // Khi Server chạy thật ở 127.0.0.1:5000, đổi thành false.
    public static bool UseMockServer { get; set; } = true;

    private TcpClient? _tcpClient;
    private NetworkStream? _stream;

    public async Task<AuthResponse> AuthenticateWithServerAsync(string firebaseIdToken, string email, string? displayName)
    {
        if (UseMockServer)
        {
            await Task.Delay(300); // Giả lập độ trễ mạng
            var fallbackName = !string.IsNullOrWhiteSpace(displayName) ? displayName : email.Split('@')[0];

            return new AuthResponse
            {
                Success = true,
                Message = "Đăng nhập thành công (Chế độ giả lập Server)",
                SessionId = Guid.NewGuid().ToString(),
                Account = new Account
                {
                    AccountId = Guid.NewGuid(),
                    Username = fallbackName
                }
            };
        }

        try
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync("127.0.0.1", 5000);
            _stream = _tcpClient.GetStream();

            var req = new FirebaseAuthRequest { FirebaseIdToken = firebaseIdToken };
            await PacketFraming.SendAsync(_stream, PacketType.AuthRequest, req);

            var responseEnvelope = await PacketFraming.ReceiveAsync(_stream);

            if (responseEnvelope?.Type == PacketType.AuthResponse)
            {
                return JsonSerializer.Deserialize<AuthResponse>(responseEnvelope.PayloadJson)
                       ?? new AuthResponse { Success = false, Message = "Dữ liệu phản hồi rỗng." };
            }

            return new AuthResponse { Success = false, Message = "Gói tin phản hồi từ Server không hợp lệ." };
        }
        catch (Exception ex)
        {
            return new AuthResponse { Success = false, Message = $"Không kết nối được Game Server: {ex.Message}" };
        }
    }

    public async Task LogoutAsync()
    {
        try
        {
            if (!UseMockServer && _stream != null && _tcpClient?.Connected == true)
            {
                await PacketFraming.SendAsync(_stream, PacketType.LogoutRequest, new LogoutRequest());
            }
        }
        catch
        {
            // Bỏ qua lỗi mạng khi đang đóng phiên
        }
        finally
        {
            _stream?.Close();
            _tcpClient?.Close();
            _stream = null;
            _tcpClient = null;
        }
    }
}