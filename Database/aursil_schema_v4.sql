-- AurSil v4: personal AI assistant (MCP) connector keys
-- Run: sqlcmd -S "(localdb)\MSSQLLocalDB" -E -C -i Database\aursil_schema_v4.sql
-- Only the SHA-256 hash of a key is stored; the key itself is shown to the user once.

SET QUOTED_IDENTIFIER ON;
USE aursil;
GO

IF COL_LENGTH('dbo.Users', 'McpKeyHash') IS NULL
    ALTER TABLE dbo.Users ADD McpKeyHash CHAR(64) NULL, McpKeyCreatedAt DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_Users_McpKeyHash')
    CREATE UNIQUE INDEX UX_Users_McpKeyHash ON dbo.Users (McpKeyHash) WHERE McpKeyHash IS NOT NULL;
GO
