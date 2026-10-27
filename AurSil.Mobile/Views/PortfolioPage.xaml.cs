using AurSil.Mobile.Services;
using Microsoft.Maui.Controls.Shapes;

namespace AurSil.Mobile.Views;

public partial class PortfolioPage : ContentPage
{
	private readonly ApiClient _api;

	public PortfolioPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync();
	}

	private async void OnRefreshing(object? sender, EventArgs e)
	{
		await LoadAsync();
		Refresher.IsRefreshing = false;
	}

	private async Task LoadAsync()
	{
		var portfolio = _api.GetPortfolioAsync();
		var orders = _api.GetOrdersAsync();
		await Task.WhenAll(portfolio, orders);

		if (portfolio.Result.Data is { } p)
		{
			ValueLabel.Text = Format.Inr(p.TotalValue);
			InvestedLabel.Text = "Invested " + Format.Inr(p.TotalInvested);
			PlLabel.Text = p.TotalInvested > 0 ? $"{Format.SignedInr(p.ProfitLoss)} ({p.ProfitLossPercent:0.00}%)" : "";
			PlLabel.TextColor = Format.Trend(p.ProfitLoss);

			HoldingsList.Children.Clear();
			foreach (var h in p.Holdings)
				HoldingsList.Children.Add(HoldingCard(h));
		}
		else if (!portfolio.Result.Ok)
		{
			await DisplayAlertAsync("Couldn't load portfolio", portfolio.Result.Message, "OK");
		}

		var list = orders.Result.Data ?? [];
		BindableLayout.SetItemsSource(TxList, list);
		TxCount.Text = list.Count > 0 ? $"{list.Count} orders" : "";
	}

	private View HoldingCard(HoldingDto h)
	{
		var brush = (Brush)Application.Current!.Resources[Format.MetalBrushKey(h.Metal)];
		var textColor = h.Metal == "silver" ? Color.FromArgb("#0B1F3A") : Colors.White;

		var badge = new Border
		{
			WidthRequest = 52, HeightRequest = 52, StrokeThickness = 0, Background = brush,
			StrokeShape = new RoundRectangle { CornerRadius = 14 },
			Content = new Label { Text = Format.Symbol(h.Metal), FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = textColor,
				HorizontalOptions = LayoutOptions.Center, VerticalOptions = LayoutOptions.Center },
		};

		var info = new VerticalStackLayout
		{
			VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = Format.Metal(h.Metal), FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0B1F3A") },
				new Label { Text = Format.Grams(h.Grams), FontSize = 13, TextColor = Color.FromArgb("#6B7280") },
			},
		};

		var value = new VerticalStackLayout
		{
			HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Center,
			Children =
			{
				new Label { Text = Format.Inr(h.CurrentValue), FontSize = 16, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#0B1F3A"), HorizontalOptions = LayoutOptions.End },
				new Label { Text = h.Invested > 0 ? $"{Format.SignedInr(h.ProfitLoss)} ({h.ProfitLossPercent:0.00}%)" : "Not invested",
					FontSize = 12, TextColor = h.Invested > 0 ? Format.Trend(h.ProfitLoss) : Color.FromArgb("#9CA3AF"), HorizontalOptions = LayoutOptions.End },
			},
		};

		var buy = new Button { Text = "Buy", HeightRequest = 38, FontSize = 14, Style = (Style)Application.Current.Resources["GoldButton"] };
		buy.Clicked += async (_, _) => await Shell.Current.GoToAsync($"buy?metal={h.Metal}");
		var sell = new Button
		{
			Text = "Sell", HeightRequest = 38, FontSize = 14, CornerRadius = 12, BackgroundColor = Colors.White,
			TextColor = Color.FromArgb("#0B1F3A"), BorderColor = Color.FromArgb("#D7DCE5"), BorderWidth = 1, IsEnabled = h.Grams > 0,
		};
		sell.Clicked += async (_, _) => await Shell.Current.GoToAsync($"sell?metal={h.Metal}");

		var top = new Grid { ColumnDefinitions = [new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto)], ColumnSpacing = 12 };
		top.Add(badge, 0);
		top.Add(info, 1);
		top.Add(value, 2);

		var actions = new Grid { ColumnDefinitions = [new(GridLength.Star), new(GridLength.Star)], ColumnSpacing = 10 };
		actions.Add(sell, 0);
		actions.Add(buy, 1);

		return new Border
		{
			BackgroundColor = Colors.White, StrokeThickness = 0, Padding = 14,
			StrokeShape = new RoundRectangle { CornerRadius = 16 },
			Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.08f, Radius = 12, Offset = new Point(0, 4) },
			Content = new VerticalStackLayout { Spacing = 12, Children = { top, actions } },
		};
	}
}
