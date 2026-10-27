using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class ForgotPasswordPage : ContentPage
{
	private readonly ApiClient _api;
	private bool _otpSent;

	public ForgotPasswordPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	private async void OnActionClicked(object? sender, EventArgs e)
	{
		if (!_otpSent)
			await SendOtpAsync();
		else
			await ResetAsync();
	}

	private async void OnResendTapped(object? sender, TappedEventArgs e) => await SendOtpAsync();

	private async Task SendOtpAsync()
	{
		var email = EmailEntry.Text?.Trim() ?? "";
		if (!Validators.IsEmail(email))
		{
			ShowError("Please enter a valid email address.");
			return;
		}

		SetBusy(true);
		var result = await _api.ForgotPasswordAsync(email);
		SetBusy(false);

		if (!result.Ok)
		{
			ShowError(result.Message);
			return;
		}

		_otpSent = true;
		EmailEntry.IsEnabled = false;
		ResetSection.IsVisible = ResendLabel.IsVisible = true;
		SubtitleLabel.Text = "Enter the OTP and choose a new password";
		InfoLabel.Text = result.Message;
		InfoLabel.IsVisible = true;
		ActionButton.Text = "Reset Password";
	}

	private async Task ResetAsync()
	{
		var code = CodeEntry.Text?.Trim() ?? "";
		var password = NewPasswordEntry.Text ?? "";

		string? error =
			code.Length != 6 || !code.All(char.IsDigit) ? "Please enter the 6-digit OTP." :
			Validators.PasswordError(password) ??
			(password != ConfirmEntry.Text ? "Passwords do not match." : null);

		if (error is not null)
		{
			ShowError(error);
			return;
		}

		SetBusy(true);
		var result = await _api.ResetPasswordAsync(EmailEntry.Text!.Trim(), code, password);
		SetBusy(false);

		if (!result.Ok)
		{
			ShowError(result.Message);
			return;
		}

		await DisplayAlertAsync("Password changed ✅", result.Message, "Login");
		await Shell.Current.GoToAsync("//login");
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) =>
		await Shell.Current.GoToAsync("..");

	private void ShowError(string message)
	{
		InfoLabel.IsVisible = false;
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		ErrorLabel.IsVisible = false;
		ActionButton.IsEnabled = !busy;
		ActionButton.Text = busy ? "" : _otpSent ? "Reset Password" : "Send OTP";
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
