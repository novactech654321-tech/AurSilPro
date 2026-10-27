using AurSil.Api.Models;
using AurSil.Api.Services;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController(IConfiguration config, PriceService prices, OrderService orders, RazorpayService razorpay,
    ILogger<OrdersController> logger) : ControllerBase
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    [HttpPost("buy")]
    public async Task<IActionResult> Buy(BuyRequest req)
    {
        if (!Metals.IsValid(req.Metal))
            return BadRequest(new ApiMessage("Unknown metal."));
        if (prices.Get(req.Metal) is not { } quote)
            return StatusCode(503, new ApiMessage("Live prices are loading. Please try again."));

        var gstRate = prices.Options.GstPercent / 100;
        decimal grams, amount, gst, total;

        if (req.AmountInr is > 0 and var inr)
        {
            // User pays exactly the amount entered; GST is carved out of it
            total = Math.Round(inr, 2);
            amount = Math.Round(total / (1 + gstRate), 2);
            gst = total - amount;
            grams = Math.Floor(amount / quote.BuyRate * 10000) / 10000;
        }
        else if (req.Grams is > 0 and var g)
        {
            grams = Math.Round(g, 4);
            amount = Math.Round(grams * quote.BuyRate, 2);
            gst = Math.Round(amount * gstRate, 2);
            total = amount + gst;
        }
        else
        {
            return BadRequest(new ApiMessage("Enter an amount in ₹ or grams."));
        }

        if (total < prices.Options.MinOrderInr)
            return BadRequest(new ApiMessage($"Minimum purchase is {OrderService.Inr(prices.Options.MinOrderInr)}."));
        if (total > prices.Options.MaxOrderInr)
            return BadRequest(new ApiMessage($"Maximum purchase per order is {OrderService.Inr(prices.Options.MaxOrderInr)}."));
        if (grams < 0.0001m)
            return BadRequest(new ApiMessage("Amount is too small to buy any metal."));

        var orderNo = OrderService.NewOrderNo();
        string? gatewayOrderId = null;
        var mode = "simulated";
        if (razorpay.IsConfigured)
        {
            try
            {
                gatewayOrderId = await razorpay.CreateOrderAsync(total, orderNo);
                mode = "razorpay";
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Razorpay order creation failed");
                return StatusCode(502, new ApiMessage("Payment gateway is unavailable. Please try again."));
            }
        }

        await using var db = new SqlConnection(_connectionString);
        await db.ExecuteAsync(
            """
            INSERT INTO Orders (OrderNo, UserId, Metal, Side, Grams, RatePerGram, Amount, Gst, Total, Status, PaymentGateway, GatewayOrderId)
            VALUES (@orderNo, @userId, @Metal, 'Buy', @grams, @rate, @amount, @gst, @total, 'Created', @mode, @gatewayOrderId)
            """,
            new { orderNo, userId = User.UserId(), req.Metal, grams, rate = quote.BuyRate, amount, gst, total, mode, gatewayOrderId });

        var order = (await orders.GetAsync(orderNo))!;
        return Ok(new BuyResponse(order.ToDto(), mode, $"/pay/{orderNo}"));
    }

    [HttpPost("sell")]
    public async Task<IActionResult> Sell(SellRequest req)
    {
        if (!Metals.IsValid(req.Metal))
            return BadRequest(new ApiMessage("Unknown metal."));
        if (prices.Get(req.Metal) is not { } quote)
            return StatusCode(503, new ApiMessage("Live prices are loading. Please try again."));

        var grams = Math.Round(req.Grams, 4);
        if (grams <= 0)
            return BadRequest(new ApiMessage("Enter the grams to sell."));

        var userId = User.UserId();
        await using var db = new SqlConnection(_connectionString);
        await db.OpenAsync();

        var bank = await db.QuerySingleOrDefaultAsync<BankAccountRow>("SELECT * FROM BankAccounts WHERE UserId = @userId", new { userId });
        if (bank is null)
            return BadRequest(new { message = "Please add your bank account to receive the sale amount.", needsBank = true });

        await using var tx = await db.BeginTransactionAsync();
        var holding = await db.QuerySingleOrDefaultAsync<(decimal Grams, decimal InvestedAmount)>(
            "SELECT Grams, InvestedAmount FROM Holdings WITH (UPDLOCK) WHERE UserId = @userId AND Metal = @Metal",
            new { userId, req.Metal }, tx);

        if (holding.Grams < grams)
        {
            await tx.RollbackAsync();
            return BadRequest(new ApiMessage($"You only have {holding.Grams:0.####} g of {Metals.Display(req.Metal)}."));
        }

        var amount = Math.Round(grams * quote.SellRate, 2);
        if (amount < 1)
        {
            await tx.RollbackAsync();
            return BadRequest(new ApiMessage("Amount is too small to sell."));
        }

        var investedReduction = Math.Round(holding.InvestedAmount * grams / holding.Grams, 2);
        await db.ExecuteAsync(
            "UPDATE Holdings SET Grams = Grams - @grams, InvestedAmount = InvestedAmount - @investedReduction, UpdatedAt = SYSUTCDATETIME() WHERE UserId = @userId AND Metal = @Metal",
            new { grams, investedReduction, userId, req.Metal }, tx);

        // Bank payout is simulated: the order completes immediately and the amount is shown as paid to the linked account
        var orderNo = OrderService.NewOrderNo();
        await db.ExecuteAsync(
            """
            INSERT INTO Orders (OrderNo, UserId, Metal, Side, Grams, RatePerGram, Amount, Gst, Total, Status, PaymentGateway, BankAccountId, CompletedAt)
            VALUES (@orderNo, @userId, @Metal, 'Sell', @grams, @rate, @amount, 0, @amount, 'Completed', 'bank', @bankId, SYSUTCDATETIME())
            """,
            new { orderNo, userId, req.Metal, grams, rate = quote.SellRate, amount, bankId = bank.Id }, tx);
        await tx.CommitAsync();

        var order = (await orders.GetAsync(orderNo))!;
        await orders.SendOrderEmailAsync(order);
        return Ok(order.ToDto());
    }

    [HttpGet]
    public async Task<IActionResult> List(int take = 50)
    {
        await using var db = new SqlConnection(_connectionString);
        var rows = await db.QueryAsync<OrderRow>(
            """
            SELECT TOP (@take) o.*, b.BankName, b.AccountNumber FROM Orders o
            LEFT JOIN BankAccounts b ON b.Id = o.BankAccountId
            WHERE o.UserId = @userId AND o.Status <> 'Created'
            ORDER BY o.CreatedAt DESC
            """,
            new { take = Math.Clamp(take, 1, 200), userId = User.UserId() });
        return Ok(rows.Select(r => r.ToDto()));
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo)
    {
        var order = await orders.GetAsync(orderNo);
        if (order is null || order.UserId != User.UserId())
            return NotFound(new ApiMessage("Order not found."));
        return Ok(order.ToDto());
    }
}
