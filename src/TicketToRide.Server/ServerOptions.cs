namespace TicketToRide.Server;

public sealed class ServerOptions
{
    public int Port { get; init; } = 5000;

    /// <summary>true (cờ --dev): cho phép HELLO chỉ bằng tên, không cần Firebase. Chỉ dùng để chạy TestClient.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>Firebase Web API key, dùng để xác minh idToken và gửi email đặt lại mật khẩu. null = chưa cấu hình.</summary>
    public string? FirebaseApiKey { get; init; }

    public string AccountsFile { get; init; } = Path.Combine(AppContext.BaseDirectory, "accounts.json");
}
