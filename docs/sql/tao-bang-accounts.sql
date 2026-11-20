IF DB_ID(N'TicketToRide') IS NULL
    CREATE DATABASE TicketToRide;
GO

USE TicketToRide;
GO

-- 1. Bảng Tài khoản (đồng bộ cột CreatedVN / LastLoginVN với AccountStore.cs)
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    Uid          NVARCHAR(128) NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    Email        NVARCHAR(256) NOT NULL,
    Username     NVARCHAR(50)  NOT NULL,
    CreatedVN    DATETIME2     NOT NULL,
    LastLoginVN  DATETIME2     NOT NULL,
    LoginCount   INT           NOT NULL
);
GO

-- 2. Bảng Lịch sử Ván đấu
IF OBJECT_ID(N'dbo.Matches', N'U') IS NULL
CREATE TABLE dbo.Matches (
    MatchId      UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Matches PRIMARY KEY,
    RoomCode     NVARCHAR(16)     NOT NULL,
    Status       NVARCHAR(32)     NOT NULL, -- InProgress, Completed, Abandoned, RecoveryPending
    MapVersion   NVARCHAR(16)     NOT NULL DEFAULT N'1.0',
    StartedAtVN  DATETIME2        NOT NULL,
    EndedAtVN    DATETIME2        NULL
);
GO

-- 3. Bảng Người chơi tham gia từng Ván
IF OBJECT_ID(N'dbo.MatchPlayers', N'U') IS NULL
CREATE TABLE dbo.MatchPlayers (
    MatchPlayerId   UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MatchPlayers PRIMARY KEY,
    MatchId         UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_MatchPlayers_Matches FOREIGN KEY REFERENCES dbo.Matches(MatchId),
    AccountUid      NVARCHAR(128)    NOT NULL CONSTRAINT FK_MatchPlayers_Accounts FOREIGN KEY REFERENCES dbo.Accounts(Uid),
    SeatIndex       INT              NOT NULL,
    TrainColor      NVARCHAR(20)     NOT NULL,
    RouteScore      INT              NOT NULL DEFAULT 0,
    RemainingTrains INT              NOT NULL DEFAULT 45,
    PlayerStatus    NVARCHAR(32)     NOT NULL, -- Active, Disconnected, Left, Finished
    JoinedAtVN      DATETIME2        NOT NULL
);
GO

-- 4. Bảng Kết quả & Xếp hạng sau Ván đấu
IF OBJECT_ID(N'dbo.MatchResults', N'U') IS NULL
CREATE TABLE dbo.MatchResults (
    MatchId            UNIQUEIDENTIFIER NOT NULL,
    MatchPlayerId      UNIQUEIDENTIFIER NOT NULL,
    AccountUid         NVARCHAR(128)    NOT NULL,
    RoutePoints        INT              NOT NULL,
    TicketPoints       INT              NOT NULL,
    LongestRouteBonus  INT              NOT NULL DEFAULT 0,
    TotalScore         INT              NOT NULL,
    RankPosition       INT              NOT NULL,
    IsWinner           BIT              NOT NULL,
    CONSTRAINT PK_MatchResults PRIMARY KEY (MatchId, MatchPlayerId),
    CONSTRAINT FK_MatchResults_Matches FOREIGN KEY (MatchId) REFERENCES dbo.Matches(MatchId),
    CONSTRAINT FK_MatchResults_Players FOREIGN KEY (MatchPlayerId) REFERENCES dbo.MatchPlayers(MatchPlayerId),
    CONSTRAINT FK_MatchResults_Accounts FOREIGN KEY (AccountUid) REFERENCES dbo.Accounts(Uid)
);
GO

-- 5. Bảng Snapshot trạng thái ván chơi (dùng khi rớt mạng & khôi phục ván)
IF OBJECT_ID(N'dbo.GameStateSnapshot', N'U') IS NULL
CREATE TABLE dbo.GameStateSnapshot (
    SnapshotId    BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_GameStateSnapshot PRIMARY KEY,
    MatchId       UNIQUEIDENTIFIER     NOT NULL CONSTRAINT FK_Snapshot_Matches FOREIGN KEY REFERENCES dbo.Matches(MatchId),
    StateVersion  BIGINT               NOT NULL,
    SnapshotData  NVARCHAR(MAX)        NOT NULL,
    CreatedAtVN   DATETIME2            NOT NULL
);
GO

-- 6. Bảng Chống xử lý lặp lệnh hành động (Idempotency theo ReqId)
IF OBJECT_ID(N'dbo.ProcessedRequests', N'U') IS NULL
CREATE TABLE dbo.ProcessedRequests (
    MatchId       UNIQUEIDENTIFIER NOT NULL,
    AccountUid    NVARCHAR(128)    NOT NULL,
    ReqId         NVARCHAR(64)     NOT NULL,
    ActionType    NVARCHAR(32)     NOT NULL,
    IsAccepted    BIT              NOT NULL,
    ResponseJson  NVARCHAR(MAX)    NULL,
    ProcessedAtVN DATETIME2        NOT NULL,
    CONSTRAINT PK_ProcessedRequests PRIMARY KEY (MatchId, AccountUid, ReqId)
);
GO