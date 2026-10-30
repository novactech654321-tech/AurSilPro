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

	// Account
	public Task<ApiResult<object>> ChangePasswordAsync(string currentPassword, string newPassword) =>
		SendAsync<object>(HttpMethod.Post, "api/change-password", new { currentPassword, newPassword });

	public Task<ApiResult<object>> LogoutAsync() =>
		SendAsync<object>(HttpMethod.Post, "api/logout");

	public Task<ApiResult<object>> AbandonOrderAsync(string orderNo, string reason) =>
		SendAsync<object>(HttpMethod.Post, $"api/orders/{orderNo}/abandon?reason={reason}");

	public Task<ApiResult<List<OfferDto>>> GetOffersAsync() =>
		SendAsync<List<OfferDto>>(HttpMethod.Get, "api/offers");

	// AI assistant (ChatGPT / Claude) connector link
	public Task<ApiResult<AssistantStatus>> GetAssistantAsync() =>
		SendAsync<AssistantStatus>(HttpMethod.Get, "api/assistant");

	public Task<ApiResult<AssistantLink>> CreateAssistantLinkAsync() =>
		SendAsync<AssistantLink>(HttpMethod.Post, "api/assistant/key");

	public Task<ApiResult<object>> DisconnectAssistantAsync() =>
		SendAsync<object>(HttpMethod.Delete, "api/assistant/key");

	// Admin
	public Task<ApiResult<DashboardDto>> GetDashboardAsync() =>
		SendAsync<DashboardDto>(HttpMethod.Get, "api/admin/dashboard");

	public Task<ApiResult<object>> UpdateStockAsync(string metal, decimal grams, string mode) =>
		SendAsync<object>(HttpMethod.Put, $"api/admin/inventory/{metal}", new { grams, mode });

	public Task<ApiResult<List<OfferDto>>> GetAllOffersAsync() =>
		SendAsync<List<OfferDto>>(HttpMethod.Get, "api/admin/offers");

	public Task<ApiResult<OfferDto>> CreateOfferAsync(string title, string message, int? validDays) =>
		SendAsync<OfferDto>(HttpMethod.Post, "api/admin/offers", new { title, message, validDays, isActive = true });

	public Task<ApiResult<OfferDto>> UpdateOfferAsync(OfferDto offer, bool isActive) =>
		SendAsync<OfferDto>(HttpMethod.Put, $"api/admin/offers/{offer.Id}", new { offer.Title, offer.Message, isActive });

	public Task<ApiResult<object>> DeleteOfferAsync(int id) =>
		SendAsync<object>(HttpMethod.Delete, $"api/admin/offers/{id}");

	public Task<ApiResult<List<DiscountDto>>> GetDiscountsAsync() =>
		SendAsync<List<DiscountDto>>(HttpMethod.Get, "api/admin/discounts");

	public Task<ApiResult<DiscountDto>> UpdateDiscountAsync(string metal, decimal percent, bool isActive) =>
		SendAsync<DiscountDto>(HttpMethod.Put, $"api/admin/discounts/{metal}", new { percent, isActive });

	public Task<ApiResult<List<ActivityDto>>> GetActivityAsync(DateTime day, string? search) =>
		SendAsync<List<ActivityDto>>(HttpMethod.Get, $"api/admin/activity?date={day:yyyy-MM-dd}&search={Uri.EscapeDataString(search ?? "")}");

	// Saves the day's activity log CSV into the app's cache folder and returns its path
	public async Task<ApiResult<string>> DownloadActivityFileAsync(DateTime day)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/api/admin/activity/file?date={day:yyyy-MM-dd}");
			request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Session.Token);
			using var response = await _http.SendAsync(request);
			if (!response.IsSuccessStatusCode)
				return new ApiResult<string>(false, $"Download failed ({(int)response.StatusCode}).");

			var path = Path.Combine(FileSystem.CacheDirectory, $"activity-{day:yyyy-MM-dd}.csv");
			await File.WriteAllBytesAsync(path, await response.Content.ReadAsByteArrayAsync());
			return new ApiResult<string>(true, "", path);
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return new ApiResult<string>(false, $"Cannot reach the server at {BaseUrl}.");
		}
	}

	// Camera photos / voice recordings captured in the app
	public Task<ApiResult<MediaItemDto>> UploadPhotoAsync(byte[] data, string contentType) =>
		UploadMediaAsync("api/media/photo", data, contentType, "photo.jpg");

	public Task<ApiResult<MediaItemDto>> UploadVoiceAsync(byte[] data, string contentType) =>
		UploadMediaAsync("api/media/voice", data, contentType, "voice.m4a");

	public Task<ApiResult<List<MediaItemDto>>> GetMediaAsync() =>
		SendAsync<List<MediaItemDto>>(HttpMethod.Get, "api/media");

	private async Task<ApiResult<MediaItemDto>> UploadMediaAsync(string path, byte[] data, string contentType, string fileName)
	{
		try
		{
			using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{path}");
			if (Session.Token is { } token)
				request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

			var fileContent = new ByteArrayContent(data);
			fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
			var form = new MultipartFormDataContent { { fileContent, "file", fileName } };
			request.Content = form;

			using var response = await _http.SendAsync(request);
			var text = await response.Content.ReadAsStringAsync();
			if (!response.IsSuccessStatusCode)
			{
				var message = TryReadMessage(text) ?? $"Upload failed ({(int)response.StatusCode}).";
				return new ApiResult<MediaItemDto>(false, message);
			}
			return new ApiResult<MediaItemDto>(true, "", JsonSerializer.Deserialize<MediaItemDto>(text, Json));
		}
		catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
		{
			return new ApiResult<MediaItemDto>(false, $"Cannot reach the server at {BaseUrl}.");
		}
	}

	private static string? TryReadMessage(string text)
	{
		try
		{
			using var doc = string.IsNullOrWhiteSpace(text) || text[0] != '{' ? null : JsonDocument.Parse(text);
			return doc?.RootElement.TryGetProperty("message", out var m) == true ? m.GetString() : null;
		}
		catch (JsonException) { return null; }
	}

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
