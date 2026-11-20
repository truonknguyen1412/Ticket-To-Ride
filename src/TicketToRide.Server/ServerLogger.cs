namespace TicketToRide.Server;

public enum LogKind { Info, Step, Ok, Warn, Error }

/// <summary>
/// Ghi nhật ký ra cửa sổ PowerShell/console của máy chạy server: giờ, nhãn luồng, mức (màu), nội dung.
/// Không bao giờ in nguyên token, chỉ in dạng che bớt (<see cref="Mask"/>).
/// </summary>
public sealed class ServerLogger
{
    readonly object _lock = new();

    public void Write(LogKind kind, string tag, string text)
    {
        var (color, mark) = kind switch
        {
            LogKind.Ok => (ConsoleColor.Green, "[OK ]"),
            LogKind.Error => (ConsoleColor.Red, "[LOI]"),
            LogKind.Warn => (ConsoleColor.Yellow, "[CB ]"),
            LogKind.Step => (ConsoleColor.DarkGray, "[...]"),
            _ => (ConsoleColor.Cyan, "[i  ]"),
        };

        lock (_lock)
        {
            var prev = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"{DateTime.Now:HH:mm:ss} ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write($"{tag,-10} ");
            Console.ForegroundColor = color;
            Console.WriteLine($"{mark} {text}");
            Console.ForegroundColor = prev;
        }
    }

    public void Info(string tag, string text) => Write(LogKind.Info, tag, text);
    public void Step(string tag, string text) => Write(LogKind.Step, tag, text);
    public void Ok(string tag, string text) => Write(LogKind.Ok, tag, text);
    public void Warn(string tag, string text) => Write(LogKind.Warn, tag, text);
    public void Error(string tag, string text) => Write(LogKind.Error, tag, text);

    /// <summary>Che token: chỉ hiện 6 ký tự đầu, 4 ký tự cuối và độ dài.</summary>
    public static string Mask(string? token)
    {
        if (string.IsNullOrEmpty(token)) return "(trống)";
        if (token.Length <= 12) return $"***({token.Length})";
        return $"{token[..6]}...{token[^4..]}({token.Length} ký tự)";
    }

    public static string Time(DateTime utc) => utc.ToLocalTime().ToString("HH:mm:ss dd/MM/yyyy");
}
