using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

[QueryProperty(nameof(Metal), "metal")]
public partial class BuyPage : ContentPage
{
	private static readonly decimal[] QuickInr = [500, 1000, 2000, 5000];
	private static readonly decimal[] QuickGrams = [0.5m, 1, 2, 5];
	private static readonly decimal[] QuickSilverGrams = [10, 50, 100, 500];

	private readonly ApiClient _api;
	private IDispatcherTimer? _timer;
	private MetalQuote? _quote;
	private decimal _gstPercent = 3;
	private bool _byGrams;
	private string _metal = "gold";

	public BuyPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	public string Metal
	{
		get => _metal;
		set
		{
			_metal = value is "silver" or "platinum" ? value : "gold";
			TitleLabel.Text = $"Buy {Format.Metal(_metal)}";
			Header.Background = (Brush)Application.Current!.Resources[Format.MetalBrushKey(_metal)];
			UpdateQuickLabels();
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(30);
		_timer.Tick += async (_, _) => await LoadPriceAsync();
		_timer.Start();
		await LoadPriceAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_timer?.Stop();
	}

	private async Task LoadPriceAsync()
	{
		var result = await _api.GetPricesAsync();
		if (!result.Ok || result.Data is null)
		{
			LiveLabel.Text = result.Message;
			return;
		}

		_gstPercent = result.Data.GstPercent;
		_quote = result.Data.Metals.FirstOrDefault(m => m.Metal == _metal);
		if (_quote is null)
			return;

		RateLabel.Text = Format.Inr(_quote.BuyRate) + " / g";
		LiveLabel.Text = $"Live price • updated {_quote.UpdatedAt.ToLocalTime():hh:mm tt} (excl. GST)";
		GstTitle.Text = $"GST ({_gstPercent:0.##}%)";
		Recalculate();
	}

	private void OnModeTapped(object? sender, TappedEventArgs e)
	{
		_byGrams = e.Parameter as string == "grams";
		SetModeStyle(ModeRupees, !_byGrams);
		SetModeStyle(ModeGrams, _byGrams);
		PrefixLabel.Text = _byGrams ? "" : "₹";
		SuffixLabel.Text = _byGrams ? "g" : "";
		AmountEntry.Text = "";
		UpdateQuickLabels();
		Recalculate();
	}

	private static void SetModeStyle(Border border, bool on)
	{
		border.BackgroundColor = on ? Color.FromArgb("#0B1F3A") : Colors.Transparent;
		if (border.Content is Label label)
		{
			label.TextColor = on ? Colors.White : Color.FromArgb("#0B1F3A");
			label.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
		}
	}

	private decimal[] QuickValues => !_byGrams ? QuickInr : _metal == "silver" ? QuickSilverGrams : QuickGrams;

	private void UpdateQuickLabels()
	{
		var values = QuickValues;
		var labels = new[] { Quick0, Quick1, Quick2, Quick3 };
		for (var i = 0; i < labels.Length; i++)
			labels[i].Text = _byGrams ? Format.Grams(values[i]) : Format.InrWhole(values[i]);
	}

	private void OnQuickTapped(object? sender, TappedEventArgs e)
	{
		var index = int.Parse((string)e.Parameter!);
		AmountEntry.Text = QuickValues[index].ToString(System.Globalization.CultureInfo.InvariantCulture);
	}

	private void OnAmountChanged(object? sender, TextChangedEventArgs e) => Recalculate();

	// Mirrors the server's calculation so the user sees the exact breakdown before paying
	private (decimal Grams, decimal Value, decimal Gst, decimal Total) Calculate()
	{
		if (_quote is null || !Format.TryParseDecimal(AmountEntry.Text, out var input) || input <= 0)
			return (0, 0, 0, 0);

		var gstRate = _gstPercent / 100;
		if (_byGrams)
		{
			var grams = Math.Round(input, 4);
			var value = Math.Round(grams * _quote.BuyRate, 2);
			var gst = Math.Round(value * gstRate, 2);
			return (grams, value, gst, value + gst);
		}

		var total = Math.Round(input, 2);
		var metalValue = Math.Round(total / (1 + gstRate), 2);
		return (Math.Floor(metalValue / _quote.BuyRate * 10000) / 10000, metalValue, total - metalValue, total);
	}

	private void Recalculate()
	{
		ErrorLabel.IsVisible = false;
		var (grams, value, gst, total) = Calculate();
		GetLabel.Text = $"{Format.Grams(grams)} {Format.Metal(_metal)}";
		RateRow.Text = _quote is null ? "--" : Format.Inr(_quote.BuyRate);
		ValueRow.Text = Format.Inr(value);
		GstRow.Text = Format.Inr(gst);
		TotalRow.Text = Format.Inr(total);
		PayButton.Text = total > 0 ? $"Proceed to Pay {Format.Inr(total)}" : "Proceed to Pay";
	}

	private async void OnPayClicked(object? sender, EventArgs e)
	{
		var (grams, _, _, total) = Calculate();
		if (total <= 0)
		{
			ShowError(_byGrams ? "Enter the grams you want to buy." : "Enter the amount you want to invest.");
			return;
		}

		SetBusy(true);
		var result = _byGrams
			? await _api.BuyAsync(_metal, null, grams)
			: await _api.BuyAsync(_metal, total, null);
		SetBusy(false);

		if (!result.Ok || result.Data is null)
		{
			ShowError(result.Message);
			return;
		}

		var order = result.Data.Order;
		await Shell.Current.GoToAsync($"payment?orderNo={order.OrderNo}&path={Uri.EscapeDataString(result.Data.PayPath)}");
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		PayButton.IsEnabled = !busy;
		if (busy)
			PayButton.Text = "";
		else
			Recalculate();
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
