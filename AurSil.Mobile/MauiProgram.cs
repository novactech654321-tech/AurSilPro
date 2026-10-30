using AurSil.Mobile.Services;
using AurSil.Mobile.Views;
using Microsoft.Extensions.Logging;

namespace AurSil.Mobile;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if ANDROID
		// Payment page: Razorpay checkout needs DOM storage, and a custom user agent stops
		// ngrok's free plan from showing its browser warning page inside the WebView
		Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("AurSilWebView", (handler, view) =>
		{
			var settings = handler.PlatformView.Settings;
			settings.JavaScriptEnabled = true;
			settings.DomStorageEnabled = true;
			settings.UserAgentString = "AurSilApp/1.0 (Android)";
		});
#endif

		builder.Services.AddSingleton<ApiClient>();

		builder.Services.AddTransient<SplashPage>();
		builder.Services.AddTransient<LoginPage>();
		builder.Services.AddTransient<RegisterPage>();
		builder.Services.AddTransient<OtpPage>();
		builder.Services.AddTransient<ForgotPasswordPage>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<MarketPage>();
		builder.Services.AddTransient<PortfolioPage>();
		builder.Services.AddTransient<ProfilePage>();
		builder.Services.AddTransient<BuyPage>();
		builder.Services.AddTransient<SellPage>();
		builder.Services.AddTransient<PaymentPage>();
		builder.Services.AddTransient<BankPage>();
		builder.Services.AddTransient<ChangePasswordPage>();
		builder.Services.AddTransient<MediaPage>();
		builder.Services.AddTransient<AdminStockPage>();
		builder.Services.AddTransient<AdminOffersPage>();
		builder.Services.AddTransient<AdminDiscountsPage>();
		builder.Services.AddTransient<AdminActivityPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
