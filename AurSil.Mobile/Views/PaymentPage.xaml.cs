using System.Web;
using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

// Hosts the server's payment page (Razorpay checkout or test payment) and watches for /pay/result
[QueryProperty(nameof(OrderNo), "orderNo")]
[QueryProperty(nameof(PayPath), "path")]
public partial class PaymentPage : ContentPage
{
	private readonly ApiClient _api;
	private bool _finished;

	public PaymentPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	public string OrderNo { get; set; } = "";

	public string PayPath
	{
		get => "";
		set => Web.Source = _api.PaymentUrl(Uri.UnescapeDataString(value));
	}

	private void OnNavigated(object? sender, WebNavigatedEventArgs e) => Busy.IsRunning = Busy.IsVisible = false;

	private async void OnNavigating(object? sender, WebNavigatingEventArgs e)
	{
		if (!e.Url.Contains("/pay/result", StringComparison.OrdinalIgnoreCase) || _finished)
			return;

		e.Cancel = true;
		_finished = true;
		var status = HttpUtility.ParseQueryString(new Uri(e.Url).Query)["status"];

		if (status == "success")
		{
			var order = await _api.GetOrderAsync(OrderNo);
			var message = order.Data is { } o
				? $"{Format.Grams(o.Grams)} of {Format.Metal(o.Metal)} has been added to your portfolio.\n\nPaid {Format.Inr(o.Total)}\nOrder {o.OrderNo}"
				: "Your purchase is complete.";
			await DisplayAlertAsync("Payment successful ✅", message, "View Portfolio");
			await Shell.Current.GoToAsync("//main/portfolio");
		}
		else if (status == "cancelled")
		{
			await Shell.Current.GoToAsync("..");
		}
		else
		{
			await DisplayAlertAsync("Payment failed", "The payment could not be completed. No metal was credited.", "OK");
			await Shell.Current.GoToAsync("..");
		}
	}

	private async void OnCloseTapped(object? sender, TappedEventArgs e)
	{
		if (await DisplayAlertAsync("Cancel payment?", "Your order will not be placed.", "Cancel payment", "Continue"))
			await Shell.Current.GoToAsync("..");
	}

	protected override bool OnBackButtonPressed()
	{
		OnCloseTapped(null, null!);
		return true;
	}
}
