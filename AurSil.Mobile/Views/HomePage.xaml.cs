using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class HomePage : ContentPage
{
	private static readonly Color Gold = Color.FromArgb("#F5B82E");
	private static readonly Color Navy = Color.FromArgb("#0B1F3A");
	private static readonly Color ChipOff = Color.FromArgb("#22FFFFFF");

	private readonly ApiClient _api;
	private IDispatcherTimer? _timer;
	private string _chartMetal = "gold";
	private string _range = "1D";

	public HomePage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var user = Session.CurrentUser;
		WelcomeLabel.Text = user is null ? "Welcome!" : $"Welcome, {user.Name.Split(' ')[0]}!";

		_timer = Dispatcher.CreateTimer();
		_timer.Interval = TimeSpan.FromSeconds(30);
		_timer.Tick += async (_, _) => await Task.WhenAll(LoadPricesAsync(), LoadOffersAsync());
		_timer.Start();

		await Task.WhenAll(LoadPricesAsync(), LoadOffersAsync(), LoadPortfolioAsync(), LoadChartAsync());
	}

	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_timer?.Stop();
	}

	private async void OnRefreshing(object? sender, EventArgs e)
	{
		await Task.WhenAll(LoadPricesAsync(), LoadOffersAsync(), LoadPortfolioAsync(), LoadChartAsync());
		Refresher.IsRefreshing = false;
	}

	private async Task LoadOffersAsync()
	{
		var result = await _api.GetOffersAsync();
		if (!result.Ok)
			return;
		BindableLayout.SetItemsSource(OfferList, result.Data);
		OfferList.IsVisible = result.Data is { Count: > 0 };
	}

	private async Task LoadPricesAsync()
	{
		var result = await _api.GetPricesAsync();
		if (!result.Ok || result.Data is null)
		{
			UpdatedLabel.Text = "Offline";
			return;
		}

		foreach (var q in result.Data.Metals)
		{
			var (rate, change, off) = q.Metal switch
			{
				"gold" => (GoldRate, GoldChange, GoldOff),
				"silver" => (SilverRate, SilverChange, SilverOff),
				_ => (PlatinumRate, PlatinumChange, PlatinumOff),
			};
			var discount = result.Data.DiscountFor(q.Metal);
			rate.Text = Format.Inr(Format.DiscountedRate(q.BuyRate, discount)) + "/g";
			change.Text = Format.Percent(q.Change24hPercent);
			off.Text = $"{discount:0.##}% OFF";
			off.IsVisible = discount > 0;
		}
		UpdatedLabel.Text = "LIVE • " + result.Data.UpdatedAt.ToLocalTime().ToString("hh:mm tt");
	}

	private async Task LoadPortfolioAsync()
	{
		var result = await _api.GetPortfolioAsync();
		if (!result.Ok || result.Data is not { } p)
			return;

		PortfolioValue.Text = Format.Inr(p.TotalValue);
		PortfolioInvested.Text = "Invested " + Format.Inr(p.TotalInvested);
		PortfolioPl.Text = p.TotalInvested > 0 ? $"{Format.SignedInr(p.ProfitLoss)} ({p.ProfitLossPercent:0.00}%)" : "Start investing today";
		PortfolioPl.TextColor = p.TotalInvested > 0 ? Format.Trend(p.ProfitLoss) : Navy;
	}

	private async Task LoadChartAsync()
	{
		var result = await _api.GetHistoryAsync(_chartMetal, _range);
		ChartDrawable.LineColor = Format.LineColor(_chartMetal);
		ChartDrawable.Points = result.Data?.Select(p => (double)p.Buy).ToList() ?? [];
		Chart.Invalidate();
	}

	private async void OnChartMetalTapped(object? sender, TappedEventArgs e)
	{
		_chartMetal = e.Parameter as string ?? "gold";
		foreach (var (tab, metal) in new[] { (TabGold, "gold"), (TabSilver, "silver"), (TabPlatinum, "platinum") })
		{
			var on = metal == _chartMetal;
			tab.Stroke = on ? Format.LineColor(metal) : Color.FromArgb("#33FFFFFF");
			tab.BackgroundColor = on ? Format.LineColor(metal).WithAlpha(0.2f) : Colors.Transparent;
		}
		await LoadChartAsync();
	}

	private async void OnRangeTapped(object? sender, TappedEventArgs e)
	{
		_range = e.Parameter as string ?? "1D";
		foreach (var (chip, name) in new[] { (Chip1D, "1D"), (Chip1W, "1W"), (Chip1M, "1M"), (Chip1Y, "1Y") })
		{
			var selected = name == _range;
			chip.BackgroundColor = selected ? Gold : ChipOff;
			if (chip.Content is Label label)
			{
				label.TextColor = selected ? Navy : Colors.White;
				label.FontAttributes = selected ? FontAttributes.Bold : FontAttributes.None;
			}
		}
		await LoadChartAsync();
	}

	private async void OnBuyTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync($"buy?metal={e.Parameter}");

	private async void OnPortfolioTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync("//main/portfolio");

	private async void OnMarketTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync("//main/market");

	private async void OnComingSoon(object? sender, TappedEventArgs e) =>
		await DisplayAlertAsync(e.Parameter as string ?? "AurSil", "This feature is coming soon.", "OK");
}
