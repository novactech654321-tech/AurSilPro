-- AurSil schema v5: stores photos and voice recordings the user captures in the app.
-- Only created when the user taps Take Photo / Record Voice and grants camera / mic permission.
SET QUOTED_IDENTIFIER ON;
GO
USE aursil;
GO

IF OBJECT_ID('dbo.MediaCaptures', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.MediaCaptures
    (
        MediaId     BIGINT IDENTITY(1,1) CONSTRAINT PK_MediaCaptures PRIMARY KEY,
        UserId      NVARCHAR(50)   NOT NULL,
        Kind        NVARCHAR(10)   NOT NULL,          -- 'photo' or 'voice'
        ContentType NVARCHAR(100)  NOT NULL,          -- e.g. image/jpeg, audio/mp4
        Data        VARBINARY(MAX) NOT NULL,          -- the file bytes
        SizeBytes   INT            NOT NULL,
        CreatedAt   DATETIME2      NOT NULL CONSTRAINT DF_MediaCaptures_CreatedAt DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_MediaCaptures_Users FOREIGN KEY (UserId) REFERENCES dbo.Users (UserId)
    );

    CREATE INDEX IX_MediaCaptures_User ON dbo.MediaCaptures (UserId, CreatedAt DESC);
END
GO
