namespace TicketToRide.Server;

public sealed class ServerOptions
{
    public int Port { get; init; } = 5000;

    /// <summary>true (cờ --dev): cho phép HELLO chỉ bằng tên, không cần Firebase. Chỉ dùng để chạy TestClient.</summary>
    public bool AllowAnonymous { get; init; }

    /// <summary>Firebase Web API key, dùng để xác minh idToken và gửi email đặt lại mật khẩu. null = chưa cấu hình.</summary>
    public string? FirebaseApiKey { get; init; }

    /// <summary>Chuỗi kết nối SQL Server mặc định tới database TicketToRideVietNam.</summary>
    public const string DefaultSqlConnection =
        @"Server=localhost;Database=TicketToRideVietNam;Integrated Security=True;TrustServerCertificate=True;";

    /// <summary>Chuỗi kết nối SQL Server. Đổi bằng --sql=... hoặc biến SQLSERVER_CONNECTION.</summary>
    public string SqlConnectionString { get; init; } = DefaultSqlConnection;
}