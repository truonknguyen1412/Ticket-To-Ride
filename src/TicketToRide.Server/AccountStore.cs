using System.Data;
using Microsoft.Data.SqlClient;

namespace TicketToRide.Server;

public sealed class AccountRecord
{
    public string Uid { get; set; } = "";          // localId của Firebase
    public string Email { get; set; } = "";
    public string Username { get; set; } = "";
    public DateTime CreatedVN { get; set; }
    public DateTime LastLoginVN { get; set; }
    public int LoginCount { get; set; }
}

/// <summary>
/// Hồ sơ tài khoản game lưu trong SQL Server (bảng dbo.Accounts).
/// Mật khẩu KHÔNG bao giờ nằm ở đây: mật khẩu do Firebase Authentication giữ, bảng này chỉ nối với Firebase qua cột Uid.
/// Khi server khởi động, <see cref="Initialize"/> tự tạo cơ sở dữ liệu và bảng nếu chưa có.
/// </summary>
public sealed class AccountStore
{
    const string DefaultDatabase = "TicketToRide";
    const int DuplicateKey = 2627;        // vi phạm PRIMARY KEY
    const int DuplicateIndex = 2601;      // vi phạm unique index

    readonly string _connectionString;
    readonly string _databaseName;
    readonly object _initLock = new();
    volatile bool _ready;

    public AccountStore(string connectionString)
    {
        var b = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(b.InitialCatalog)) b.InitialCatalog = DefaultDatabase;

        _databaseName = b.InitialCatalog;
        _connectionString = b.ConnectionString;
        // Chỉ lấy tên máy chủ và tên database để hiển thị, không bao giờ in mật khẩu trong chuỗi kết nối
        Description = $"SQL Server {b.DataSource}, cơ sở dữ liệu {_databaseName}";
    }

    /// <summary>Mô tả ngắn để ghi log (không chứa mật khẩu).</summary>
    public string Description { get; }

    public bool IsReady => _ready;

    // ------------------------------------------------------------ khởi tạo

    /// <summary>Kết nối SQL Server, tạo database và bảng Accounts nếu chưa có. Ném lỗi nếu không kết nối được.</summary>
    public void Initialize()
    {
        lock (_initLock)
        {
            if (_ready) return;

            // Bước 1: tạo database (kết nối vào master). Không đủ quyền thì bỏ qua: nếu database đã có sẵn thì bước 2 vẫn chạy được.
            try
            {
                var master = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };
                using var cn = new SqlConnection(master.ConnectionString);
                cn.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = $"IF DB_ID(@db) IS NULL CREATE DATABASE [{_databaseName.Replace("]", "]]")}]";
                cmd.Parameters.Add("@db", SqlDbType.NVarChar, 128).Value = _databaseName;
                cmd.ExecuteNonQuery();
            }
            catch (SqlException)
            {
            }

            // Bước 2: tạo bảng
            using (var cn = new SqlConnection(_connectionString))
            {
                cn.Open();
                using var cmd = cn.CreateCommand();
                cmd.CommandText = @"
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    Uid          NVARCHAR(128) NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    Email        NVARCHAR(256) NOT NULL,
    Username     NVARCHAR(50)  NOT NULL,
    CreatedVN   DATETIME2     NOT NULL,
    LastLoginVN DATETIME2     NOT NULL,
    LoginCount   INT           NOT NULL
);";
                cmd.ExecuteNonQuery();
            }

            _ready = true;
        }
    }

    void EnsureReady()
    {
        if (!_ready) Initialize();      // SQL Server bật sau khi server game chạy thì lần gọi đầu tiên sẽ tự khởi tạo lại
    }

    /// <summary>Số tài khoản trong bảng Accounts.</summary>
    public int Count()
    {
        EnsureReady();
        using var cn = new SqlConnection(_connectionString);
        cn.Open();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Accounts", cn);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ------------------------------------------------------------ truy vấn

    public async Task<AccountRecord?> FindAsync(string uid, CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        return await SelectAsync(cn, uid, ct);
    }

    /// <summary>Đăng ký: thêm hồ sơ mới. Created = false nếu uid đã có (Record là hồ sơ cũ).</summary>
    public async Task<(bool Created, AccountRecord Record)> TryCreateAsync(
        string uid, string email, string username, CancellationToken ct = default)
    {
        EnsureReady();
        var now = DateTime.UtcNow.AddHours(7);
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        try
        {
            await InsertAsync(cn, uid, email, username, now, ct);
            return (true, NewRecord(uid, email, username, now));
        }
        catch (SqlException ex) when (ex.Number == DuplicateKey || ex.Number == DuplicateIndex)
        {
            var existing = await SelectAsync(cn, uid, ct);
            return (false, existing ?? new AccountRecord { Uid = uid, Email = email, Username = username });
        }
    }

    /// <summary>Đăng nhập: tăng LoginCount, cập nhật LastLoginVN. Chưa có hồ sơ thì tự tạo (Created = true).</summary>
    public async Task<(AccountRecord Record, bool Created)> RecordLoginAsync(
        string uid, string email, string fallbackName, CancellationToken ct = default)
    {
        EnsureReady();
        var now = DateTime.UtcNow.AddHours(7);
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var updated = await UpdateLoginAsync(cn, uid, email, now, ct);
            if (updated != null) return (updated, false);

            try
            {
                await InsertAsync(cn, uid, email, fallbackName, now, ct);
                return (NewRecord(uid, email, fallbackName, now), true);
            }
            catch (SqlException ex) when (ex.Number == DuplicateKey || ex.Number == DuplicateIndex)
            {
                // có kết nối khác vừa tạo hồ sơ này: quay lại bước cập nhật
            }
        }
        throw new InvalidOperationException("Không ghi nhận được lần đăng nhập vào SQL Server");
    }

    // ------------------------------------------------------------ SQL

    static AccountRecord NewRecord(string uid, string email, string username, DateTime now) => new()
    {
        Uid = uid, Email = email, Username = username,
        CreatedVN = now, LastLoginVN = now, LoginCount = 1,
    };

    static async Task InsertAsync(SqlConnection cn, string uid, string email, string username, DateTime now, CancellationToken ct)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText =
            "INSERT INTO dbo.Accounts (Uid, Email, Username, CreatedVN, LastLoginVN, LoginCount) " +
            "VALUES (@uid, @email, @name, @now, @now, 1)";
        cmd.Parameters.Add("@uid", SqlDbType.NVarChar, 128).Value = uid;
        cmd.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = email;
        cmd.Parameters.Add("@name", SqlDbType.NVarChar, 50).Value = username;
        cmd.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    static async Task<AccountRecord?> UpdateLoginAsync(SqlConnection cn, string uid, string email, DateTime now, CancellationToken ct)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText =
            "UPDATE dbo.Accounts SET Email = @email, LastLoginVN = @now, LoginCount = LoginCount + 1 " +
            "OUTPUT inserted.Uid, inserted.Email, inserted.Username, inserted.CreatedVN, inserted.LastLoginVN, inserted.LoginCount " +
            "WHERE Uid = @uid";
        cmd.Parameters.Add("@uid", SqlDbType.NVarChar, 128).Value = uid;
        cmd.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = email;
        cmd.Parameters.Add("@now", SqlDbType.DateTime2).Value = now;

        using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? ReadRecord(rd) : null;
    }

    static async Task<AccountRecord?> SelectAsync(SqlConnection cn, string uid, CancellationToken ct)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText =
            "SELECT Uid, Email, Username, CreatedVN, LastLoginVN, LoginCount FROM dbo.Accounts WHERE Uid = @uid";
        cmd.Parameters.Add("@uid", SqlDbType.NVarChar, 128).Value = uid;

        using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? ReadRecord(rd) : null;
    }

    static AccountRecord ReadRecord(SqlDataReader rd) => new()
    {
        Uid = rd.GetString(0),
        Email = rd.GetString(1),
        Username = rd.GetString(2),
        CreatedVN = DateTime.SpecifyKind(rd.GetDateTime(3), DateTimeKind.Utc),
        LastLoginVN = DateTime.SpecifyKind(rd.GetDateTime(4), DateTimeKind.Utc),
        LoginCount = rd.GetInt32(5),
    };
}
