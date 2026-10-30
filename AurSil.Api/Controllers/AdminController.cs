using System.Globalization;
using System.Text;
using AurSil.Api.Models;
using AurSil.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Controllers;

[ApiController]
[Authorize(Roles = UserRole.Admin)]
[Route("api/admin")]
public class AdminController(IConfiguration config, PriceService prices, StoreService store, ActivityService activity) : ControllerBase
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var (from, to) = ActivityService.IndiaDay(ActivityService.TodayInIndia());
        await using var db = new SqlConnection(_connectionString);

        var stock = (await db.QueryAsync<(string Metal, decimal StockGrams, DateTime UpdatedAt)>(
            "SELECT Metal, StockGrams, UpdatedAt FROM Inventory")).ToList();
        var held = (await db.QueryAsync<(string Metal, decimal Grams)>(
            "SELECT Metal, SUM(Grams) FROM Holdings GROUP BY Metal")).ToList();
        var today = (await db.QueryAsync<(string Metal, string Side, decimal Grams, decimal Total, int Count)>(
            """
            SELECT Metal, Side, SUM(Grams), SUM(Total), COUNT(*) FROM Orders
            WHERE Status = 'Completed' AND CompletedAt >= @from AND CompletedAt < @to
            GROUP BY Metal, Side
            """, new { from, to })).ToList();

        var metals = Metals.All.Select(metal =>
        {
            var s = stock.FirstOrDefault(r => r.Metal == metal);
            var rate = prices.Get(metal)?.BuyRate ?? 0;
            return new MetalStockDto(metal, s.StockGrams,
                held.FirstOrDefault(h => h.Metal == metal).Grams,
                today.Where(t => t.Metal == metal && t.Side == "Buy").Sum(t => t.Grams),
                today.Where(t => t.Metal == metal && t.Side == "Sell").Sum(t => t.Grams),
                rate, Math.Round(s.StockGrams * rate, 2), s.UpdatedAt);
        }).ToList();

        var users = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM Users WHERE Role = @role", new { role = UserRole.User });
        var logins = await db.ExecuteScalarAsync<int>(
            "SELECT COUNT(DISTINCT UserId) FROM ActivityLog WHERE Action = @action AND CreatedAt >= @from AND CreatedAt < @to",
            new { action = Activity.Login, from, to });

        return Ok(new DashboardDto(metals, users, logins, today.Sum(t => t.Count),
            today.Where(t => t.Side == "Buy").Sum(t => t.Total), today.Where(t => t.Side == "Sell").Sum(t => t.Total)));
    }

    [HttpPut("inventory/{metal}")]
    public async Task<IActionResult> UpdateStock(string metal, StockRequest req)
    {
        if (!Metals.IsValid(metal))
            return BadRequest(new ApiMessage("Unknown metal."));
        var grams = Math.Round(req.Grams, 4);
        var set = req.Mode == "set";
        if (set && grams < 0)
            return BadRequest(new ApiMessage("Stock cannot be negative."));

        await using var db = new SqlConnection(_connectionString);
        var before = await store.StockAsync(metal);
        var after = set ? grams : before + grams;
        if (after < 0)
            return BadRequest(new ApiMessage($"Only {before:0.####} g in stock; cannot remove {-grams:0.####} g."));

        await db.ExecuteAsync("UPDATE Inventory SET StockGrams = @after, UpdatedAt = SYSUTCDATETIME() WHERE Metal = @metal", new { after, metal });
        await activity.LogAsync(User.UserId(), Activity.StockUpdated,
            $"{Metals.Display(metal)}: {before:0.####} g → {after:0.####} g ({(set ? "set" : grams >= 0 ? $"+{grams:0.####} g" : $"{grams:0.####} g")})");
        return Ok(new ApiMessage($"{Metals.Display(metal)} stock is now {after:0.####} g."));
    }

    [HttpGet("offers")]
    public async Task<IActionResult> Offers()
    {
        await using var db = new SqlConnection(_connectionString);
        return Ok(await db.QueryAsync<OfferDto>("SELECT Id, Title, Message, IsActive, ExpiresAt, CreatedAt FROM Offers ORDER BY CreatedAt DESC"));
    }

    [HttpPost("offers")]
    public async Task<IActionResult> CreateOffer(OfferRequest req)
    {
        if (OfferError(req) is { } error)
            return BadRequest(new ApiMessage(error));

        await using var db = new SqlConnection(_connectionString);
        var offer = await db.QuerySingleAsync<OfferDto>(
            """
            INSERT INTO Offers (Title, Message, IsActive, ExpiresAt)
            OUTPUT inserted.Id, inserted.Title, inserted.Message, inserted.IsActive, inserted.ExpiresAt, inserted.CreatedAt
            VALUES (@title, @message, @IsActive, @expires)
            """,
            new { title = req.Title.Trim(), message = req.Message.Trim(), req.IsActive, expires = Expiry(req.ValidDays) });
        await activity.LogAsync(User.UserId(), Activity.OfferCreated,
            $"#{offer.Id} \"{offer.Title}\"" + (offer.ExpiresAt is null ? "" : $", valid {req.ValidDays} day(s)"));
        return Ok(offer);
    }

    [HttpPut("offers/{id:int}")]
    public async Task<IActionResult> UpdateOffer(int id, OfferRequest req)
    {
        if (OfferError(req) is { } error)
            return BadRequest(new ApiMessage(error));

        await using var db = new SqlConnection(_connectionString);
        var offer = await db.QuerySingleOrDefaultAsync<OfferDto>(
            """
            UPDATE Offers SET Title = @title, Message = @message, IsActive = @IsActive,
                ExpiresAt = CASE WHEN @keepExpiry = 1 THEN ExpiresAt ELSE @expires END
            OUTPUT inserted.Id, inserted.Title, inserted.Message, inserted.IsActive, inserted.ExpiresAt, inserted.CreatedAt
            WHERE Id = @id
            """,
            new { id, title = req.Title.Trim(), message = req.Message.Trim(), req.IsActive, keepExpiry = req.ValidDays is null, expires = Expiry(req.ValidDays) });
        if (offer is null)
            return NotFound(new ApiMessage("Offer not found."));

        await activity.LogAsync(User.UserId(), Activity.OfferUpdated, $"#{id} \"{offer.Title}\" {(offer.IsActive ? "shown" : "hidden")}");
        return Ok(offer);
    }

    [HttpDelete("offers/{id:int}")]
    public async Task<IActionResult> DeleteOffer(int id)
    {
        await using var db = new SqlConnection(_connectionString);
        var title = await db.QuerySingleOrDefaultAsync<string>("DELETE FROM Offers OUTPUT deleted.Title WHERE Id = @id", new { id });
        if (title is null)
            return NotFound(new ApiMessage("Offer not found."));
        await activity.LogAsync(User.UserId(), Activity.OfferDeleted, $"#{id} \"{title}\"");
        return Ok(new ApiMessage("Offer deleted."));
    }

    [HttpGet("discounts")]
    public async Task<IActionResult> Discounts() => Ok(await store.DiscountsAsync());

    [HttpPut("discounts/{metal}")]
    public async Task<IActionResult> UpdateDiscount(string metal, DiscountRequest req)
    {
        if (!Metals.IsValid(metal))
            return BadRequest(new ApiMessage("Unknown metal."));
        if (req.Percent is < 0 or > 50)
            return BadRequest(new ApiMessage("Discount must be between 0% and 50%."));

        var percent = Math.Round(req.Percent, 2);
        await using var db = new SqlConnection(_connectionString);
        await db.ExecuteAsync(
            """
            MERGE Discounts AS d USING (SELECT @metal AS Metal) AS s ON d.Metal = s.Metal
            WHEN MATCHED THEN UPDATE SET DiscountPercent = @percent, IsActive = @IsActive, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (Metal, DiscountPercent, IsActive) VALUES (@metal, @percent, @IsActive);
            """, new { metal, percent, req.IsActive });
        await activity.LogAsync(User.UserId(), Activity.DiscountUpdated,
            $"{Metals.Display(metal)}: {percent:0.##}% off buy price, {(req.IsActive ? "ON" : "OFF")}");
        return Ok(new DiscountDto(metal, percent, req.IsActive));
    }

    // date is yyyy-MM-dd in India time; defaults to today
    [HttpGet("activity")]
    public async Task<IActionResult> ActivityLog(string? date = null, string? search = null)
    {
        if (ParseDay(date) is not { } day)
            return BadRequest(new ApiMessage("Date must be in yyyy-MM-dd format."));
        return Ok(await activity.ForDayAsync(day, search));
    }

    // The day's log as a downloadable CSV file, e.g. activity-2026-10-28.csv
    [HttpGet("activity/file")]
    public async Task<IActionResult> ActivityFile(string? date = null)
    {
        if (ParseDay(date) is not { } day)
            return BadRequest(new ApiMessage("Date must be in yyyy-MM-dd format."));

        var rows = (await activity.ForDayAsync(day, null)).Reverse();
        var csv = new StringBuilder("Time (IST),User ID,Name,Action,Details,IP Address\r\n");
        foreach (var r in rows)
        {
            var time = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc), ActivityService.India);
            csv.AppendJoin(',', Csv(time.ToString("yyyy-MM-dd HH:mm:ss")), Csv(r.UserId), Csv(r.UserName), Csv(r.Action), Csv(r.Details), Csv(r.IpAddress))
               .Append("\r\n");
        }
        // BOM so Excel opens the ₹ sign correctly
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv; charset=utf-8", $"activity-{day:yyyy-MM-dd}.csv");
    }

    private static DateOnly? ParseDay(string? date) =>
        string.IsNullOrWhiteSpace(date) ? ActivityService.TodayInIndia()
        : DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    private static string Csv(string? value) =>
        value is null ? "" : value.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    private static DateTime? Expiry(int? validDays) => validDays is > 0 ? DateTime.UtcNow.AddDays(validDays.Value) : null;

    private static string? OfferError(OfferRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || req.Title.Trim().Length > 100)
            return "Offer title is required (up to 100 characters).";
        if (string.IsNullOrWhiteSpace(req.Message) || req.Message.Trim().Length > 500)
            return "Offer message is required (up to 500 characters).";
        if (req.ValidDays is < 0 or > 365)
            return "Validity must be between 1 and 365 days, or empty for no end date.";
        return null;
    }
}
