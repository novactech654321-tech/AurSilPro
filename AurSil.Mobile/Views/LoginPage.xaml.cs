using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class LoginPage : ContentPage
{
	private readonly ApiClient _api;

	public LoginPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	private async void OnLoginClicked(object? sender, EventArgs e)
	{
		var login = LoginEntry.Text?.Trim() ?? "";
		var password = PasswordEntry.Text ?? "";

		if (login.Length == 0 || password.Length == 0)
		{
			ShowError("Please enter your User ID / Email and password.");
			return;
		}

		SetBusy(true);
		var result = await _api.LoginAsync(login, password);
		SetBusy(false);

		if (result.Ok && result.Data is { } session)
		{
			Session.SignIn(session.Token, session.User);
			PasswordEntry.Text = "";
			await Shell.Current.GoToAsync("//main/home");
			return;
		}

		if (result.NeedsVerification && result.Email is { } email)
		{
			await DisplayAlertAsync("Verify your email", result.Message, "OK");
			await Shell.Current.GoToAsync($"otp?email={Uri.EscapeDataString(email)}");
			return;
		}

		ShowError(result.Message);
	}

	private async void OnRegisterTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync("register");

	private async void OnForgotTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync("forgot");

	private void OnTogglePassword(object? sender, TappedEventArgs e)
	{
		PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
		ShowPassword.Text = PasswordEntry.IsPassword ? "Show" : "Hide";
	}

	private async void OnServerSettingsTapped(object? sender, TappedEventArgs e)
	{
		var url = await DisplayPromptAsync("Server address", "AurSil API URL (the PC running AurSil.Api):",
			initialValue: _api.BaseUrl, keyboard: Keyboard.Url);
		if (!string.IsNullOrWhiteSpace(url))
			_api.BaseUrl = url;
	}

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		ErrorLabel.IsVisible = false;
		LoginButton.IsEnabled = !busy;
		LoginButton.Text = busy ? "" : "Login";
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
