using System.Security.Cryptography;
using System.Text;
using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

// Personal keys that let an AI assistant (ChatGPT, Claude) act on one user's account through the MCP connector.
// The key is part of the connector URL; only its SHA-256 hash is stored so a database leak exposes no keys.
public class McpKeyService(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    public async Task<string> CreateAsync(string userId)
    {
        var key = "aurs_" + Base64UrlEncode(RandomNumberGenerator.GetBytes(24));
        await using var db = new SqlConnection(_connectionString);
        await db.ExecuteAsync("UPDATE Users SET McpKeyHash = @hash, McpKeyCreatedAt = SYSUTCDATETIME() WHERE UserId = @userId",
            new { hash = Hash(key), userId });
        return key;
    }

    public async Task RevokeAsync(string userId)
    {
        await using var db = new SqlConnection(_connectionString);
        await db.ExecuteAsync("UPDATE Users SET McpKeyHash = NULL, McpKeyCreatedAt = NULL WHERE UserId = @userId", new { userId });
    }

    public async Task<DateTime?> CreatedAtAsync(string userId)
    {
        await using var db = new SqlConnection(_connectionString);
        return await db.ExecuteScalarAsync<DateTime?>("SELECT McpKeyCreatedAt FROM Users WHERE UserId = @userId", new { userId });
    }

    // Only active accounts can be used through the connector
    public async Task<UserRow?> FindUserAsync(string? key)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith("aurs_"))
            return null;
        await using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleOrDefaultAsync<UserRow>(
            "SELECT * FROM Users WHERE McpKeyHash = @hash AND Status = 'Active'", new { hash = Hash(key) });
    }

    public string ConnectorUrl(HttpRequest request, string key) => $"{PublicBaseUrl(request)}/mcp/{key}";

    // The public address of the API: Mcp:PublicBaseUrl if set, otherwise the address this request came in on (ngrok sends X-Forwarded-*)
    public string PublicBaseUrl(HttpRequest request)
    {
        var baseUrl = config["Mcp:PublicBaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
            var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.Value;
            baseUrl = $"{scheme}://{host}";
        }
        return baseUrl.TrimEnd('/');
    }

    private static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    private static string Base64UrlEncode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
