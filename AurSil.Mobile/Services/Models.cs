namespace AurSil.Mobile.Services;

public record UserProfile(string UserId, string Name, string Email, string Phone, string Status);

public record LoginResponse(string Token, UserProfile User);

public record MetalQuote(string Metal, decimal BuyRate, decimal SellRate, decimal SpotUsdPerOz, decimal UsdInr,
	decimal Change24hPercent, DateTime UpdatedAt);

public record PricesResponse(List<MetalQuote> Metals, decimal GstPercent, DateTime UpdatedAt);

public record HistoryPoint(DateTime T, decimal Buy, decimal Sell);

public record OrderDto(string OrderNo, string Metal, string Side, decimal Grams, decimal RatePerGram, decimal Amount,
	decimal Gst, decimal Total, string Status, string? PaymentGateway, string? PayoutTo, DateTime CreatedAt, DateTime? CompletedAt)
{
	public string Title => $"{(Side == "Buy" ? "Bought" : "Sold")} {Format.Metal(Metal)}";
	public string GramsText => $"{Format.Grams(Grams)} @ {Format.Inr(RatePerGram)}/g";
	public string TotalText => (Side == "Buy" ? "- " : "+ ") + Format.Inr(Total);
	public Color TotalColor => Side == "Buy" ? Color.FromArgb("#0B1F3A") : Color.FromArgb("#15803D");
	public string DateText => CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
	public string Icon => Format.Symbol(Metal);
	public string Detail => Side == "Sell" && PayoutTo is not null ? $"Paid to {PayoutTo}" : Status;
}

public record BuyResponse(OrderDto Order, string PaymentMode, string PayPath);

public record HoldingDto(string Metal, decimal Grams, decimal Invested, decimal CurrentValue, decimal ProfitLoss,
	decimal ProfitLossPercent, decimal SellRate);

public record PortfolioResponse(List<HoldingDto> Holdings, decimal TotalInvested, decimal TotalValue, decimal ProfitLoss,
	decimal ProfitLossPercent);

public record BankAccountDto(string HolderName, string AccountNumberMasked, string Ifsc, string BankName, string? Branch);

public record IfscInfo(string Ifsc, string BankName, string Branch, string City);
