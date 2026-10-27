using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AurSil.Mobile.Services;

public record ApiResult<T>(bool Ok, string Message, T? Data = default, bool NeedsVerification = false,
	string? Email = null, bool NeedsBank = false);

public class ApiClient
{
	// PC running AurSil.Api. Changeable in the app via the ⚙ on the login screen or Profile → Server address.
	public const string DefaultBaseUrl = "https://legume-cardiac-shiny.ngrok-free.dev";
	private const string BaseUrlKey = "api_base_url";

	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
	private readonly HttpClient _http = CreateHttpClient();

	private static HttpClient CreateHttpClient()
	{
		var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
		// ngrok's free plan shows a warning page instead of the API unless this header is sent
		http.DefaultRequestHeaders.Add("ngrok-skip-browser-warning", "true");
		return http;
	}

	public string BaseUrl
	{
		get => Preferences.Default.Get(BaseUrlKey, DefaultBaseUrl);
		set => Preferences.Default.Set(BaseUrlKey, value.Trim().TrimEnd('/'));
	}

	// Auth
	public Task<ApiResult<object>> RegisterAsync(string userId, string name, string email, string phone, string password) =>
		SendAsync<object>(HttpMethod.Post, "api/auth/register", new { userId, name, email, phone, password });

	public Task<ApiResult<object>> VerifyOtpAsync(string email, string code) =>
		SendAsync<object>(HttpMethod.Post, "api/auth/verify-otp", new { email, code });

	public Task<ApiResult<object>> ResendOtpAsync(string email) =>
		SendAsync<object>(HttpMethod.Post, "api/auth/resend-otp", new { email });

	public Task<ApiResult<LoginResponse>> LoginAsync(string login, string password) =>
		SendAsync<LoginResponse>(HttpMethod.Post, "api/auth/login", new { login, password });

	public Task<ApiResult<object>> ForgotPasswordAsync(string email) =>
		SendAsync<object>(HttpMethod.Post, "api/auth/forgot-password", new { email });

	public Task<ApiResult<object>> ResetPasswordAsync(string email, string code, string newPassword) =>
		SendAsync<object>(HttpMethod.Post, "api/auth/reset-password", new { email, code, newPassword });

	// Prices
	public Task<ApiResult<PricesResponse>> GetPricesAsync() =>
		SendAsync<PricesResponse>(HttpMethod.Get, "api/prices");

	public Task<ApiResult<List<HistoryPoint>>> GetHistoryAsync(string metal, string range) =>
		SendAsync<List<HistoryPoint>>(HttpMethod.Get, $"api/prices/history?metal={metal}&range={range}");

	// Trading
	public Task<ApiResult<BuyResponse>> BuyAsync(string metal, decimal? amountInr, decimal? grams) =>
		SendAsync<BuyResponse>(HttpMethod.Post, "api/orders/buy", new { metal, amountInr, grams });

	public Task<ApiResult<OrderDto>> SellAsync(string metal, decimal grams) =>
		SendAsync<OrderDto>(HttpMethod.Post, "api/orders/sell", new { metal, grams });

	public Task<ApiResult<OrderDto>> GetOrderAsync(string orderNo) =>
		SendAsync<OrderDto>(HttpMethod.Get, $"api/orders/{orderNo}");

	public Task<ApiResult<List<OrderDto>>> GetOrdersAsync() =>
		SendAsync<List<OrderDto>>(HttpMethod.Get, "api/orders");

	public Task<ApiResult<PortfolioResponse>> GetPortfolioAsync() =>
		SendAsync<PortfolioResponse>(HttpMethod.Get, "api/portfolio");

	// Bank
	public Task<ApiResult<BankAccountDto>> GetBankAsync() =>
		SendAsync<BankAccountDto>(HttpMethod.Get, "api/bank");

	public Task<ApiResult<BankAccountDto>> SaveBankAsync(string holderName, string accountNumber, string ifsc) =>
		SendAsync<BankAccountDto>(HttpMethod.Put, "api/bank", new { holderName, accountNumber, ifsc });

	public Task<ApiResult<IfscInfo>> LookupIfscAsync(string ifsc) =>
		SendAsync<IfscInfo>(HttpMethod.Get, $"api/bank/ifsc/{ifsc}");

	public string PaymentUrl(string payPath) => BaseUrl + payPath;

	private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string path, object? body = null)
	{
		try
		{
			using var request = new HttpRequestMessage(method, $"{BaseUrl}/{path}");
			if (body is not null)
				request.Content = JsonContent.Create(body, options: Json);
			if (Session.Token is { } token)
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

			using var response = await _http.SendAsync(request);
			var text = await response.Content.ReadAsStringAsync();

			if (response.StatusCode == HttpStatusCode.Unauthorized && Session.Token is not null && !path.StartsWith("api/auth"))
			{
				Session.SignOut();
				MainThread.BeginInvokeOnMainThread(async () =>
				{
					await Shell.Current.DisplayAlertAsync("Session expired", "Please log in again.", "OK");
					await Shell.Current.GoToAsync("//login");
				});
				return new ApiResult<T>(false, "Session expired. Please log in again.");
			}

			using var doc = string.IsNullOrWhiteSpace(text) || text[0] is not ('{' or '[') ? null : JsonDocument.Parse(text);
			var obj = doc?.RootElement is { ValueKind: JsonValueKind.Object } o ? o : (JsonElement?)null;
			var message = obj is { } r && r.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";

			if (response.IsSuccessStatusCode)
			{
				var data = typeof(T) == typeof(object) || doc is null ? default : JsonSerializer.Deserialize<T>(text, Json);
				return new ApiResult<T>(true, message, data);
			}

			var needsVerification = obj is { } e1 && e1.TryGetProperty("needsVerification", out var nv) && nv.GetBoolean();
			var needsBank = obj is { } e2 && e2.TryGetProperty("needsBank", out var nb) && nb.GetBoolean();
			var email = obj is { } e3 && e3.TryGetProperty("email", out var em) ? em.GetString() : null;
			if (string.IsNullOrEmpty(message))
				message = response.StatusCode == HttpStatusCode.NotFound ? "Server endpoint not found." : $"Request failed ({(int)response.StatusCode}).";

			return new ApiResult<T>(false, message, default, needsVerification, email, needsBank);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return new ApiResult<T>(false, $"Cannot reach the server at {BaseUrl}. Check that the API and ngrok are running.");
		}
		catch (JsonException)
		{
			return new ApiResult<T>(false, "Unexpected response from the server.");
		}
	}
}
