using System.Globalization;
using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class MarketPage : ContentPage
{
	private static readonly Color Gold = Color.FromArgb("#F5B82E");
	private static readonly Color Navy = Color.FromArgb("#0B1F3A");
	private static readonly Color Off = Color.FromArgb("#22FFFFFF");

	private readonly ApiClient _api;
	private IDispatcherTimer? _timer;
	private PricesResponse? _prices;
	private string _metal = "gold";
	private string _range = "1D";

	public MarketPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(30);
		_timer.Tick += async (_, _) => await LoadAsync();
		_timer.Start();
		await LoadAsync();
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_timer?.Stop();
	}

	private async void OnRefreshing(object? sender, EventArgs e)
	{
		await LoadAsync();
		Refresher.IsRefreshing = false;
	}

	private async Task LoadAsync()
	{
		var prices = await _api.GetPricesAsync();
		if (prices.Ok)
			_prices = prices.Data;
		ShowQuote();
		await LoadChartAsync();
	}

	private void ShowQuote()
	{
		MetalTitle.Text = _metal switch
		{
			"gold" => "Gold (24K, 99.9%)",
			"silver" => "Silver (99.9%)",
			_ => "Platinum (99.95%)",
		};

		var q = _prices?.Metals.FirstOrDefault(m => m.Metal == _metal);
		if (q is null)
		{
			BuyRateLabel.Text = "--";
			return;
		}

		BuyRateLabel.Text = Format.Inr(q.BuyRate);
		ChangeLabel.Text = Format.Percent(q.Change24hPercent) + " 24h";
		ChangeLabel.TextColor = Format.Trend(q.Change24hPercent);
		ChangeBadge.BackgroundColor = Format.Trend(q.Change24hPercent).WithAlpha(0.15f);
		BuyDetail.Text = Format.Inr(q.BuyRate);
		SellDetail.Text = Format.Inr(q.SellRate);
		SpotDetail.Text = "$" + q.SpotUsdPerOz.ToString("N2", CultureInfo.InvariantCulture) + " / oz";
		FxDetail.Text = "₹" + q.UsdInr.ToString("0.00", CultureInfo.InvariantCulture);
		UpdatedLabel.Text = "Updated " + q.UpdatedAt.ToLocalTime().ToString("dd MMM, hh:mm:ss tt") + " • refreshes every 30s";
	}

	private async Task LoadChartAsync()
	{
		var result = await _api.GetHistoryAsync(_metal, _range);
		var points = result.Data ?? [];
		ChartDrawable.LineColor = Format.LineColor(_metal);
		ChartDrawable.Points = points.Select(p => (double)p.Buy).ToList();
		Chart.Invalidate();

		if (points.Count >= 2)
		{
			var first = points[0].Buy;
			var change = first > 0 ? (points[^1].Buy - first) / first * 100 : 0;
			RangeLabel.Text = $"{_range} change: {Format.Percent(change)}  ({Format.SignedInr(points[^1].Buy - first)}/g)";
			RangeLabel.TextColor = Format.Trend(change);
		}
	}

	private async void OnMetalTapped(object? sender, TappedEventArgs e)
	{
		_metal = e.Parameter as string ?? "gold";
		foreach (var (seg, metal) in new[] { (SegGold, "gold"), (SegSilver, "silver"), (SegPlatinum, "platinum") })
			Highlight(seg, metal == _metal);
		ShowQuote();
		await LoadChartAsync();
	}

	private async void OnRangeTapped(object? sender, TappedEventArgs e)
	{
		_range = e.Parameter as string ?? "1D";
		foreach (var (chip, name) in new[] { (Chip1D, "1D"), (Chip1W, "1W"), (Chip1M, "1M"), (Chip1Y, "1Y") })
			Highlight(chip, name == _range);
		await LoadChartAsync();
	}

	private static void Highlight(Border border, bool on)
	{
		border.BackgroundColor = on ? Gold : Off;
		if (border.Content is Label label)
		{
			label.TextColor = on ? Navy : Colors.White;
			label.FontAttributes = on ? FontAttributes.Bold : FontAttributes.None;
		}
	}

	private async void OnBuyClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync($"buy?metal={_metal}");

	private async void OnSellClicked(object? sender, EventArgs e) =>
		await Shell.Current.GoToAsync($"sell?metal={_metal}");
}
