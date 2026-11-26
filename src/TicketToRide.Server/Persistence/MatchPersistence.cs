using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;

namespace TicketToRide.Server;

/// <summary>
/// Ghi đồng bộ vòng đời phòng, ván đấu, người chơi, snapshot và idempotency (reqId) xuống SQL Server.
/// </summary>
public sealed class SqlMatchPersistence : IMatchPersistence
{
    static readonly string[] TrainPalette = ["Red", "Blue", "Green", "Yellow", "Black"];

    readonly AccountStore _accounts;
    readonly string _connectionString;
    readonly ServerLogger? _log;

    public SqlMatchPersistence(AccountStore accounts, string connectionString, ServerLogger? log = null)
    {
        _accounts = accounts;
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            builder.InitialCatalog = "TicketToRideVietNam";
        _connectionString = builder.ConnectionString;
        _log = log;
    }

    public async Task<Guid?> EnsureRoomCreatedAsync(
        string roomCode,
        Session host,
        byte maxPlayers = 4,
        CancellationToken ct = default)
    {
        if (host.DbAccountId == null)
        {
            _log?.Warn("DB", $"Phòng {roomCode}: host '{host.PlayerName}' chưa có DbAccountId (chế độ --dev/ẩn danh), bỏ qua ghi DB phòng chờ");
            return null;
        }

        _accounts.Initialize();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        Guid roomId;
        using (var cmd = cn.CreateCommand())
        {
            cmd.Transaction = tx;
            cmd.CommandText = @"
SELECT TOP 1 RoomId
FROM dbo.Rooms
WHERE RoomName = @roomCode AND Status IN ('Waiting', 'Playing')
ORDER BY CreatedAtUtc DESC;";
            cmd.Parameters.Add("@roomCode", SqlDbType.NVarChar, 100).Value = roomCode;
            var existing = await cmd.ExecuteScalarAsync(ct);
            if (existing != null && existing != DBNull.Value)
            {
                roomId = (Guid)existing;
            }
            else
            {
                cmd.Parameters.Clear();
                cmd.CommandText = @"
INSERT INTO dbo.Rooms (HostAccountId, RoomName, Status, MaxPlayers)
OUTPUT inserted.RoomId
VALUES (@hostId, @roomCode, 'Waiting', @maxPlayers);";
                cmd.Parameters.Add("@hostId", SqlDbType.UniqueIdentifier).Value = host.DbAccountId.Value;
                cmd.Parameters.Add("@roomCode", SqlDbType.NVarChar, 100).Value = roomCode;
                cmd.Parameters.Add("@maxPlayers", SqlDbType.TinyInt).Value = maxPlayers;
                roomId = (Guid)(await cmd.ExecuteScalarAsync(ct))!;
            }
        }

        using (var cmdMember = cn.CreateCommand())
        {
            cmdMember.Transaction = tx;
            cmdMember.CommandText = @"
MERGE dbo.RoomPlayers AS target
USING (SELECT @roomId AS RoomId, @accId AS AccountId) AS src
ON target.RoomId = src.RoomId AND target.AccountId = src.AccountId
WHEN MATCHED THEN
    UPDATE SET MemberStatus = 'Joined'
WHEN NOT MATCHED THEN
    INSERT (RoomId, AccountId, MemberStatus) VALUES (@roomId, @accId, 'Joined');";
            cmdMember.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId;
            cmdMember.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = host.DbAccountId.Value;
            await cmdMember.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        _log?.Ok("DB", $"Đã đồng bộ phòng {roomCode} -> dbo.Rooms (RoomId={roomId}) & dbo.RoomPlayers");
        return roomId;
    }

    public async Task SyncRoomPlayerAsync(
        Guid? roomId,
        string roomCode,
        Session member,
        string memberStatus = "Joined",
        CancellationToken ct = default)
    {
        if (roomId == null || member.DbAccountId == null) return;

        _accounts.Initialize();
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
        cmd.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId.Value;
        cmd.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = member.DbAccountId.Value;
        cmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = memberStatus;
        await cmd.ExecuteNonQueryAsync(ct);
        _log?.Info("DB", $"Phòng {roomCode}: cập nhật RoomPlayers ({member.PlayerName}) -> {memberStatus}");
    }

    public async Task UpdateRoomStatusAsync(
        Guid? roomId,
        string roomCode,
        string status,
        CancellationToken ct = default)
    {
        if (roomId == null) return;

        _accounts.Initialize();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);

        using var cmd = cn.CreateCommand();
        cmd.CommandText = "UPDATE dbo.Rooms SET Status = @status WHERE RoomId = @roomId;";
        cmd.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId.Value;
        cmd.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = status;
        await cmd.ExecuteNonQueryAsync(ct);
        _log?.Info("DB", $"Phòng {roomCode}: cập nhật dbo.Rooms.Status -> {status}");
    }

    public async Task<Guid?> StartMatchAsync(
        Guid? existingRoomId,
        string roomCode,
        IReadOnlyList<Member> members,
        object? initialSnapshot = null,
        CancellationToken ct = default)
    {
        var dbPlayers = members
            .Where(m => m.Session.DbAccountId != null)
            .Select((m, idx) => (
                AccountId: m.Session.DbAccountId!.Value,
                TrainColor: TrainPalette[idx % TrainPalette.Length]))
            .ToList();

        if (dbPlayers.Count == 0)
        {
            _log?.Warn("DB", $"Phòng {roomCode}: không có thành viên nào có DbAccountId, bỏ qua tạo bản ghi dbo.Matches");
            return null;
        }

        _accounts.Initialize();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        Guid? roomId = existingRoomId;
        if (roomId == null)
        {
            var hostAccId = members.FirstOrDefault(m => m.Session.DbAccountId != null)?.Session.DbAccountId ?? dbPlayers[0].AccountId;
            using var cmdRoom = cn.CreateCommand();
            cmdRoom.Transaction = tx;
            cmdRoom.CommandText = @"
INSERT INTO dbo.Rooms (HostAccountId, RoomName, Status, MaxPlayers)
OUTPUT inserted.RoomId
VALUES (@hostId, @roomCode, 'Playing', @maxPlayers);";
            cmdRoom.Parameters.Add("@hostId", SqlDbType.UniqueIdentifier).Value = hostAccId;
            cmdRoom.Parameters.Add("@roomCode", SqlDbType.NVarChar, 100).Value = roomCode;
            cmdRoom.Parameters.Add("@maxPlayers", SqlDbType.TinyInt).Value = (byte)Math.Max(4, members.Count);
            roomId = (Guid)(await cmdRoom.ExecuteScalarAsync(ct))!;
        }
        else
        {
            using var cmdRoom = cn.CreateCommand();
            cmdRoom.Transaction = tx;
            cmdRoom.CommandText = "UPDATE dbo.Rooms SET Status = 'Playing' WHERE RoomId = @roomId;";
            cmdRoom.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId.Value;
            await cmdRoom.ExecuteNonQueryAsync(ct);
        }

        Guid matchId;
        using (var cmdMatch = cn.CreateCommand())
        {
            cmdMatch.Transaction = tx;
            cmdMatch.CommandText = @"
INSERT INTO dbo.Matches (RoomId, Status)
OUTPUT inserted.MatchId
VALUES (@roomId, 'InProgress');";
            cmdMatch.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId.Value;
            matchId = (Guid)(await cmdMatch.ExecuteScalarAsync(ct))!;
        }

        foreach (var p in dbPlayers)
        {
            using var cmdPlayer = cn.CreateCommand();
            cmdPlayer.Transaction = tx;
            cmdPlayer.CommandText = @"
INSERT INTO dbo.MatchPlayers (MatchId, AccountId, TrainColor, RouteScore, RemainingTrains, PlayerStatus)
VALUES (@matchId, @accId, @color, 0, 45, 'Active');";
            cmdPlayer.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
            cmdPlayer.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = p.AccountId;
            cmdPlayer.Parameters.Add("@color", SqlDbType.VarChar, 20).Value = p.TrainColor;
            await cmdPlayer.ExecuteNonQueryAsync(ct);
        }

        if (initialSnapshot != null)
        {
            var json = JsonSerializer.Serialize(initialSnapshot);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(json));
            using var cmdSnap = cn.CreateCommand();
            cmdSnap.Transaction = tx;
            cmdSnap.CommandText = @"
INSERT INTO dbo.GameStateSnapshot (MatchId, StateVersion, SnapshotSchemaVersion, SnapshotData, ContentHash)
VALUES (@matchId, 0, 1, @data, @hash);";
            cmdSnap.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId;
            cmdSnap.Parameters.Add("@data", SqlDbType.NVarChar, -1).Value = json;
            cmdSnap.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
            await cmdSnap.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        _log?.Ok("DB", $"Bắt đầu trận tại phòng {roomCode}: MatchId={matchId}, {dbPlayers.Count} người chơi lưu vào dbo.MatchPlayers");
        return matchId;
    }

    public async Task SaveMatchStepAsync(
        string roomCode,
        int stateVersion,
        string playerId,
        string? reqId,
        string cachedReply,
        object snapshot)
    {
        await Task.CompletedTask;
    }

    public async Task SaveMatchStepWithMatchIdAsync(
        Guid? matchId,
        Guid? accountId,
        string actionType,
        int stateVersion,
        string? reqId,
        bool succeeded,
        string resultJson,
        object? snapshot,
        CancellationToken ct = default)
    {
        if (matchId == null) return;

        _accounts.Initialize();
        var snapJson = JsonSerializer.Serialize(snapshot ?? new { stateVersion });
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(snapJson));

        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        using (var cmdSnap = cn.CreateCommand())
        {
            cmdSnap.Transaction = tx;
            cmdSnap.CommandText = @"
INSERT INTO dbo.GameStateSnapshot (MatchId, StateVersion, SnapshotSchemaVersion, SnapshotData, ContentHash)
VALUES (@matchId, @ver, 1, @data, @hash);";
            cmdSnap.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId.Value;
            cmdSnap.Parameters.Add("@ver", SqlDbType.BigInt).Value = (long)stateVersion;
            cmdSnap.Parameters.Add("@data", SqlDbType.NVarChar, -1).Value = snapJson;
            cmdSnap.Parameters.Add("@hash", SqlDbType.VarBinary, 32).Value = hash;
            await cmdSnap.ExecuteNonQueryAsync(ct);
        }

        if (!string.IsNullOrWhiteSpace(reqId) && accountId != null)
        {
            using var cmdReq = cn.CreateCommand();
            cmdReq.Transaction = tx;
            cmdReq.CommandText = @"
INSERT INTO dbo.ProcessedRequests (MatchId, AccountId, ReqId, ActionType, ResultStatus, ResultJson, StateVersion)
VALUES (@matchId, @accId, @reqId, @action, @status, @json, @ver);";
            cmdReq.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId.Value;
            cmdReq.Parameters.Add("@accId", SqlDbType.UniqueIdentifier).Value = accountId.Value;
            cmdReq.Parameters.Add("@reqId", SqlDbType.VarChar, 64).Value = reqId;
            cmdReq.Parameters.Add("@action", SqlDbType.VarChar, 30).Value = actionType;
            cmdReq.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = succeeded ? "Succeeded" : "Rejected";
            cmdReq.Parameters.Add("@json", SqlDbType.NVarChar, -1).Value = resultJson;
            cmdReq.Parameters.Add("@ver", SqlDbType.BigInt).Value = (long)stateVersion;
            await cmdReq.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
    }

    public async Task CompleteMatchAsync(
        Guid? matchId,
        Guid? roomId,
        string roomCode,
        string matchStatus = "Completed",
        string roomStatus = "Closed",
        CancellationToken ct = default)
    {
        if (matchId == null && roomId == null) return;

        _accounts.Initialize();
        await using var cn = new SqlConnection(_connectionString);
        await cn.OpenAsync(ct);
        await using var tx = (SqlTransaction)await cn.BeginTransactionAsync(ct);

        if (matchId != null)
        {
            using var cmdMatch = cn.CreateCommand();
            cmdMatch.Transaction = tx;
            cmdMatch.CommandText = @"
UPDATE dbo.Matches
SET Status = @status, EndedAtUtc = SYSUTCDATETIME()
WHERE MatchId = @matchId;";
            cmdMatch.Parameters.Add("@matchId", SqlDbType.UniqueIdentifier).Value = matchId.Value;
            cmdMatch.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = matchStatus;
            await cmdMatch.ExecuteNonQueryAsync(ct);
        }

        if (roomId != null)
        {
            using var cmdRoom = cn.CreateCommand();
            cmdRoom.Transaction = tx;
            cmdRoom.CommandText = "UPDATE dbo.Rooms SET Status = @status WHERE RoomId = @roomId;";
            cmdRoom.Parameters.Add("@roomId", SqlDbType.UniqueIdentifier).Value = roomId.Value;
            cmdRoom.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = roomStatus;
            await cmdRoom.ExecuteNonQueryAsync(ct);
        }

        await tx.CommitAsync(ct);
        _log?.Info("DB", $"Phòng {roomCode}: hoàn tất trận (MatchId={matchId}) -> MatchStatus={matchStatus}, RoomStatus={roomStatus}");
    }
}