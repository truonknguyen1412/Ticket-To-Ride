using TicketToRide.Shared.Models;

namespace TicketToRide.Shared.Protocol;

public class FirebaseAuthRequest
{
    public string FirebaseIdToken { get; set; } = string.Empty;
    public string? Username { get; set; }
    public bool IsNewRegistration { get; set; }
}

public class AuthResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public Account? Account { get; set; }
    public string? SessionId { get; set; }
}

public class LogoutRequest
{
    public string SessionId { get; set; } = string.Empty;
}

public class LogoutResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}