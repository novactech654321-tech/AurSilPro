using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

public class OrderService(IConfiguration config, EmailSender email, ILogger<OrderService> logger)
{
    private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    public static string NewOrderNo() =>
        "AS" + DateTime.UtcNow.ToString("yyMMdd") + Convert.ToHexString(RandomNumberGenerator.GetBytes(5));

    public async Task<OrderRow?> GetAsync(string orderNo)
    {
        await using var db = new SqlConnection(_connectionString);
        return await db.QuerySingleOrDefaultAsync<OrderRow>(
            """
            SELECT o.*, b.BankName, b.AccountNumber FROM Orders o
            LEFT JOIN BankAccounts b ON b.Id = o.BankAccountId
            WHERE o.OrderNo = @orderNo
            """, new { orderNo });
    }

    // Marks a paid buy order complete and credits the grams. Safe to call twice.
    public async Task<bool> CompleteBuyAsync(string orderNo, string gateway, string? paymentId)
    {
        await using var db = new SqlConnection(_connectionString);
        await db.OpenAsync();
        await using var tx = await db.BeginTransactionAsync();

        var order = await db.QuerySingleOrDefaultAsync<OrderRow>(
            "SELECT * FROM Orders WITH (UPDLOCK) WHERE OrderNo = @orderNo AND Side = 'Buy'", new { orderNo }, tx);
        if (order is null || order.Status != "Created")
        {
            await tx.RollbackAsync();
            return order?.Status == "Completed";
        }

        await db.ExecuteAsync(
            """
            UPDATE Orders SET Status = 'Completed', PaymentGateway = @gateway, GatewayPaymentId = @paymentId,
                CompletedAt = SYSUTCDATETIME() WHERE Id = @Id
            """, new { gateway, paymentId, order.Id }, tx);

        await db.ExecuteAsync(
            """
            MERGE Holdings AS h
            USING (SELECT @UserId AS UserId, @Metal AS Metal) AS s ON h.UserId = s.UserId AND h.Metal = s.Metal
            WHEN MATCHED THEN UPDATE SET Grams = h.Grams + @Grams, InvestedAmount = h.InvestedAmount + @Total, UpdatedAt = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN INSERT (UserId, Metal, Grams, InvestedAmount) VALUES (@UserId, @Metal, @Grams, @Total);
            """, new { order.UserId, order.Metal, order.Grams, order.Total }, tx);

        await tx.CommitAsync();
        order.Status = "Completed";
        order.PaymentGateway = gateway;
        await SendOrderEmailAsync(order);
        return true;
    }

    public async Task SendOrderEmailAsync(OrderRow order)
    {
        try
        {
            await using var db = new SqlConnection(_connectionString);
            var user = await db.QuerySingleAsync<UserRow>("SELECT * FROM Users WHERE UserId = @UserId", new { order.UserId });
            var metal = Metals.Display(order.Metal);
            var verb = order.Side == "Buy" ? "bought" : "sold";
            var rows = order.Side == "Buy"
                ? $"{Row("Metal value", order.Amount)}{Row("GST", order.Gst)}{Row("Total paid", order.Total, true)}"
                : $"{Row("Amount", order.Total, true)}<tr><td style='padding:6px 0;color:#666'>Paid to</td><td style='text-align:right'>{WebUtility.HtmlEncode(order.BankName)} {BankMask.Mask(order.AccountNumber ?? "")}</td></tr>";

            var html = $"""
                <div style="font-family:Segoe UI,Arial,sans-serif;max-width:480px;margin:auto;border:1px solid #e5e5e5;border-radius:12px;overflow:hidden">
                  <div style="background:#0B1F3A;padding:18px;text-align:center"><span style="color:#F5B82E;font-size:24px;font-weight:700">AurSil</span></div>
                  <div style="padding:22px;color:#222">
                    <p>Hi {WebUtility.HtmlEncode(user.Name)},</p>
                    <p>You have successfully {verb} <b>{order.Grams:0.####} g of {metal}</b>.</p>
                    <table style="width:100%;font-size:14px;border-collapse:collapse">
                      <tr><td style="padding:6px 0;color:#666">Order no.</td><td style="text-align:right">{order.OrderNo}</td></tr>
                      <tr><td style="padding:6px 0;color:#666">Rate</td><td style="text-align:right">{Inr(order.RatePerGram)} / g</td></tr>
                      {rows}
                    </table>
                  </div>
                </div>
                """;
            await email.SendAsync(user.Email, $"AurSil: {metal} {order.Side.ToLowerInvariant()} order {order.OrderNo}", html);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Order email failed for {OrderNo}", order.OrderNo);
        }
    }

    public static string Inr(decimal amount) => "₹" + amount.ToString("N2", India);

    private static string Row(string label, decimal amount, bool bold = false) =>
        $"<tr><td style='padding:6px 0;color:#666'>{label}</td><td style='text-align:right'>{(bold ? "<b>" : "")}{Inr(amount)}{(bold ? "</b>" : "")}</td></tr>";
}
