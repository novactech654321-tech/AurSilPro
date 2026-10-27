using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

public enum OtpResult { Valid, Invalid, Expired, TooManyAttempts }

public class OtpService(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;
    public int ExpiryMinutes { get; } = config.GetValue("Otp:ExpiryMinutes", 10);
    private readonly int _maxAttempts = config.GetValue("Otp:MaxAttempts", 5);

    public async Task<string> CreateAsync(string email, string purpose)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        await using var db = new SqlConnection(_connectionString);
        // Only the newest code is valid
        await db.ExecuteAsync(
            "UPDATE OtpCodes SET IsUsed = 1 WHERE Email = @email AND Purpose = @purpose AND IsUsed = 0",
            new { email, purpose });
        await db.ExecuteAsync(
            "INSERT INTO OtpCodes (Email, CodeHash, Purpose, ExpiresAt) VALUES (@email, @hash, @purpose, @expires)",
            new { email, hash = HashCode(code), purpose, expires = DateTime.UtcNow.AddMinutes(ExpiryMinutes) });

        return code;
    }

    public async Task<OtpResult> VerifyAsync(string email, string purpose, string code)
    {
        await using var db = new SqlConnection(_connectionString);
        var otp = await db.QuerySingleOrDefaultAsync<(int Id, string CodeHash, DateTime ExpiresAt, int Attempts)>(
            """
            SELECT TOP 1 Id, CodeHash, ExpiresAt, Attempts FROM OtpCodes
            WHERE Email = @email AND Purpose = @purpose AND IsUsed = 0
            ORDER BY CreatedAt DESC
            """,
            new { email, purpose });

        if (otp.Id == 0)
            return OtpResult.Invalid;
        if (otp.ExpiresAt < DateTime.UtcNow)
            return OtpResult.Expired;
        if (otp.Attempts >= _maxAttempts)
            return OtpResult.TooManyAttempts;

        var matches = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(HashCode(code.Trim())), Encoding.ASCII.GetBytes(otp.CodeHash));

        if (!matches)
        {
            await db.ExecuteAsync("UPDATE OtpCodes SET Attempts = Attempts + 1 WHERE Id = @Id", new { otp.Id });
            return OtpResult.Invalid;
        }

        await db.ExecuteAsync("UPDATE OtpCodes SET IsUsed = 1 WHERE Id = @Id", new { otp.Id });
        return OtpResult.Valid;
    }

    private static string HashCode(string code) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code)));
}
