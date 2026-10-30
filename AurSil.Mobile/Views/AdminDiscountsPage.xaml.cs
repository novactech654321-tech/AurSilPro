using System.Globalization;
using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class AdminDiscountsPage : ContentPage
{
	private readonly ApiClient _api;

	public AdminDiscountsPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	private (Entry Percent, Switch On, Label State) Row(string metal) => metal switch
	{
		"gold" => (GoldPercent, GoldSwitch, GoldState),
		"silver" => (SilverPercent, SilverSwitch, SilverState),
		_ => (PlatinumPercent, PlatinumSwitch, PlatinumState),
	};

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var result = await _api.GetDiscountsAsync();
		if (!result.Ok || result.Data is null)
		{
			await DisplayAlertAsync("Discounts", result.Message, "OK");
			return;
		}
		foreach (var d in result.Data)
			Show(d);
	}

	private void Show(DiscountDto d)
	{
		var (percent, on, state) = Row(d.Metal);
		percent.Text = d.Percent.ToString("0.##", CultureInfo.InvariantCulture);
		on.IsToggled = d.IsActive;
		state.Text = d.IsActive && d.Percent > 0 ? $"Live: {d.Percent:0.##}% off for all users" : "No discount running";
		state.TextColor = d.IsActive && d.Percent > 0 ? Color.FromArgb("#15803D") : Color.FromArgb("#6B7280");
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		if ((sender as Button)?.CommandParameter is not string metal)
			return;
		var (percentEntry, on, _) = Row(metal);

		var text = string.IsNullOrWhiteSpace(percentEntry.Text) ? "0" : percentEntry.Text;
		if (!Format.TryParseDecimal(text, out var percent) || percent is < 0 or > 50)
		{
			await DisplayAlertAsync("Invalid discount", "Enter a percent between 0 and 50.", "OK");
			return;
		}

		var result = await _api.UpdateDiscountAsync(metal, percent, on.IsToggled);
		if (!result.Ok || result.Data is null)
		{
			await DisplayAlertAsync("Could not save", result.Message, "OK");
			return;
		}
		Show(result.Data);
		await DisplayAlertAsync("Saved ✅", result.Data.IsActive && result.Data.Percent > 0
			? $"{Format.Metal(metal)} is now {result.Data.Percent:0.##}% off for all users."
			: $"{Format.Metal(metal)} discount is off.", "OK");
	}

	private async void OnMenuTapped(object? sender, TappedEventArgs e) => await AdminMenu.ShowAsync(this, _api);
}
