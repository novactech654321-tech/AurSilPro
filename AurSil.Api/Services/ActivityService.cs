using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

// Writes the audit trail that admins read in the app. A logging failure never breaks the user's request.
public class ActivityService(IConfiguration config, IHttpContextAccessor http, ILogger<ActivityService> logger)
{
    public static readonly TimeZoneInfo India = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    // UTC range of one calendar day in India time
    public static (DateTime From, DateTime To) IndiaDay(DateOnly day)
    {
        var from = TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), India);
        return (from, from.AddDays(1));
    }

    public static DateOnly TodayInIndia() => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, India));

    public async Task LogAsync(string? userId, string action, string? details = null)
    {
        try
        {
            var ip = http.HttpContext?.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                     ?? http.HttpContext?.Connection.RemoteIpAddress?.ToString();
            await using var db = new SqlConnection(_connectionString);
            await db.ExecuteAsync(
                "INSERT INTO ActivityLog (UserId, Action, Details, IpAddress) VALUES (@userId, @action, @details, @ip)",
                new { userId, action, details = details is { Length: > 500 } ? details[..500] : details, ip });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Activity log write failed for {Action}", action);
        }
    }

    public async Task<IReadOnlyList<ActivityDto>> ForDayAsync(DateOnly day, string? search)
    {
        var (from, to) = IndiaDay(day);
        var like = string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";
        await using var db = new SqlConnection(_connectionString);
        var rows = await db.QueryAsync<ActivityDto>(
            """
            SELECT a.Id, a.UserId, u.Name AS UserName, a.Action, a.Details, a.IpAddress, a.CreatedAt
            FROM ActivityLog a LEFT JOIN Users u ON u.UserId = a.UserId
            WHERE a.CreatedAt >= @from AND a.CreatedAt < @to
              AND (@like IS NULL OR a.UserId LIKE @like OR a.Action LIKE @like OR a.Details LIKE @like OR u.Name LIKE @like)
            ORDER BY a.CreatedAt DESC, a.Id DESC
            """, new { from, to, like });
        return rows.ToList();
    }
}
