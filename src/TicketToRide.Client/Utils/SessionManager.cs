namespace TicketToRide.Client.Utils;

public static class SessionManager
{
    public static string? Uid { get; set; }
    public static string? Email { get; set; }
    public static string? DisplayName { get; set; }
    public static string? FirebaseIdToken { get; set; }
    public static string? ResumeToken { get; set; }

    public static bool IsAuthenticated => !string.IsNullOrEmpty(FirebaseIdToken);

    public static void Clear()
    {
        Uid = null;
        Email = null;
        DisplayName = null;
        FirebaseIdToken = null;
        ResumeToken = null;
    }
}