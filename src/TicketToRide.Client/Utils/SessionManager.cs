using TicketToRide.ClientCore;

namespace TicketToRide.Client.Utils;

public static class SessionManager
{
    public static GameClient? Client { get; set; }
    public static string? Uid { get; set; }
    public static string? Email { get; set; }
    public static string? DisplayName { get; set; }
    public static string? FirebaseIdToken { get; set; }
    public static string? ResumeToken { get; set; }

    public static bool IsAuthenticated => !string.IsNullOrEmpty(FirebaseIdToken) && Client != null;

    public static void Clear()
    {
        Client?.Dispose();
        Client = null;
        Uid = null;
        Email = null;
        DisplayName = null;
        FirebaseIdToken = null;
        ResumeToken = null;
    }
}