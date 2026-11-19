-- Chạy trong SQL Server Management Studio (SSMS) khi server game không tự tạo được database/bảng
-- (ví dụ tài khoản Windows của bạn không có quyền CREATE DATABASE).
-- Server game cũng tự chạy các lệnh tương đương khi khởi động, nên bình thường không cần chạy file này.

IF DB_ID(N'TicketToRide') IS NULL
    CREATE DATABASE TicketToRide;
GO

USE TicketToRide;
GO

-- Mật khẩu KHÔNG nằm ở đây: mật khẩu do Firebase Authentication giữ.
-- Cột Uid là localId của Firebase, dùng để nối hồ sơ game với tài khoản Firebase.
IF OBJECT_ID(N'dbo.Accounts', N'U') IS NULL
CREATE TABLE dbo.Accounts (
    Uid          NVARCHAR(128) NOT NULL CONSTRAINT PK_Accounts PRIMARY KEY,
    Email        NVARCHAR(256) NOT NULL,
    Username     NVARCHAR(50)  NOT NULL,
    CreatedUtc   DATETIME2     NOT NULL,
    LastLoginUtc DATETIME2     NOT NULL,
    LoginCount   INT           NOT NULL
);
GO

-- Xem các tài khoản đã đăng ký (dùng khi demo)
SELECT Uid, Email, Username, CreatedUtc, LastLoginUtc, LoginCount
FROM dbo.Accounts
ORDER BY CreatedUtc DESC;
