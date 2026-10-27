using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class ProfilePage : ContentPage
{
	private readonly ApiClient _api;

	public ProfilePage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (Session.CurrentUser is { } user)
		{
			InitialLabel.Text = user.Name.Length > 0 ? user.Name[..1].ToUpperInvariant() : "A";
			NameLabel.Text = user.Name;
			UserIdLabel.Text = "@" + user.UserId;
			EmailLabel.Text = user.Email;
			PhoneLabel.Text = "+91 " + user.Phone;
			StatusLabel.Text = user.Status;
		}
		ServerLabel.Text = _api.BaseUrl;

		var bank = await _api.GetBankAsync();
		BankLabel.Text = bank.Data is { } b ? $"{b.BankName} {b.AccountNumberMasked}" : "Add a bank account to receive sale money";
	}

	private async void OnBankTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("bank");

	private async void OnTransactionsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//main/portfolio");

	private async void OnServerTapped(object? sender, TappedEventArgs e)
	{
		var url = await DisplayPromptAsync("Server address", "AurSil API URL:", initialValue: _api.BaseUrl, keyboard: Keyboard.Url);
		if (string.IsNullOrWhiteSpace(url))
			return;
		_api.BaseUrl = url;
		ServerLabel.Text = _api.BaseUrl;
	}

	private async void OnLogoutClicked(object? sender, EventArgs e)
	{
		if (!await DisplayAlertAsync("Logout", "Do you want to log out of AurSil?", "Logout", "Cancel"))
			return;
		Session.SignOut();
		await Shell.Current.GoToAsync("//login");
	}
}
