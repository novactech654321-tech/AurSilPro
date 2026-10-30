namespace AurSil.Mobile.Services;

public record UserProfile(string UserId, string Name, string Email, string Phone, string Status, string? Role = null)
{
	public bool IsAdmin => Role == "Admin";
}

public record LoginResponse(string Token, UserProfile User);

public record MetalQuote(string Metal, decimal BuyRate, decimal SellRate, decimal SpotUsdPerOz, decimal UsdInr,
	decimal Change24hPercent, DateTime UpdatedAt);

public record PricesResponse(List<MetalQuote> Metals, decimal GstPercent, DateTime UpdatedAt, List<DiscountDto>? Discounts)
{
	// Percent off the buy rate for a metal, 0 when the admin has no discount running
	public decimal DiscountFor(string metal) => Discounts?.FirstOrDefault(d => d.Metal == metal && d.IsActive)?.Percent ?? 0;
}

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

public record DiscountDto(string Metal, decimal Percent, bool IsActive);

public record OfferDto(int Id, string Title, string Message, bool IsActive, DateTime? ExpiresAt, DateTime CreatedAt)
{
	public string ValidityText => ExpiresAt is { } e ? $"Valid till {e.ToLocalTime():dd MMM yyyy, hh:mm tt}" : "No end date";
	public string StatusText => !IsActive ? "Hidden" : ExpiresAt < DateTime.UtcNow ? "Expired" : "Live";
	public Color StatusColor => StatusText == "Live" ? Color.FromArgb("#15803D") : Color.FromArgb("#9CA3AF");
}

// Admin
public record MetalStockDto(string Metal, decimal StockGrams, decimal CustomerGrams, decimal TodayBoughtGrams,
	decimal TodaySoldGrams, decimal BuyRate, decimal StockValue, DateTime UpdatedAt)
{
	public string Name => Format.Metal(Metal);
	public string Symbol => Format.Symbol(Metal);
	public Color Accent => Format.LineColor(Metal);
	public string StockText => Format.Grams(StockGrams);
	public string StockState => StockGrams <= 0 ? "OUT OF STOCK" : "In stock";
	public Color StockStateColor => StockGrams <= 0 ? Color.FromArgb("#D32F2F") : Color.FromArgb("#15803D");
	public string ValueText => $"Worth {Format.Inr(StockValue)} at {Format.Inr(BuyRate)}/g";
	public string CustomerText => Format.Grams(CustomerGrams);
	public string TodayText => $"{Format.Grams(TodayBoughtGrams)} / {Format.Grams(TodaySoldGrams)}";
	public string UpdatedText => $"Stock updated {UpdatedAt.ToLocalTime():dd MMM, hh:mm tt}";
}

public record DashboardDto(List<MetalStockDto> Metals, int TotalUsers, int LoginsToday, int OrdersToday,
	decimal BuyValueToday, decimal SellValueToday);

public record ActivityDto(long Id, string? UserId, string? UserName, string Action, string? Details, string? IpAddress, DateTime CreatedAt)
{
	public string TimeText => CreatedAt.ToLocalTime().ToString("hh:mm:ss tt");
	public string Who => UserId is null ? "Unknown user" : UserName is null ? UserId : $"{UserName} (@{UserId})";
	public string ActionText => Action switch
	{
		"BuyOrderCreated" => "Buy order placed",
		"BuyCompleted" => "Bought (paid)",
		"PaymentCancelled" => "Payment cancelled",
		"PaymentFailed" => "Payment failed",
		"LoginFailed" => "Login failed",
		"PasswordChanged" => "Password changed",
		"PasswordReset" => "Password reset",
		"ForgotPassword" => "Forgot password",
		"BankAccountSaved" => "Bank account saved",
		"EmailVerified" => "Email verified",
		"StockUpdated" => "Stock updated",
		"OfferCreated" => "Offer created",
		"OfferUpdated" => "Offer updated",
		"OfferDeleted" => "Offer deleted",
		"DiscountUpdated" => "Discount updated",
		"AssistantConnected" => "AI assistant connected",
		"AssistantDisconnected" => "AI assistant disconnected",
		"PhotoCaptured" => "Photo captured",
		"VoiceRecorded" => "Voice recorded",
		_ => Action,
	};
	public Color ActionColor => Action switch
	{
		"BuyCompleted" or "Sell" => Color.FromArgb("#15803D"),
		"LoginFailed" or "PaymentFailed" or "PaymentCancelled" => Color.FromArgb("#D32F2F"),
		"PasswordChanged" or "PasswordReset" or "ForgotPassword" => Color.FromArgb("#B45309"),
		"StockUpdated" or "OfferCreated" or "OfferUpdated" or "OfferDeleted" or "DiscountUpdated" => Color.FromArgb("#7C3AED"),
		_ => Color.FromArgb("#0B1F3A"),
	};
}

public record AssistantStatus(bool Connected, DateTime? CreatedAt);

public record AssistantLink(string Url);

public record MediaItemDto(long MediaId, string Kind, string ContentType, int SizeBytes, DateTime CreatedAt)
{
	public string Icon => Kind == "voice" ? "🎙" : "📷";
	public string Title => Kind == "voice" ? "Voice recording" : "Photo";
	public string SizeText => SizeBytes < 1024 ? $"{SizeBytes} B"
		: SizeBytes < 1024 * 1024 ? $"{SizeBytes / 1024.0:0.#} KB"
		: $"{SizeBytes / (1024.0 * 1024.0):0.#} MB";
	public string DateText => CreatedAt.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt");
	public string SubText => $"{SizeText}  •  {DateText}";
}
