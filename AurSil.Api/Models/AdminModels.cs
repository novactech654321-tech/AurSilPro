namespace AurSil.Api.Models;

public static class Activity
{
    public const string Registered = "Registered";
    public const string EmailVerified = "EmailVerified";
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string ForgotPassword = "ForgotPassword";
    public const string PasswordReset = "PasswordReset";
    public const string PasswordChanged = "PasswordChanged";
    public const string BuyOrderCreated = "BuyOrderCreated";
    public const string BuyCompleted = "BuyCompleted";
    public const string PaymentCancelled = "PaymentCancelled";
    public const string PaymentFailed = "PaymentFailed";
    public const string Sell = "Sell";
    public const string BankAccountSaved = "BankAccountSaved";
    public const string StockUpdated = "StockUpdated";
    public const string OfferCreated = "OfferCreated";
    public const string OfferUpdated = "OfferUpdated";
    public const string OfferDeleted = "OfferDeleted";
    public const string DiscountUpdated = "DiscountUpdated";
    public const string AssistantConnected = "AssistantConnected";
    public const string AssistantDisconnected = "AssistantDisconnected";
    public const string PhotoCaptured = "PhotoCaptured";
    public const string VoiceRecorded = "VoiceRecorded";
}

public record ActivityDto(long Id, string? UserId, string? UserName, string Action, string? Details, string? IpAddress, DateTime CreatedAt);

public record OfferDto(int Id, string Title, string Message, bool IsActive, DateTime? ExpiresAt, DateTime CreatedAt);

public record OfferRequest(string Title, string Message, int? ValidDays, bool IsActive = true);

public record DiscountDto(string Metal, decimal Percent, bool IsActive);

public record DiscountRequest(decimal Percent, bool IsActive);

public record StockRequest(decimal Grams, string Mode); // Mode: add | set

public record MetalStockDto(
    string Metal,
    decimal StockGrams,
    decimal CustomerGrams,
    decimal TodayBoughtGrams,
    decimal TodaySoldGrams,
    decimal BuyRate,
    decimal StockValue,
    DateTime UpdatedAt);

public record DashboardDto(
    IReadOnlyList<MetalStockDto> Metals,
    int TotalUsers,
    int LoginsToday,
    int OrdersToday,
    decimal BuyValueToday,
    decimal SellValueToday);
