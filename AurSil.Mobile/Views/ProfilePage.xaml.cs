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

		await RefreshAssistantAsync();
	}

	private async Task RefreshAssistantAsync()
	{
		var assistant = await _api.GetAssistantAsync();
		AssistantLabel.Text = assistant.Data?.Connected == true
			? "Connected • tap to get a new link or disconnect"
			: "Buy and sell by chatting in ChatGPT or Claude";
	}

	// The link contains a personal key, so it is shown once and copied; a new link replaces the old one
	private async void OnAssistantTapped(object? sender, TappedEventArgs e)
	{
		var status = await _api.GetAssistantAsync();
		var connected = status.Data?.Connected == true;
		var choice = await DisplayActionSheetAsync("AI Assistant", "Cancel", connected ? "Disconnect" : null,
			connected ? "Create a new link" : "Create connector link", "How to connect");

		if (choice == "How to connect")
		{
			await DisplayAlertAsync("Connect ChatGPT or Claude",
				"Claude: Settings → Connectors → Add custom connector → paste the link.\n\n" +
				"ChatGPT: Settings → Apps & Connectors → Advanced → turn on Developer mode → Create → paste the link, authentication: none.\n\n" +
				"Then chat, e.g. \"buy 2 grams of gold\". Buys give you a payment link; the metal is added once you pay. Sells pay your bank account straight away.",
				"OK");
		}
		else if (choice == "Disconnect")
		{
			var result = await _api.DisconnectAssistantAsync();
			await DisplayAlertAsync("AI Assistant", result.Ok ? "Disconnected. The old link no longer works." : result.Message, "OK");
		}
		else if (choice is "Create connector link" or "Create a new link")
		{
			var result = await _api.CreateAssistantLinkAsync();
			if (!result.Ok || result.Data is null)
			{
				await DisplayAlertAsync("AI Assistant", result.Message, "OK");
				return;
			}
			await Clipboard.Default.SetTextAsync(result.Data.Url);
			await DisplayAlertAsync("Link copied",
				"Paste it into ChatGPT or Claude as a custom connector.\n\n" + result.Data.Url +
				"\n\nKeep it private: anyone with this link can trade on your account. " +
				(connected ? "Your previous link has stopped working." : ""),
				"OK");
		}
		else
		{
			return;
		}
		await RefreshAssistantAsync();
	}

	private async void OnBankTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("bank");

	private async void OnMediaTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("media");

	private async void OnTransactionsTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("//main/portfolio");

	private async void OnChangePasswordTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("password");

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
		await _api.LogoutAsync();
		Session.SignOut();
		await Shell.Current.GoToAsync("//login");
	}
}
