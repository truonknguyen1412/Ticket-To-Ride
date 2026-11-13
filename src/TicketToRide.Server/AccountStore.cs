using System.Text.Json;

namespace TicketToRide.Server;

public sealed class AccountRecord
{
    public string Uid { get; set; } = "";          // localId của Firebase
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime CreatedUtc { get; set; }
    public DateTime LastLoginUtc { get; set; }
    public int LoginCount { get; set; }
}

/// <summary>
/// Danh sách tài khoản đã biết trên server, lưu ra file JSON (tạm thời, trước khi có SQL Server).
/// Mật khẩu KHÔNG bao giờ nằm ở đây: mật khẩu do Firebase giữ.
/// </summary>
public sealed class AccountStore
{
    readonly object _lock = new();
    readonly string _path;
    readonly Dictionary<string, AccountRecord> _byUid = new();

    public AccountStore(string path)
    {
        _path = path;
        try
        {
            if (!File.Exists(path)) return;
            var list = JsonSerializer.Deserialize<List<AccountRecord>>(File.ReadAllText(path));
            if (list != null) foreach (var a in list) _byUid[a.Uid] = a;
        }
        catch { /* file hỏng: bắt đầu với danh sách rỗng */ }
    }

    public string FilePath => _path;

    public int Count { get { lock (_lock) return _byUid.Count; } }

    public AccountRecord? Find(string uid)
    {
        lock (_lock) return _byUid.TryGetValue(uid, out var a) ? a : null;
    }

    /// <summary>Tạo hồ sơ mới. false nếu uid đã có.</summary>
    public bool TryCreate(string uid, string email, string username, out AccountRecord record)
    {
        lock (_lock)
        {
            if (_byUid.TryGetValue(uid, out var existing)) { record = existing; return false; }
            var now = DateTime.UtcNow;
            record = new AccountRecord
            {
                Uid = uid, Email = email, Username = username,
                CreatedUtc = now, LastLoginUtc = now, LoginCount = 1,
            };
            _byUid[uid] = record;
            Save();
            return true;
        }
    }

    /// <summary>Ghi nhận một lần đăng nhập. Chưa có hồ sơ thì tự tạo (created = true).</summary>
    public AccountRecord RecordLogin(string uid, string email, string fallbackName, out bool created)
    {
        lock (_lock)
        {
            created = false;
            if (!_byUid.TryGetValue(uid, out var a))
            {
                var now = DateTime.UtcNow;
                a = new AccountRecord { Uid = uid, Email = email, Username = fallbackName, CreatedUtc = now };
                _byUid[uid] = a;
                created = true;
            }
            a.Email = email;
            a.LastLoginUtc = DateTime.UtcNow;
            a.LoginCount++;
            Save();
            return a;
        }
    }

    void Save()
    {
        try
        {
            File.WriteAllText(_path, JsonSerializer.Serialize(_byUid.Values.ToList(),
                new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* không ghi được file thì vẫn chạy bằng bộ nhớ */ }
    }
}
