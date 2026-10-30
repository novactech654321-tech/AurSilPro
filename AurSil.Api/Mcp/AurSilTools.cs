using System.ComponentModel;
using AurSil.Api.Models;
using AurSil.Api.Services;
using ModelContextProtocol.Server;

namespace AurSil.Api.Mcp;

// Tools an AI assistant (ChatGPT, Claude) can call on the signed-in user's AurSil account.
// The user is resolved from the personal key in the connector URL by McpAuth before any tool runs.
[McpServerToolType]
public class AurSilTools(TradeService trade, PriceService prices, StoreService store, OrderService orders,
    McpKeyService keys, IHttpContextAccessor http)
{
    private HttpContext Context => http.HttpContext!;
    private UserRow Caller => McpAuth.User(Context);
    private const string Via = "AI assistant";

    [McpServerTool(Name = "get_prices", Title = "Live metal prices", ReadOnly = true, OpenWorld = false)]
    [Description("Live AurSil prices in INR per gram for gold (24K), silver and platinum. buy_rate already includes any active discount; GST is added on top when buying.")]
    public async Task<object> GetPrices()
    {
        var discounts = await store.DiscountsAsync();
        return new
        {
            gst_percent = prices.Options.GstPercent,
            metals = prices.All().Select(q =>
            {
                var off = discounts.FirstOrDefault(d => d.Metal == q.Metal && d.IsActive)?.Percent ?? 0;
                return new
                {
                    metal = q.Metal,
                    buy_rate_per_gram = StoreService.DiscountedRate(q.BuyRate, off),
                    list_buy_rate_per_gram = q.BuyRate,
                    discount_percent = off,
                    sell_rate_per_gram = q.SellRate,
                    change_24h_percent = q.Change24hPercent,
                    updated_at = q.UpdatedAt,
                };
            }),
        };
    }

    [McpServerTool(Name = "get_portfolio", Title = "My holdings", ReadOnly = true, OpenWorld = false)]
    [Description("The user's metal holdings: grams held, amount invested, current value at the sell rate and profit or loss.")]
    public async Task<object> GetPortfolio()
    {
        var p = await trade.PortfolioAsync(Caller.UserId);
        return new
        {
            account = Caller.UserId,
            total_invested_inr = p.TotalInvested,
            current_value_inr = p.TotalValue,
            profit_loss_inr = p.ProfitLoss,
            profit_loss_percent = p.ProfitLossPercent,
            holdings = p.Holdings.Select(h => new { metal = h.Metal, grams = h.Grams, invested_inr = h.Invested, current_value_inr = h.CurrentValue, profit_loss_inr = h.ProfitLoss }),
        };
    }

    [McpServerTool(Name = "buy_metal", Title = "Buy gold, silver or platinum", Destructive = false, OpenWorld = true)]
    [Description("Creates a buy order on the user's account, by grams or by rupee amount (give exactly one). " +
                 "The metal is credited only after the user pays: always show them payment_url and ask them to open it. " +
                 "Confirm the metal and quantity with the user before calling. Use check_order afterwards to see if it was paid.")]
    public async Task<object> BuyMetal(
        [Description("gold, silver or platinum")] string metal,
        [Description("Grams to buy, e.g. 2 or 0.5. Leave empty when buying by amount.")] decimal? grams = null,
        [Description("Rupees to spend including GST, e.g. 5000. Leave empty when buying by grams.")] decimal? amount_inr = null)
    {
        if (grams is > 0 && amount_inr is > 0)
            return Error("Give either grams or amount_inr, not both.");

        var result = await trade.BuyAsync(Caller.UserId, new BuyRequest(Normalize(metal), amount_inr, grams), Via);
        if (!result.Ok)
            return Error(result.Error!);

        var o = result.Data!.Order;
        return new
        {
            order_no = o.OrderNo,
            status = "awaiting_payment",
            metal = o.Metal,
            grams = o.Grams,
            rate_per_gram = o.RatePerGram,
            metal_value_inr = o.Amount,
            gst_inr = o.Gst,
            total_to_pay_inr = o.Total,
            payment_url = keys.PublicBaseUrl(Context.Request) + result.Data.PayPath,
            message = $"Order {o.OrderNo} created. Open payment_url to pay {OrderService.Inr(o.Total)}; " +
                      $"{o.Grams:0.####} g {Metals.Display(o.Metal)} is added to the AurSil account as soon as the payment succeeds.",
        };
    }

    [McpServerTool(Name = "sell_metal", Title = "Sell metal to bank account", Destructive = true, OpenWorld = true)]
    [Description("Sells grams of metal from the user's holdings at the live sell rate. The money is paid to their saved bank account " +
                 "and the sale completes immediately and cannot be undone, so confirm the metal and grams with the user first.")]
    public async Task<object> SellMetal(
        [Description("gold, silver or platinum")] string metal,
        [Description("Grams to sell, e.g. 1.5")] decimal grams)
    {
        var result = await trade.SellAsync(Caller.UserId, new SellRequest(Normalize(metal), grams), Via);
        if (!result.Ok)
            return Error(result.NeedsBank ? result.Error + " Add it in the AurSil app under Profile → Bank Account." : result.Error!);

        var o = result.Data!;
        return new
        {
            order_no = o.OrderNo,
            status = "completed",
            metal = o.Metal,
            grams_sold = o.Grams,
            rate_per_gram = o.RatePerGram,
            amount_paid_inr = o.Total,
            message = $"Sold {o.Grams:0.####} g {Metals.Display(o.Metal)} for {OrderService.Inr(o.Total)}.",
        };
    }

    [McpServerTool(Name = "check_order", Title = "Order status", ReadOnly = true, OpenWorld = false)]
    [Description("Status of one of the user's orders, e.g. to confirm a buy order was paid and the metal credited.")]
    public async Task<object> CheckOrder([Description("The order number, e.g. AS2610281234")] string order_no)
    {
        var o = await orders.GetAsync(order_no.Trim());
        if (o is null || o.UserId != Caller.UserId)
            return Error("Order not found on this account.");
        return new
        {
            order_no = o.OrderNo,
            side = o.Side.ToLowerInvariant(),
            metal = o.Metal,
            grams = o.Grams,
            total_inr = o.Total,
            status = o.Status switch { "Completed" => "completed", "Created" => "awaiting_payment", _ => o.Status.ToLowerInvariant() },
            credited = o.Side == "Buy" && o.Status == "Completed",
            created_at = DateTime.SpecifyKind(o.CreatedAt, DateTimeKind.Utc),
        };
    }

    [McpServerTool(Name = "list_transactions", Title = "Recent transactions", ReadOnly = true, OpenWorld = false)]
    [Description("The user's most recent completed buys and sells, newest first.")]
    public async Task<object> ListTransactions([Description("How many to return, 1-50")] int limit = 10)
    {
        var list = await trade.RecentOrdersAsync(Caller.UserId, Math.Clamp(limit, 1, 50));
        return list.Select(o => new
        {
            order_no = o.OrderNo, side = o.Side.ToLowerInvariant(), metal = o.Metal, grams = o.Grams,
            rate_per_gram = o.RatePerGram, total_inr = o.Total, status = o.Status.ToLowerInvariant(), date = o.CreatedAt,
        });
    }

    [McpServerTool(Name = "get_offers", Title = "Current offers", ReadOnly = true, OpenWorld = false)]
    [Description("Offers currently published by AurSil, plus the per-metal discounts that apply to buy prices.")]
    public async Task<object> GetOffers()
    {
        var offers = await store.ActiveOffersAsync();
        var discounts = await store.DiscountsAsync();
        return new
        {
            offers = offers.Select(o => new { title = o.Title, message = o.Message, expires_at = o.ExpiresAt }),
            discounts = discounts.Where(d => d.IsActive && d.Percent > 0).Select(d => new { metal = d.Metal, percent = d.Percent }),
        };
    }

    // Assistants often say "Gold" or "24k gold"
    private static string Normalize(string metal)
    {
        var m = metal.Trim().ToLowerInvariant();
        return m.Contains("gold") ? Metals.Gold : m.Contains("silver") ? Metals.Silver : m.Contains("plat") ? Metals.Platinum : m;
    }

    private static object Error(string message) => new { error = message };
}
