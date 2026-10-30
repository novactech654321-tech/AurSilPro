using AurSil.Api.Models;
using Dapper;
using Microsoft.Data.SqlClient;

namespace AurSil.Api.Services;

// Outcome of a trade: either the data or a user-facing error with the HTTP status the API should return
public record TradeResult<T>(T? Data, string? Error = null, int Status = 200, bool NeedsBank = false)
{
    public bool Ok => Error is null;
    public static TradeResult<T> Fail(string error, int status = 400, bool needsBank = false) => new(default, error, status, needsBank);
}

// Buy, sell and portfolio rules shared by the app's API and the AI assistant (MCP) connector
public class TradeService(IConfiguration config, PriceService prices, OrderService orders, RazorpayService razorpay,
    StoreService store, ActivityService activity, ILogger<TradeService> logger)
{
    private readonly string _connectionString = config.GetConnectionString("Aursil")!;

    // via is appended to the activity log, e.g. "AI assistant", so admins can tell where a trade came from
    public async Task<TradeResult<BuyResponse>> BuyAsync(string userId, BuyRequest req, string? via = null)
    {
        if (!Metals.IsValid(req.Metal))
            return TradeResult<BuyResponse>.Fail("Unknown metal.");
        if (prices.Get(req.Metal) is not { } quote)
            return TradeResult<BuyResponse>.Fail("Live prices are loading. Please try again.", 503);

        var gstRate = prices.Options.GstPercent / 100;
        var discount = await store.ActiveDiscountAsync(req.Metal);
        var rate = StoreService.DiscountedRate(quote.BuyRate, discount);
        decimal grams, amount, gst, total;

        if (req.AmountInr is > 0 and var inr)
        {
            // User pays exactly the amount entered; GST is carved out of it
            total = Math.Round(inr, 2);
            amount = Math.Round(total / (1 + gstRate), 2);
            gst = total - amount;
            grams = Math.Floor(amount / rate * 10000) / 10000;
        }
        else if (req.Grams is > 0 and var g)
        {
            grams = Math.Round(g, 4);
            amount = Math.Round(grams * rate, 2);
            gst = Math.Round(amount * gstRate, 2);
            total = amount + gst;
        }
        else
        {
            return TradeResult<BuyResponse>.Fail("Enter an amount in ₹ or grams.");
        }

        if (total < prices.Options.MinOrderInr)
            return TradeResult<BuyResponse>.Fail($"Minimum purchase is {OrderService.Inr(prices.Options.MinOrderInr)}.");
        if (total > prices.Options.MaxOrderInr)
            return TradeResult<BuyResponse>.Fail($"Maximum purchase per order is {OrderService.Inr(prices.Options.MaxOrderInr)}.");
        if (grams < 0.0001m)
            return TradeResult<BuyResponse>.Fail("Amount is too small to buy any metal.");

        var stock = await store.StockAsync(req.Metal);
        if (grams > stock)
            return TradeResult<BuyResponse>.Fail(stock <= 0
                ? $"{Metals.Display(req.Metal)} is out of stock right now. Please try again later."
                : $"Only {stock:0.####} g of {Metals.Display(req.Metal)} is available right now.");

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
                return TradeResult<BuyResponse>.Fail("Payment gateway is unavailable. Please try again.", 502);
            }
        }

        await using var db = new SqlConnection(_connectionString);
        await db.ExecuteAsync(
            """
            INSERT INTO Orders (OrderNo, UserId, Metal, Side, Grams, RatePerGram, Amount, Gst, Total, Status, PaymentGateway, GatewayOrderId, DiscountPercent)
            VALUES (@orderNo, @userId, @Metal, 'Buy', @grams, @rate, @amount, @gst, @total, 'Created', @mode, @gatewayOrderId, @discount)
            """,
            new { orderNo, userId, req.Metal, grams, rate, amount, gst, total, mode, gatewayOrderId, discount });

        var order = (await orders.GetAsync(orderNo))!;
        await activity.LogAsync(userId, Activity.BuyOrderCreated,
            $"{orderNo}: {grams:0.####} g {Metals.Display(req.Metal)} @ {OrderService.Inr(rate)}/g, total {OrderService.Inr(total)}" +
            (discount > 0 ? $" ({discount:0.##}% discount)" : "") + $", awaiting {mode} payment" + Via(via));
        return new(new BuyResponse(order.ToDto(), mode, $"/pay/{orderNo}"));
    }

    public async Task<TradeResult<OrderDto>> SellAsync(string userId, SellRequest req, string? via = null)
    {
        if (!Metals.IsValid(req.Metal))
            return TradeResult<OrderDto>.Fail("Unknown metal.");
        if (prices.Get(req.Metal) is not { } quote)
            return TradeResult<OrderDto>.Fail("Live prices are loading. Please try again.", 503);

        var grams = Math.Round(req.Grams, 4);
        if (grams <= 0)
            return TradeResult<OrderDto>.Fail("Enter the grams to sell.");

        await using var db = new SqlConnection(_connectionString);
        await db.OpenAsync();

        var bank = await db.QuerySingleOrDefaultAsync<BankAccountRow>("SELECT * FROM BankAccounts WHERE UserId = @userId", new { userId });
        if (bank is null)
            return TradeResult<OrderDto>.Fail("Please add your bank account to receive the sale amount.", needsBank: true);

        await using var tx = await db.BeginTransactionAsync();
        var holding = await db.QuerySingleOrDefaultAsync<(decimal Grams, decimal InvestedAmount)>(
            "SELECT Grams, InvestedAmount FROM Holdings WITH (UPDLOCK) WHERE UserId = @userId AND Metal = @Metal",
            new { userId, req.Metal }, tx);

        if (holding.Grams < grams)
        {
            await tx.RollbackAsync();
            return TradeResult<OrderDto>.Fail($"You only have {holding.Grams:0.####} g of {Metals.Display(req.Metal)}.");
        }

        var amount = Math.Round(grams * quote.SellRate, 2);
        if (amount < 1)
        {
            await tx.RollbackAsync();
            return TradeResult<OrderDto>.Fail("Amount is too small to sell.");
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
        // Metal sold back by a customer returns to the vault
        await db.ExecuteAsync("UPDATE Inventory SET StockGrams = StockGrams + @grams, UpdatedAt = SYSUTCDATETIME() WHERE Metal = @Metal",
            new { grams, req.Metal }, tx);
        await tx.CommitAsync();

        var order = (await orders.GetAsync(orderNo))!;
        await activity.LogAsync(userId, Activity.Sell,
            $"{orderNo}: {grams:0.####} g {Metals.Display(req.Metal)} @ {OrderService.Inr(quote.SellRate)}/g, " +
            $"{OrderService.Inr(amount)} paid to {bank.BankName} {BankMask.Mask(bank.AccountNumber)}" + Via(via));
        await orders.SendOrderEmailAsync(order);
        return new(order.ToDto());
    }

    public async Task<PortfolioResponse> PortfolioAsync(string userId)
    {
        await using var db = new SqlConnection(_connectionString);
        var rows = (await db.QueryAsync<(string Metal, decimal Grams, decimal InvestedAmount)>(
            "SELECT Metal, Grams, InvestedAmount FROM Holdings WHERE UserId = @userId", new { userId })).ToList();

        var holdings = Metals.All.Select(metal =>
        {
            var h = rows.FirstOrDefault(r => r.Metal == metal);
            var sellRate = prices.Get(metal)?.SellRate ?? 0;
            var value = Math.Round(h.Grams * sellRate, 2);
            var pl = value - h.InvestedAmount;
            var plPct = h.InvestedAmount > 0 ? Math.Round(pl / h.InvestedAmount * 100, 2) : 0;
            return new HoldingDto(metal, h.Grams, h.InvestedAmount, value, pl, plPct, sellRate);
        }).ToList();

        var invested = holdings.Sum(h => h.Invested);
        var total = holdings.Sum(h => h.CurrentValue);
        var profit = total - invested;
        return new PortfolioResponse(holdings, invested, total, profit, invested > 0 ? Math.Round(profit / invested * 100, 2) : 0);
    }

    public async Task<IReadOnlyList<OrderDto>> RecentOrdersAsync(string userId, int take)
    {
        await using var db = new SqlConnection(_connectionString);
        var rows = await db.QueryAsync<OrderRow>(
            """
            SELECT TOP (@take) o.*, b.BankName, b.AccountNumber FROM Orders o
            LEFT JOIN BankAccounts b ON b.Id = o.BankAccountId
            WHERE o.UserId = @userId AND o.Status <> 'Created'
            ORDER BY o.CreatedAt DESC
            """,
            new { take = Math.Clamp(take, 1, 200), userId });
        return rows.Select(r => r.ToDto()).ToList();
    }

    private static string Via(string? via) => via is null ? "" : $" (via {via})";
}
