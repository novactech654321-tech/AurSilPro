using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class RegisterPage : ContentPage
{
	private readonly ApiClient _api;

	public RegisterPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	private async void OnRegisterClicked(object? sender, EventArgs e)
	{
		var userId = UserIdEntry.Text?.Trim() ?? "";
		var name = NameEntry.Text?.Trim() ?? "";
		var email = EmailEntry.Text?.Trim() ?? "";
		var phone = PhoneEntry.Text?.Trim() ?? "";
		var password = PasswordEntry.Text ?? "";

		string? error =
			!Validators.IsUserId(userId) ? "User ID must be 4-50 letters, numbers or underscore." :
			name.Length < 2 ? "Please enter your full name." :
			!Validators.IsEmail(email) ? "Please enter a valid email address." :
			!Validators.IsPhone(phone) ? "Please enter a valid 10-digit mobile number." :
			Validators.PasswordError(password) ??
			(password != ConfirmEntry.Text ? "Passwords do not match." : null);

		if (error is not null)
		{
			ShowError(error);
			return;
		}

		SetBusy(true);
		var result = await _api.RegisterAsync(userId, name, email, phone, password);
		SetBusy(false);

		if (!result.Ok)
		{
			ShowError(result.Message);
			return;
		}

		await Shell.Current.GoToAsync($"otp?email={Uri.EscapeDataString(email.ToLowerInvariant())}");
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync("..");

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		ErrorLabel.IsVisible = false;
		RegisterButton.IsEnabled = !busy;
		RegisterButton.Text = busy ? "" : "Register & Send OTP";
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
