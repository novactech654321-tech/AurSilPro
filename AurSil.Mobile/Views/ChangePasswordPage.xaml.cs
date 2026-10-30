using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class ChangePasswordPage : ContentPage
{
	private readonly ApiClient _api;

	public ChangePasswordPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	private async void OnSaveClicked(object? sender, EventArgs e)
	{
		var current = CurrentEntry.Text ?? "";
		var next = NewEntry.Text ?? "";

		if (current.Length == 0)
		{
			ShowError("Enter your current password.");
			return;
		}
		if (Validators.PasswordError(next) is { } error)
		{
			ShowError(error);
			return;
		}
		if (next != ConfirmEntry.Text)
		{
			ShowError("New passwords do not match.");
			return;
		}

		SetBusy(true);
		var result = await _api.ChangePasswordAsync(current, next);
		SetBusy(false);

		if (!result.Ok)
		{
			ShowError(result.Message);
			return;
		}
		await DisplayAlertAsync("Password changed ✅", result.Message, "OK");
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
		SaveButton.Text = busy ? "" : "Update Password";
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
