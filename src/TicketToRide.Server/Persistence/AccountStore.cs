using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.SqlClient;

namespace TicketToRide.Server;

public sealed class AccountRecord
{
    public Guid AccountId { get; set; }
    public string Username { get; set; } = "";
    public string? PasswordHash { get; set; }
    public int Reputation { get; set; } = 100;
    public int RankPoints { get; set; } = 0;
    public DateTime CreatedAtUtc { get; set; }
    public string Uid { get; set; } = "";          // localId của Firebase
    public string Email { get; set; } = "";
    public DateTime? LastLoginVN { get; set; }
    public int LoginCount { get; set; }
    public DateTime? CreatedVN { get; set; }
}

public sealed record CityRow(string CityCode, string CityName, short PositionX, short PositionY);
public sealed record RouteRow(string RouteCode, string CityACode, string CityBCode, byte ParallelIndex, byte RouteLength, string? ColorCode);
public sealed record DestinationTicketRow(string TicketCode, string CityACode, string CityBCode, int Points);

/// <summary>
/// Lớp truy cập SQL Server khớp 100% schema của Ticket-To-Ride.sql (database TicketToRideVietNam).
/// </summary>
public sealed class AccountStore
{
    const string DefaultDatabase = "TicketToRideVietNam";
    const int DuplicateKey = 2627;        // vi phạm PRIMARY KEY
    const int DuplicateIndex = 2601;      // vi phạm UNIQUE INDEX (ví dụ trùng Username)

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
        Description = $"SQL Server {b.DataSource}, cơ sở dữ liệu {_databaseName}";
    }

    public string Description { get; }
    public bool IsReady => _ready;

    // ------------------------------------------------------------ khởi tạo & kiểm tra kết nối

    /// <summary>Kiểm tra kết nối tới SQL Server và xác nhận bảng dbo.Accounts trong TicketToRideVietNam đã sẵn sàng.</summary>
    public void Initialize()
    {
        lock (_initLock)
        {
            if (_ready) return;

            using var cn = new SqlConnection(_connectionString);
            cn.Open();

            using var cmd = cn.CreateCommand();
            cmd.CommandText = "SELECT OBJECT_ID(N'dbo.Accounts', N'U')";
            var objId = cmd.ExecuteScalar();
            if (objId == null || objId == DBNull.Value)
                throw new InvalidOperationException(
                    $"Database [{_databaseName}] chưa có bảng dbo.Accounts. Hãy chạy file Ticket-To-Ride.sql trên SQL Server trước.");

            _ready = true;
        }
    }

    void EnsureReady()
    {
        if (!_ready) Initialize();
    }

    public int Count()
    {
        EnsureReady();
        using var cn = new SqlConnection(_connectionString);
        cn.Open();
        using var cmd = new SqlCommand("SELECT COUNT(*) FROM dbo.Accounts", cn);
        return Convert.ToInt32(cmd.ExecuteScalar());
    }

    // ------------------------------------------------------------ dbo.Accounts

    public async Task<AccountRecord?> FindByUidAsync(string uid, CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        return await SelectByUidAsync(cn, uid, ct);
    }

    public async Task<AccountRecord?> FindByAccountIdAsync(Guid accountId, CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT AccountId, Username, PasswordHash, Reputation, RankPoints, CreatedAtUtc, Uid, Email, LastLoginVN, LoginCount, CreatedVN
FROM dbo.Accounts
WHERE AccountId = @id";
        cmd.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = accountId;

        using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? ReadAccount(rd) : null;
    }

    /// <summary>Đăng ký: thêm hồ sơ mới vào dbo.Accounts. Created = false nếu Uid đã tồn tại.</summary>
    public async Task<(bool Created, AccountRecord Record)> TryCreateAsync(
        string uid, string email, string username, CancellationToken ct = default)
    {
        EnsureReady();
        var utcNow = DateTime.UtcNow;
        var vnNow = utcNow.AddHours(7);

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        var existingByUid = await SelectByUidAsync(cn, uid, ct);
        if (existingByUid != null)
            return (false, existingByUid);

        try
        {
            var inserted = await InsertAccountAsync(cn, uid, email, username, utcNow, vnNow, ct);
            return (true, inserted);
        }
        catch (SqlException ex) when (ex.Number == DuplicateKey || ex.Number == DuplicateIndex)
        {
            // Nếu trùng Uid thì trả về hồ sơ cũ; nếu trùng Username (UQ_Accounts_Username) thì gắn thêm hậu tố ngẫu nhiên
            var existing = await SelectByUidAsync(cn, uid, ct);
            if (existing != null) return (false, existing);

            var uniqueName = MakeFallbackUsername(username);
            var inserted = await InsertAccountAsync(cn, uid, email, uniqueName, utcNow, vnNow, ct);
            return (true, inserted);
        }
    }

    /// <summary>Đăng nhập: tăng LoginCount, cập nhật LastLoginVN. Chưa có hồ sơ thì tự tạo (Created = true).</summary>
    public async Task<(AccountRecord Record, bool Created)> RecordLoginAsync(
        string uid, string email, string fallbackName, CancellationToken ct = default)
    {
        EnsureReady();
        var utcNow = DateTime.UtcNow;
        var vnNow = utcNow.AddHours(7);

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var updated = await UpdateLoginAsync(cn, uid, email, vnNow, ct);
            if (updated != null) return (updated, false);

            try
            {
                var inserted = await InsertAccountAsync(cn, uid, email, fallbackName, utcNow, vnNow, ct);
                return (inserted, true);
            }
            catch (SqlException ex) when (ex.Number == DuplicateKey || ex.Number == DuplicateIndex)
            {
                var existing = await SelectByUidAsync(cn, uid, ct);
                if (existing != null) continue;

                // Trùng Username (UQ_Accounts_Username) -> đổi tên dự phòng để không chặn đăng nhập
                var uniqueName = MakeFallbackUsername(fallbackName);
                var inserted = await InsertAccountAsync(cn, uid, email, uniqueName, utcNow, vnNow, ct);
                return (inserted, true);
            }
        }
        throw new InvalidOperationException("Không ghi nhận được lần đăng nhập vào SQL Server.");
    }

    static async Task<AccountRecord> InsertAccountAsync(
        SqlConnection cn, string uid, string email, string username, DateTime utcNow, DateTime vnNow, CancellationToken ct)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.Accounts (Username, PasswordHash, Reputation, RankPoints, CreatedAtUtc, Uid, Email, LastLoginVN, LoginCount, CreatedVN)
OUTPUT inserted.AccountId, inserted.Username, inserted.PasswordHash, inserted.Reputation, inserted.RankPoints,
       inserted.CreatedAtUtc, inserted.Uid, inserted.Email, inserted.LastLoginVN, inserted.LoginCount, inserted.CreatedVN
VALUES (@name, NULL, 100, 0, @utcNow, @uid, @email, @vnNow, 1, @vnNow)";
        cmd.Parameters.Add("@name", SqlDbType.NVarChar, 50).Value = username;
        cmd.Parameters.Add("@utcNow", SqlDbType.DateTime2, 0).Value = utcNow;
        cmd.Parameters.Add("@uid", SqlDbType.NVarChar, 128).Value = uid;
        cmd.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = email;
        cmd.Parameters.Add("@vnNow", SqlDbType.DateTime2, 0).Value = vnNow;

        using var rd = await cmd.ExecuteReaderAsync(ct);
        await rd.ReadAsync(ct);
        return ReadAccount(rd);
    }

    static async Task<AccountRecord?> UpdateLoginAsync(
        SqlConnection cn, string uid, string email, DateTime vnNow, CancellationToken ct)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
UPDATE dbo.Accounts
SET Email = @email, LastLoginVN = @vnNow, LoginCount = LoginCount + 1
OUTPUT inserted.AccountId, inserted.Username, inserted.PasswordHash, inserted.Reputation, inserted.RankPoints,
       inserted.CreatedAtUtc, inserted.Uid, inserted.Email, inserted.LastLoginVN, inserted.LoginCount, inserted.CreatedVN
WHERE Uid = @uid";
        cmd.Parameters.Add("@uid", SqlDbType.NVarChar, 128).Value = uid;
        cmd.Parameters.Add("@email", SqlDbType.NVarChar, 256).Value = email;
        cmd.Parameters.Add("@vnNow", SqlDbType.DateTime2, 0).Value = vnNow;

        using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? ReadAccount(rd) : null;
    }

    static async Task<AccountRecord?> SelectByUidAsync(SqlConnection cn, string uid, CancellationToken ct)
    {
        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT AccountId, Username, PasswordHash, Reputation, RankPoints, CreatedAtUtc, Uid, Email, LastLoginVN, LoginCount, CreatedVN
FROM dbo.Accounts
WHERE Uid = @uid";
        cmd.Parameters.Add("@uid", SqlDbType.NVarChar, 128).Value = uid;

        using var rd = await cmd.ExecuteReaderAsync(ct);
        return await rd.ReadAsync(ct) ? ReadAccount(rd) : null;
    }

    static AccountRecord ReadAccount(SqlDataReader rd) => new()
    {
        AccountId = rd.GetGuid(0),
        Username = rd.GetString(1),
        PasswordHash = rd.IsDBNull(2) ? null : rd.GetString(2),
        Reputation = rd.GetInt32(3),
        RankPoints = rd.GetInt32(4),
        CreatedAtUtc = DateTime.SpecifyKind(rd.GetDateTime(5), DateTimeKind.Utc),
        Uid = rd.IsDBNull(6) ? "" : rd.GetString(6),
        Email = rd.IsDBNull(7) ? "" : rd.GetString(7),
        LastLoginVN = rd.IsDBNull(8) ? null : rd.GetDateTime(8),
        LoginCount = rd.GetInt32(9),
        CreatedVN = rd.IsDBNull(10) ? null : rd.GetDateTime(10),
    };

    static string MakeFallbackUsername(string baseName)
    {
        var suffix = "_" + RandomNumberGenerator.GetInt32(1000, 9999);
        var maxBase = Math.Max(1, 50 - suffix.Length);
        return (baseName.Length > maxBase ? baseName[..maxBase] : baseName) + suffix;
    }

    // ------------------------------------------------------------ dbo.Rooms & dbo.RoomPlayers

    public async Task<Guid> CreateRoomAsync(Guid hostAccountId, string roomName, byte maxPlayers = 4, CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        Guid roomId;
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO dbo.Rooms (HostAccountId, RoomName, Status, MaxPlayers)
OUTPUT inserted.RoomId
VALUES (@hostId, @roomName, 'Waiting', @maxPlayers)";
            cmd.Parameters.Add("@hostId", SqlDbType.UniqueIdentifier).Value = hostAccountId;
            cmd.Parameters.Add("@roomName", SqlDbType.NVarChar, 100).Value = roomName;
            cmd.Parameters.Add("@maxPlayers", SqlDbType.TinyInt).Value = maxPlayers;
            roomId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }

        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO dbo.RoomPlayers (RoomId, AccountId, MemberStatus)
VALUES (@roomId, @accId, 'Joined')";
            cmd.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
            cmd.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = hostAccountId;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return roomId;
    }

    public async Task UpsertRoomPlayerAsync(Guid roomId, Guid accountId, string memberStatus = "Joined", CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
MERGE dbo.RoomPlayers AS target
USING (SELECT @roomId AS RoomId, @accId AS AccountId) AS src
ON target.RoomId = src.RoomId AND target.AccountId = src.AccountId
WHEN MATCHED THEN
    UPDATE SET MemberStatus = @status
WHEN NOT MATCHED THEN
    INSERT (RoomId, AccountId, MemberStatus) VALUES (@roomId, @accId, @status);";
        cmd.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
        cmd.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = accountId;
        cmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = memberStatus;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateRoomStatusAsync(Guid roomId, string status, CancellationToken ct = default)
    {
        // status hợp lệ theo CK_Rooms_Status: 'Waiting' | 'Playing' | 'Closed'
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Rooms SET Status = @status WHERE RoomId = @roomId";
        cmd.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
        cmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = status;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ------------------------------------------------------------ dbo.Matches, dbo.MatchPlayers, dbo.MatchResults

    public async Task<Guid> CreateMatchAsync(
        Guid roomId,
        IEnumerable<(Guid AccountId, string TrainColor)> players,
        CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        Guid matchId;
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO dbo.Matches (RoomId, Status)
OUTPUT inserted.MatchId
VALUES (@roomId, 'InProgress')";
            cmd.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
            matchId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
        }

        foreach (var (accountId, trainColor) in players)
        {
            using var cmd = cn.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO dbo.MatchPlayers (MatchId, AccountId, TrainColor, RouteScore, RemainingTrains, PlayerStatus)
VALUES (@matchId, @accId, @color, 0, 45, 'Active')";
            cmd.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
            cmd.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = accountId;
            cmd.Parameters.Add("@color", SqlDbType.VarChar, 20).Value = trainColor;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        using (var cmdRoom = cn.CreateCommand())
        {
            cmdRoom.Transaction = tx;
            cmdRoom.CommandText = "UPDATE dbo.Rooms SET Status = 'Playing' WHERE RoomId = @roomId";
            cmdRoom.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
            await cmdRoom.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        return matchId;
    }

    public async Task SaveMatchResultAsync(
        Guid matchId,
        Guid accountId,
        int placement,
        int finalScore,
        int completedTickets,
        int routeScore,
        int longestRouteBonus,
        int rankPointsChange,
        int reputationChange,
        CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
INSERT INTO dbo.MatchResults
    (MatchId, AccountId, Placement, FinalScore, CompletedTickets, RouteScore, LongestRouteBonus, RankPointsChange, ReputationChange)
VALUES
    (@matchId, @accId, @placement, @finalScore, @tickets, @routeScore, @longestBonus, @rankDelta, @repDelta)";
            cmd.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
            cmd.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = accountId;
            cmd.Parameters.Add("@placement", SqlDbType.Int).Value = placement;
            cmd.Parameters.Add("@finalScore", SqlDbType.Int).Value = finalScore;
            cmd.Parameters.Add("@tickets", SqlDbType.Int).Value = completedTickets;
            cmd.Parameters.Add("@routeScore", SqlDbType.Int).Value = routeScore;
            cmd.Parameters.Add("@longestBonus", SqlDbType.Int).Value = longestRouteBonus;
            cmd.Parameters.Add("@rankDelta", SqlDbType.Int).Value = rankPointsChange;
            cmd.Parameters.Add("@repDelta", SqlDbType.Int).Value = reputationChange;
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // Cộng/trừ điểm Rank và Uy tín (Reputation giới hạn 0..100 theo CK_Accounts_Reputation)
        using (var cmdAcc = cn.CreateCommand())
        {
            cmdAcc.Transaction = tx;
            cmdAcc.CommandText = @"
UPDATE dbo.Accounts
SET RankPoints = CASE WHEN RankPoints + @rankDelta < 0 THEN 0 ELSE RankPoints + @rankDelta END,
    Reputation = CASE
        WHEN Reputation + @repDelta < 0 THEN 0
        WHEN Reputation + @repDelta > 100 THEN 100
        ELSE Reputation + @repDelta
    END
WHERE AccountId = @accId";
            cmdAcc.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = accountId;
            cmdAcc.Parameters.Add("@rankDelta", SqlDbType.Int).Value = rankPointsChange;
            cmdAcc.Parameters.Add("@repDelta", SqlDbType.Int).Value = reputationChange;
            await cmdAcc.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task CompleteMatchAsync(Guid matchId, string status = "Completed", CancellationToken ct = default)
    {
        // status hợp lệ theo CK_Matches_Status: 'InProgress' | 'Completed' | 'Abandoned' | 'RecoveryPending'
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Matches SET Status = @status, EndedAtUtc = SYSUTCDATETIME() WHERE MatchId = @matchId";
        cmd.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
        cmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = status;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ------------------------------------------------------------ dbo.GameStateSnapshot & dbo.ProcessedRequests

    public async Task SaveSnapshotAsync(
        Guid matchId, long stateVersion, string snapshotJson, int schemaVersion = 1, CancellationToken ct = default)
    {
        EnsureReady();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson)); // 32 bytes đúng CK_GameStateSnapshot_Hash

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.GameStateSnapshot (MatchId, StateVersion, SnapshotSchemaVersion, SnapshotData, ContentHash)
VALUES (@matchId, @ver, @schemaVer, @data, @hash)";
        cmd.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
        cmd.Parameters.Add("@ver", SqlDbType.BigInt).Value = stateVersion;
        cmd.Parameters.Add("@schemaVer", SqlDbType.Int).Value = schemaVersion;
        cmd.Parameters.Add("@data", SqlDbType.NVarChar, -1).Value = snapshotJson;
        cmd.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task SaveProcessedRequestAsync(
        Guid matchId, Guid accountId, string reqId, string actionType, bool succeeded, string resultJson, long? stateVersion = null, CancellationToken ct = default)
    {
        EnsureReady();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
INSERT INTO dbo.ProcessedRequests (MatchId, AccountId, ReqId, ActionType, ResultStatus, ResultJson, StateVersion)
VALUES (@matchId, @accId, @reqId, @action, @status, @json, @ver)";
        cmd.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
        cmd.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = accountId;
        cmd.Parameters.Add("@reqId", SqlDbType.VarChar, 64).Value = reqId;
        cmd.Parameters.Add("@action", SqlDbType.VarChar, 30).Value = actionType;
        cmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = succeeded ? "Succeeded" : "Rejected";
        cmd.Parameters.Add("@json", SqlDbType.NVarChar, -1).Value = resultJson;
        cmd.Parameters.Add("@ver", SqlDbType.BigInt).Value = (object?)stateVersion ?? DBNull.Value;
        await cmd.ExecuteNonQueryAsync(ct);
    }

    // ------------------------------------------------------------ Dữ liệu bản đồ tĩnh (Cities, Routes, DestinationTickets)

    public async Task<List<CityRow>> LoadCitiesAsync(CancellationToken ct = default)
    {
        EnsureReady();
        var list = new List<CityRow>();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = @"
SELECT c.CityCode, c.CityName, p.PositionX, p.PositionY
FROM dbo.Cities c
INNER JOIN dbo.CityMapPositions p ON c.CityCode = p.CityCode";

        using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new CityRow(rd.GetString(0), rd.GetString(1), rd.GetInt16(2), rd.GetInt16(3)));
        return list;
    }

    public async Task<List<RouteRow>> LoadRoutesAsync(CancellationToken ct = default)
    {
        EnsureReady();
        var list = new List<RouteRow>();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT RouteCode, CityACode, CityBCode, ParallelIndex, RouteLength, ColorCode FROM dbo.Routes";

        using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new RouteRow(rd.GetString(0), rd.GetString(1), rd.GetString(2), rd.GetByte(3), rd.GetByte(4), rd.IsDBNull(5) ? null : rd.GetString(5)));
        return list;
    }

    public async Task<List<DestinationTicketRow>> LoadDestinationTicketsAsync(CancellationToken ct = default)
    {
        EnsureReady();
        var list = new List<DestinationTicketRow>();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "SELECT TicketCode, CityACode, CityBCode, Points FROM dbo.DestinationTickets";

        using var rd = await cmd.ExecuteReaderAsync(ct);
        while (await rd.ReadAsync(ct))
            list.Add(new DestinationTicketRow(rd.GetString(0), rd.GetString(1), rd.GetString(2), rd.GetInt32(3)));
        return list;
    }
}