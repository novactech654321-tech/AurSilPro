using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

[QueryProperty(nameof(Email), "email")]
public partial class OtpPage : ContentPage
{
	private const int ResendSeconds = 30;
	private readonly ApiClient _api;
	private int _secondsLeft;
	private string _email = "";

	public OtpPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	public string Email
	{
		get => _email;
		set
		{
			_email = Uri.UnescapeDataString(value ?? "");
			SentToLabel.Text = $"Code sent to\n{_email}";
		}
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		StartResendCountdown();
	}

	private void OnCodeChanged(object? sender, TextChangedEventArgs e)
	{
		if (e.NewTextValue?.Length == 6)
			OnVerifyClicked(sender, EventArgs.Empty);
	}

	private async void OnVerifyClicked(object? sender, EventArgs e)
	{
		var code = CodeEntry.Text?.Trim() ?? "";
		if (code.Length != 6 || !code.All(char.IsDigit))
		{
			ShowError("Please enter the 6-digit OTP.");
			return;
		}
		if (!VerifyButton.IsEnabled)
			return;

		SetBusy(true);
		var result = await _api.VerifyOtpAsync(_email, code);
		SetBusy(false);

		if (!result.Ok)
		{
			ShowError(result.Message);
			CodeEntry.Text = "";
			return;
		}

		await DisplayAlertAsync("Account verified ✅", "Your AurSil account is active. Please log in.", "Login");
		await Shell.Current.GoToAsync("//login");
	}

	private async void OnResendTapped(object? sender, TappedEventArgs e)
	{
		if (_secondsLeft > 0)
			return;

		var result = await _api.ResendOtpAsync(_email);
		if (!result.Ok)
		{
			ShowError(result.Message);
			return;
		}
		ErrorLabel.IsVisible = false;
		await DisplayAlertAsync("OTP sent", "A new code has been sent to your email.", "OK");
		StartResendCountdown();
	}

	private void StartResendCountdown()
	{
		_secondsLeft = ResendSeconds;
		ResendLabel.Text = $"Resend in {_secondsLeft}s";
		Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
		{
			_secondsLeft--;
			ResendLabel.Text = _secondsLeft > 0 ? $"Resend in {_secondsLeft}s" : "Resend OTP";
			return _secondsLeft > 0;
		});
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
		VerifyButton.IsEnabled = !busy;
		VerifyButton.Text = busy ? "" : "Verify";
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
