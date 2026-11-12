using TicketToRide.Shared.Models;

namespace TicketToRide.Client.Utils;

public static class SessionManager
{
    public static Account? CurrentAccount { get; set; }
    public static string? Email { get; set; }
    public static string? FirebaseIdToken { get; set; }
    public static String? SessionId { get; set; }

    public static bool IsLoggedIn => CurrentAccount != null && !string.IsNullOrEmpty(FirebaseIdToken);

    public static void Clear()
    {
        CurrentAccount = null;
        Email = null;
        FirebaseIdToken = null;
        SessionId = null;
    }
}