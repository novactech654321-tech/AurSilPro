using System.Globalization;
using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

[QueryProperty(nameof(Metal), "metal")]
public partial class SellPage : ContentPage
{
	private readonly ApiClient _api;
	private string _metal = "gold";
	private decimal _available;
	private decimal _sellRate;
	private BankAccountDto? _bank;

	public SellPage(ApiClient api)
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
			TitleLabel.Text = $"Sell {Format.Metal(_metal)}";
		}
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var portfolio = _api.GetPortfolioAsync();
		var bank = _api.GetBankAsync();
		await Task.WhenAll(portfolio, bank);

		if (portfolio.Result.Data?.Holdings.FirstOrDefault(h => h.Metal == _metal) is { } h)
		{
			_available = h.Grams;
			_sellRate = h.SellRate;
		}
		AvailableLabel.Text = Format.Grams(_available);
		RateLabel.Text = _sellRate > 0 ? Format.Inr(_sellRate) + " / g" : "--";

		_bank = bank.Result.Data;
		BankLabel.Text = _bank is null ? "Add bank account" : $"{_bank.BankName} {_bank.AccountNumberMasked}";
		BankAction.Text = _bank is null ? "Add ›" : "Change";
		Recalculate();
	}

	private void OnGramsChanged(object? sender, TextChangedEventArgs e) => Recalculate();

	private void OnSellAllTapped(object? sender, TappedEventArgs e) =>
		GramsEntry.Text = _available.ToString("0.####", CultureInfo.InvariantCulture);

	private decimal Grams => Format.TryParseDecimal(GramsEntry.Text, out var g) ? Math.Round(g, 4) : 0;

	private void Recalculate()
	{
		ErrorLabel.IsVisible = false;
		var amount = Math.Round(Grams * _sellRate, 2);
		ReceiveLabel.Text = Format.Inr(amount);
		SellButton.Text = amount > 0 ? $"Sell for {Format.Inr(amount)}" : "Sell";
	}

	private async void OnBankTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("bank");

	private async void OnSellClicked(object? sender, EventArgs e)
	{
		var grams = Grams;
		if (_available <= 0)
		{
			ShowError($"You don't have any {Format.Metal(_metal)} to sell yet.");
			return;
		}
		if (grams <= 0 || grams > _available)
		{
			ShowError($"Enter grams between 0 and {Format.Grams(_available)}.");
			return;
		}
		if (_bank is null)
		{
			await DisplayAlertAsync("Bank account needed", "Add your bank account to receive the sale amount.", "Add bank");
			await Shell.Current.GoToAsync("bank");
			return;
		}

		var amount = Math.Round(grams * _sellRate, 2);
		if (!await DisplayAlertAsync("Confirm sale",
			$"Sell {Format.Grams(grams)} {Format.Metal(_metal)} for about {Format.Inr(amount)}?\n\nPaid to {_bank.BankName} {_bank.AccountNumberMasked}",
			"Sell", "Cancel"))
			return;

		SetBusy(true);
		var result = await _api.SellAsync(_metal, grams);
		SetBusy(false);

		if (!result.Ok || result.Data is not { } order)
		{
			if (result.NeedsBank)
			{
				await Shell.Current.GoToAsync("bank");
				return;
			}
			ShowError(result.Message);
			return;
		}

		await DisplayAlertAsync("Sold ✅",
			$"{Format.Grams(order.Grams)} {Format.Metal(order.Metal)} sold at {Format.Inr(order.RatePerGram)}/g.\n\n{Format.Inr(order.Total)} paid to {order.PayoutTo}\nOrder {order.OrderNo}",
			"OK");
		await Shell.Current.GoToAsync("//main/portfolio");
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		SellButton.IsEnabled = !busy;
		if (busy)
			SellButton.Text = "";
		else
			Recalculate();
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
