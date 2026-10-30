using AurSil.Api.Models;
using AurSil.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AurSil.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/orders")]
public class OrdersController(TradeService trade, OrderService orders, ActivityService activity) : ControllerBase
{
    [HttpPost("buy")]
    public async Task<IActionResult> Buy(BuyRequest req) => ToResult(await trade.BuyAsync(User.UserId(), req));

    [HttpPost("sell")]
    public async Task<IActionResult> Sell(SellRequest req) => ToResult(await trade.SellAsync(User.UserId(), req));

    [HttpGet]
    public async Task<IActionResult> List(int take = 50) => Ok(await trade.RecentOrdersAsync(User.UserId(), take));

    // The app reports an abandoned payment so it shows in the activity log; the order simply stays unpaid
    [HttpPost("{orderNo}/abandon")]
    public async Task<IActionResult> Abandon(string orderNo, [FromQuery] string reason = "cancelled")
    {
        var order = await orders.GetAsync(orderNo);
        if (order is null || order.UserId != User.UserId())
            return NotFound(new ApiMessage("Order not found."));
        if (order.Status == "Created")
            await activity.LogAsync(order.UserId, reason == "failed" ? Activity.PaymentFailed : Activity.PaymentCancelled,
                $"{orderNo}: {order.Grams:0.####} g {Metals.Display(order.Metal)}, {OrderService.Inr(order.Total)}");
        return Ok(new ApiMessage("Noted."));
    }

    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Get(string orderNo)
    {
        var order = await orders.GetAsync(orderNo);
        if (order is null || order.UserId != User.UserId())
            return NotFound(new ApiMessage("Order not found."));
        return Ok(order.ToDto());
    }

    private IActionResult ToResult<T>(TradeResult<T> result) => result switch
    {
        { Ok: true } => Ok(result.Data),
        { NeedsBank: true } => BadRequest(new { message = result.Error, needsBank = true }),
        _ => StatusCode(result.Status, new ApiMessage(result.Error!)),
    };
}
