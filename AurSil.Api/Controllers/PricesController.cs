using AurSil.Api.Models;
using AurSil.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace AurSil.Api.Controllers;

[ApiController]
[Route("api/prices")]
public class PricesController(PriceService prices, StoreService store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var all = prices.All();
        if (all.Count == 0)
            return StatusCode(503, new ApiMessage("Live prices are loading. Please try again in a moment."));

        var discounts = (await store.DiscountsAsync()).Where(d => d.IsActive && d.Percent > 0).ToList();
        return Ok(new PricesResponse(all, prices.Options.GstPercent, all.Max(q => q.UpdatedAt), discounts));
    }

    [HttpGet("history")]
    public async Task<IActionResult> History(string metal = Metals.Gold, string range = "1D")
    {
        if (!Metals.IsValid(metal))
            return BadRequest(new ApiMessage("Unknown metal."));
        return Ok(await prices.HistoryAsync(metal, range.ToUpperInvariant()));
    }

    // Offers the admin has switched on; shown on every user's home screen
    [HttpGet("/api/offers")]
    public async Task<IActionResult> Offers() => Ok(await store.ActiveOffersAsync());
}
