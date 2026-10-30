using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

// Stores and reads the photos / voice clips a user captures in the app (MediaCaptures table).
public class MediaService(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    public async Task<MediaItemDto> SaveAsync(string userId, string kind, string contentType, byte[] data)
    {
        await using var db = new SqlConnection(_connectionString);
        var meta = await db.QuerySingleAsync<MediaMeta>(
            """
            INSERT INTO MediaCaptures (UserId, Kind, ContentType, Data, SizeBytes)
            OUTPUT INSERTED.MediaId, INSERTED.CreatedAt
            VALUES (@userId, @kind, @contentType, @data, @size);
            """,
            new { userId, kind, contentType, data, size = data.Length });
        return new MediaItemDto(meta.MediaId, kind, contentType, data.Length, meta.CreatedAt);
    }

    public async Task<IReadOnlyList<MediaItemDto>> ListAsync(string userId)
    {
        await using var db = new SqlConnection(_connectionString);
        var rows = await db.QueryAsync<MediaItemDto>(
            "SELECT MediaId, Kind, ContentType, SizeBytes, CreatedAt FROM MediaCaptures WHERE UserId = @userId ORDER BY CreatedAt DESC",
            new { userId });
        return rows.ToList();
    }

    // Only returns the file when it belongs to the asking user
    public async Task<MediaBlob?> GetAsync(string userId, long id)
    {
        await using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleOrDefaultAsync<MediaBlob>(
            "SELECT Data, ContentType FROM MediaCaptures WHERE MediaId = @id AND UserId = @userId",
            new { id, userId });
    }

    private class MediaMeta
    {
        public long MediaId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

public class MediaBlob
{
    public byte[] Data { get; set; } = [];
    public string ContentType { get; set; } = "";
}
