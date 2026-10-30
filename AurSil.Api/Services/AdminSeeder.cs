using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

// Creates the admin account from the "Admin" config section the first time the API starts.
// After that the admin changes the password in the app; the config value is not used again.
public static class AdminSeeder
{
    public static async Task EnsureAdminAsync(IConfiguration config, ILogger logger)
    {
        var userId = config["Admin:UserId"];
        var password = config["Admin:Password"];
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("No Admin:UserId / Admin:Password configured; admin account not created.");
            return;
        }

        try
        {
            await using var db = new SqlConnection(config.GetConnectionString("Aursil"));
            if (await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE UserId = @userId", new { userId }) > 0)
                return;

            await db.ExecuteAsync(
                """
                INSERT INTO Users (UserId, Name, Email, Phone, PasswordHash, Status, Role)
                VALUES (@userId, 'AurSil Admin', @email, '0000000000', @hash, @status, @role)
                """,
                new
                {
                    userId,
                    email = config["Admin:Email"] ?? $"{userId}@aursil.admin",
                    hash = PasswordHasher.Hash(password),
                    status = UserStatus.Active,
                    role = UserRole.Admin,
                });
            logger.LogInformation("Admin account '{UserId}' created.", userId);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Could not create the admin account. Did you run Database\\aursil_schema_v3.sql?");
        }
    }
}
