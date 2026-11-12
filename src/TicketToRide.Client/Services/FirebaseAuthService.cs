using System.Net.Http.Json;
using System.Text.Json;

namespace TicketToRide.Client.Services;

public record FirebaseAuthResult(bool Success, string Message, string? IdToken = null, string? Email = null, string? DisplayName = null);

public class FirebaseAuthService
{
    //
    // Dán Web API Key vào đây
    //
    private const string ApiKey = "Web API Key";
    private static readonly HttpClient _http = new();

    public async Task<FirebaseAuthResult> SignInAsync(string email, string password)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={ApiKey}";
        var payload = new { email, password, returnSecureToken = true };

        return await SendAuthRequestAsync(url, payload);
    }

    public async Task<FirebaseAuthResult> SignUpAsync(string username, string email, string password)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={ApiKey}";
        var payload = new { email, password, returnSecureToken = true };

        var result = await SendAuthRequestAsync(url, payload);
        if (!result.Success || string.IsNullOrEmpty(result.IdToken))
            return result;

        // Cập nhật tên hiển thị (Username) lên hồ sơ Firebase
        var updateUrl = $"https://identitytoolkit.googleapis.com/v1/accounts:update?key={ApiKey}";
        var updatePayload = new { idToken = result.IdToken, displayName = username, returnSecureToken = true };
        await _http.PostAsJsonAsync(updateUrl, updatePayload);

        return result with { DisplayName = username, Message = "Đăng ký tài khoản thành công!" };
    }

    public async Task<FirebaseAuthResult> SendPasswordResetEmailAsync(string email)
    {
        var url = $"https://identitytoolkit.googleapis.com/v1/accounts:sendOobCode?key={ApiKey}";
        var payload = new { requestType = "PASSWORD_RESET", email };

        try
        {
            var response = await _http.PostAsJsonAsync(url, payload);
            var json = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
                return new FirebaseAuthResult(true, "Đã gửi liên kết đặt lại mật khẩu về email của bạn. Vui lòng kiểm tra hộp thư (kể cả mục Spam).");

            return new FirebaseAuthResult(false, ParseFirebaseError(json));
        }
        catch (Exception ex)
        {
            return new FirebaseAuthResult(false, $"Lỗi kết nối mạng: {ex.Message}");
        }
    }

    private static async Task<FirebaseAuthResult> SendAuthRequestAsync(string url, object payload)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(url, payload);
            var json = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                return new FirebaseAuthResult(false, ParseFirebaseError(json));

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var idToken = root.GetProperty("idToken").GetString();
            var email = root.GetProperty("email").GetString();
            var displayName = root.TryGetProperty("displayName", out var dn) ? dn.GetString() : null;

            return new FirebaseAuthResult(true, "Thành công", idToken, email, displayName);
        }
        catch (Exception ex)
        {
            return new FirebaseAuthResult(false, $"Không thể kết nối Firebase: {ex.Message}");
        }
    }

    private static string ParseFirebaseError(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var code = doc.RootElement.GetProperty("error").GetProperty("message").GetString() ?? "";

            return code switch
            {
                "INVALID_LOGIN_CREDENTIALS" or "EMAIL_NOT_FOUND" or "INVALID_PASSWORD"
                    => "Email hoặc mật khẩu không chính xác.",
                "EMAIL_EXISTS"
                    => "Email này đã được sử dụng cho tài khoản khác.",
                "INVALID_EMAIL"
                    => "Định dạng email không hợp lệ.",
                "WEAK_PASSWORD : Password should be at least 6 characters" or "WEAK_PASSWORD"
                    => "Mật khẩu phải có ít nhất 6 ký tự.",
                "TOO_MANY_ATTEMPTS_TRY_LATER"
                    => "Bạn đã thử quá nhiều lần. Vui lòng đợi ít phút rồi thử lại.",
                _ => $"Lỗi xác thực từ Firebase: {code}"
            };
        }
        catch
        {
            return "Đã xảy ra lỗi không xác định từ máy chủ xác thực.";
        }
    }
}