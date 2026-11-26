using System.Net.Http.Json;
using System.Text.Json;
using TicketToRide.Shared.Net;

namespace TicketToRide.Server;

public sealed record IdentityUser(string Uid, string Email, string? DisplayName, bool EmailVerified, DateTimeOffset? TokenExpiresAt);

public sealed record IdentityOutcome(bool Ok, string Code, string Message, IdentityUser? User = null)
{
    public static IdentityOutcome Success(string message, IdentityUser? user = null) => new(true, "OK", message, user);
    public static IdentityOutcome Failure(string code, string message) => new(false, code, message);
}

/// <summary>Nơi xác minh danh tính. Hiện dùng Firebase; tách interface để sau này thay được.</summary>
public interface IIdentityProvider
{
    /// <summary>Kiểm tra idToken do client gửi lên có thật, còn hạn, chưa bị thu hồi.</summary>
    Task<IdentityOutcome> VerifyIdTokenAsync(string idToken, CancellationToken ct);

    /// <summary>Nhờ Firebase gửi email đặt lại mật khẩu.</summary>
    Task<IdentityOutcome> SendPasswordResetAsync(string email, CancellationToken ct);
}

/// <summary>
/// Gọi REST của Firebase Authentication (accounts:lookup, accounts:sendOobCode).
/// Dùng accounts:lookup để Firebase tự kiểm tra chữ ký/hạn/thu hồi của idToken, nên server không cần projectId hay thư viện JWT.
/// </summary>
public sealed class FirebaseIdentityProvider : IIdentityProvider
{
    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };
    readonly string _apiKey;

    public FirebaseIdentityProvider(string apiKey) => _apiKey = apiKey;

    string Url(string method) =>
        $"https://identitytoolkit.googleapis.com/v1/accounts:{method}?key={Uri.EscapeDataString(_apiKey)}";

    public async Task<IdentityOutcome> VerifyIdTokenAsync(string idToken, CancellationToken ct)
    {
        try
        {
            using var resp = await Http.PostAsJsonAsync(Url("lookup"), new { idToken }, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode) return MapVerifyError(ReadErrorMessage(json));

            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("users", out var users) || users.GetArrayLength() == 0)
                return IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "Tài khoản không tồn tại trên Firebase");

            var u = users[0];
            if (u.TryGetProperty("disabled", out var dis) && dis.ValueKind == JsonValueKind.True)
                return IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "Tài khoản đã bị vô hiệu hóa");

            var uid = u.TryGetProperty("localId", out var l) ? l.GetString() ?? "" : "";
            if (uid.Length == 0)
                return IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "Firebase không trả về uid");

            var email = u.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
            var name = u.TryGetProperty("displayName", out var d) ? d.GetString() : null;
            var verified = u.TryGetProperty("emailVerified", out var ev) && ev.ValueKind == JsonValueKind.True;

            return IdentityOutcome.Success("idToken hợp lệ",
                new IdentityUser(uid, email, name, verified, ReadJwtExpiry(idToken)));
        }
        catch (TaskCanceledException)
        {
            return IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, "Firebase không phản hồi (quá 10 giây)");
        }
        catch (HttpRequestException ex)
        {
            return IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, $"Server không gọi được Firebase: {ex.Message}");
        }
        catch (JsonException)
        {
            return IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, "Phản hồi Firebase không đọc được");
        }
    }

    public async Task<IdentityOutcome> SendPasswordResetAsync(string email, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Url("sendOobCode"))
            {
                Content = JsonContent.Create(new { requestType = "PASSWORD_RESET", email }),
            };
            req.Headers.Add("X-Firebase-Locale", "vi");      // email đặt lại bằng tiếng Việt

            using var resp = await Http.SendAsync(req, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            if (resp.IsSuccessStatusCode)
                return IdentityOutcome.Success("Đã gửi liên kết đặt lại mật khẩu. Hãy kiểm tra hộp thư (kể cả mục Spam).");

            var code = ReadErrorMessage(json);
            return code switch
            {
                "EMAIL_NOT_FOUND" => IdentityOutcome.Failure(ErrorCodes.EmailInvalid, "Email này chưa đăng ký tài khoản."),
                "INVALID_EMAIL" or "MISSING_EMAIL" => IdentityOutcome.Failure(ErrorCodes.EmailInvalid, "Định dạng email không hợp lệ."),
                "TOO_MANY_ATTEMPTS_TRY_LATER" or "RESET_PASSWORD_EXCEED_LIMIT"
                    => IdentityOutcome.Failure(ErrorCodes.TooManyAttempts, "Gửi quá nhiều lần, vui lòng đợi ít phút."),
                _ when IsKeyError(code) => IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, "Firebase API key của server không hợp lệ"),
                _ => IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, $"Firebase từ chối yêu cầu: {code}"),
            };
        }
        catch (TaskCanceledException)
        {
            return IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, "Firebase không phản hồi (quá 10 giây)");
        }
        catch (HttpRequestException ex)
        {
            return IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, $"Server không gọi được Firebase: {ex.Message}");
        }
    }

    static IdentityOutcome MapVerifyError(string code) => code switch
    {
        "INVALID_ID_TOKEN" => IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "idToken không hợp lệ"),
        "TOKEN_EXPIRED" => IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "idToken đã hết hạn, hãy đăng nhập lại"),
        "CREDENTIAL_TOO_OLD_LOGIN_AGAIN" => IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "Phiên Firebase quá cũ, hãy đăng nhập lại"),
        "USER_NOT_FOUND" => IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "Tài khoản không tồn tại trên Firebase"),
        "USER_DISABLED" => IdentityOutcome.Failure(ErrorCodes.TokenInvalid, "Tài khoản đã bị vô hiệu hóa"),
        _ when IsKeyError(code) => IdentityOutcome.Failure(ErrorCodes.AuthUnavailable, "Firebase API key của server không hợp lệ"),
        _ => IdentityOutcome.Failure(ErrorCodes.TokenInvalid, $"Firebase từ chối idToken: {code}"),
    };

    static bool IsKeyError(string code) =>
        code.Contains("API key", StringComparison.OrdinalIgnoreCase) || code.StartsWith("API_KEY", StringComparison.Ordinal);

    static string ReadErrorMessage(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var msg = doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";
            // Firebase đôi khi gửi "WEAK_PASSWORD : ..." -> chỉ lấy phần mã
            var i = msg.IndexOf(" : ", StringComparison.Ordinal);
            return i > 0 ? msg[..i] : msg;
        }
        catch { return "UNKNOWN"; }
    }

    /// <summary>Đọc "exp" trong payload JWT (chỉ để hiển thị; việc kiểm tra thật do Firebase lo).</summary>
    static DateTimeOffset? ReadJwtExpiry(string jwt)
    {
        try
        {
            var parts = jwt.Split('.');
            if (parts.Length != 3) return null;
            var b64 = parts[1].Replace('-', '+').Replace('_', '/');
            b64 = b64.PadRight(b64.Length + (4 - b64.Length % 4) % 4, '=');
            using var doc = JsonDocument.Parse(Convert.FromBase64String(b64));
            return doc.RootElement.TryGetProperty("exp", out var exp) && exp.TryGetInt64(out var secs)
                ? DateTimeOffset.FromUnixTimeSeconds(secs)
                : null;
        }
        catch { return null; }
    }
}
