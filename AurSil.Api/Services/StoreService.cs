using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

// Admin-controlled settings that the user app reads: per-metal discounts, offers and vault stock
public class StoreService(IConfiguration config)
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    public async Task<IReadOnlyList<DiscountDto>> DiscountsAsync()
    {
        await using var db = new SqlConnection(_connectionString);
        var rows = await db.QueryAsync<DiscountDto>("SELECT Metal, DiscountPercent AS [Percent], IsActive FROM Discounts");
        return Metals.All.Select(m => rows.FirstOrDefault(r => r.Metal == m) ?? new DiscountDto(m, 0, false)).ToList();
    }

    // The percent currently taken off the buy rate, 0 when the discount is switched off
    public async Task<decimal> ActiveDiscountAsync(string metal) =>
        (await DiscountsAsync()).FirstOrDefault(d => d.Metal == metal) is { IsActive: true } d ? d.Percent : 0;

    public static decimal DiscountedRate(decimal buyRate, decimal percent) =>
        percent > 0 ? Math.Round(buyRate * (1 - percent / 100), 2) : buyRate;

    public async Task<IReadOnlyList<OfferDto>> ActiveOffersAsync()
    {
        await using var db = new SqlConnection(_connectionString);
        var rows = await db.QueryAsync<OfferDto>(
            """
            SELECT Id, Title, Message, IsActive, ExpiresAt, CreatedAt FROM Offers
            WHERE IsActive = 1 AND (ExpiresAt IS NULL OR ExpiresAt > SYSUTCDATETIME())
            ORDER BY CreatedAt DESC
            """);
        return rows.ToList();
    }

    public async Task<decimal> StockAsync(string metal)
    {
        await using var db = new SqlConnection(_connectionString);
        return await db.ExecuteScalarAsync<decimal?>("SELECT StockGrams FROM Inventory WHERE Metal = @metal", new { metal }) ?? 0;
    }
}
