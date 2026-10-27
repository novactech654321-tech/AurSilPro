using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class BankPage : ContentPage
{
	private readonly ApiClient _api;
	private string _lookedUp = "";

	public BankPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		var result = await _api.GetBankAsync();
		if (result.Data is { } b)
		{
			CurrentCard.IsVisible = true;
			CurrentBank.Text = b.BankName;
			CurrentNumber.Text = $"A/c {b.AccountNumberMasked}  •  {b.Ifsc}";
			CurrentHolder.Text = b.HolderName;
			FormTitle.Text = "Change bank account";
			SaveButton.Text = "Update Bank Account";
		}
		else
		{
			HolderEntry.Text ??= Session.CurrentUser?.Name;
		}
	}

	private async void OnIfscChanged(object? sender, TextChangedEventArgs e)
	{
		var ifsc = e.NewTextValue?.Trim().ToUpperInvariant() ?? "";
		if (ifsc.Length != 11)
		{
			IfscInfoLabel.IsVisible = false;
			return;
		}
		if (ifsc == _lookedUp)
			return;

		_lookedUp = ifsc;
		var result = await _api.LookupIfscAsync(ifsc);
		IfscInfoLabel.IsVisible = true;
		IfscInfoLabel.TextColor = result.Ok ? Color.FromArgb("#15803D") : Color.FromArgb("#D32F2F");
		IfscInfoLabel.Text = result.Data is { } info ? $"✔ {info.BankName}, {info.Branch} ({info.City})" : result.Message;
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		var holder = HolderEntry.Text?.Trim() ?? "";
		var account = AccountEntry.Text?.Trim() ?? "";
		var ifsc = IfscEntry.Text?.Trim().ToUpperInvariant() ?? "";

		string? error =
			holder.Length < 3 ? "Enter the account holder name." :
			account.Length is < 9 or > 18 || !account.All(char.IsDigit) ? "Account number must be 9 to 18 digits." :
			account != ConfirmEntry.Text?.Trim() ? "Account numbers do not match." :
			ifsc.Length != 11 ? "Enter the 11-character IFSC code." : null;

		if (error is not null)
		{
			ShowError(error);
			return;
		}

		SetBusy(true);
		var result = await _api.SaveBankAsync(holder, account, ifsc);
		SetBusy(false);

		if (!result.Ok || result.Data is not { } bank)
		{
			ShowError(result.Message);
			return;
		}

		await DisplayAlertAsync("Bank account saved ✅", $"{bank.BankName} {bank.AccountNumberMasked}", "OK");
		await Shell.Current.GoToAsync("..");
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		ErrorLabel.IsVisible = false;
		SaveButton.IsEnabled = !busy;
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
