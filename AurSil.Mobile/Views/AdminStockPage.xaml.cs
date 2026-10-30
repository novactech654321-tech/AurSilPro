using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class AdminStockPage : ContentPage
{
	private readonly ApiClient _api;

	public AdminStockPage(ApiClient api)
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
		var result = await _api.GetDashboardAsync();
		ErrorLabel.IsVisible = !result.Ok;
		if (!result.Ok || result.Data is not { } d)
		{
			ErrorLabel.Text = result.Message;
			return;
		}

		UsersLabel.Text = d.TotalUsers.ToString();
		LoginsLabel.Text = d.LoginsToday.ToString();
		OrdersLabel.Text = d.OrdersToday.ToString();
		BuyValueLabel.Text = Format.Inr(d.BuyValueToday);
		SellValueLabel.Text = Format.Inr(d.SellValueToday);
		BindableLayout.SetItemsSource(StockList, d.Metals);
	}

	private async void OnUpdateStockTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not string metal)
			return;
		var name = Format.Metal(metal);

		var choice = await DisplayActionSheetAsync($"Update {name} stock", "Cancel", null, "Add grams", "Remove grams", "Set exact stock");
		var (mode, sign, prompt) = choice switch
		{
			"Add grams" => ("add", 1, $"Grams of {name} received into the vault:"),
			"Remove grams" => ("add", -1, $"Grams of {name} taken out of the vault:"),
			"Set exact stock" => ("set", 1, $"Total grams of {name} now in the vault:"),
			_ => ("", 0, ""),
		};
		if (mode == "")
			return;

		var text = await DisplayPromptAsync($"{name} stock", prompt, "Save", "Cancel", "0", keyboard: Keyboard.Numeric);
		if (text is null)
			return;
		if (!Format.TryParseDecimal(text, out var grams) || grams < 0 || (mode == "add" && grams == 0))
		{
			await DisplayAlertAsync("Invalid amount", "Enter a number of grams, e.g. 250 or 12.5", "OK");
			return;
		}

		var result = await _api.UpdateStockAsync(metal, sign * grams, mode);
		await DisplayAlertAsync(result.Ok ? "Stock updated ✅" : "Could not update", result.Message, "OK");
		if (result.Ok)
			await LoadAsync();
	}

	private async void OnMenuTapped(object? sender, TappedEventArgs e) => await AdminMenu.ShowAsync(this, _api);
}
