using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class AdminOffersPage : ContentPage
{
	private readonly ApiClient _api;

	public AdminOffersPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync();
	}

	private async void OnRefreshing(object? sender, EventArgs e)
	{
		await LoadAsync();
		Refresher.IsRefreshing = false;
	}

	private async Task LoadAsync()
	{
		var result = await _api.GetAllOffersAsync();
		if (result.Ok)
			BindableLayout.SetItemsSource(OfferList, result.Data);
		else
			ShowError(result.Message);
	}

	private async void OnPostClicked(object? sender, EventArgs e)
	{
		var title = TitleEntry.Text?.Trim() ?? "";
		var message = MessageEditor.Text?.Trim() ?? "";
		int? days = null;

		if (title.Length == 0 || message.Length == 0)
		{
			ShowError("Enter a title and a message for the offer.");
			return;
		}
		if (!string.IsNullOrWhiteSpace(DaysEntry.Text))
		{
			if (!int.TryParse(DaysEntry.Text.Trim(), out var d) || d is < 1 or > 365)
			{
				ShowError("Validity must be 1 to 365 days, or empty.");
				return;
			}
			days = d;
		}

		SetBusy(true);
		var result = await _api.CreateOfferAsync(title, message, days);
		SetBusy(false);
		if (!result.Ok)
		{
			ShowError(result.Message);
			return;
		}

		TitleEntry.Text = MessageEditor.Text = DaysEntry.Text = "";
		await DisplayAlertAsync("Offer published ✅", "All users will see it on their home screen.", "OK");
		await LoadAsync();
	}

	private async void OnToggleTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not OfferDto offer)
			return;
		var result = await _api.UpdateOfferAsync(offer, !offer.IsActive);
		if (!result.Ok)
			await DisplayAlertAsync("Could not update", result.Message, "OK");
		await LoadAsync();
	}

	private async void OnDeleteTapped(object? sender, TappedEventArgs e)
	{
		if (e.Parameter is not OfferDto offer ||
		    !await DisplayAlertAsync("Delete offer", $"Delete \"{offer.Title}\"? Users will no longer see it.", "Delete", "Cancel"))
			return;
		var result = await _api.DeleteOfferAsync(offer.Id);
		if (!result.Ok)
			await DisplayAlertAsync("Could not delete", result.Message, "OK");
		await LoadAsync();
	}

	private async void OnMenuTapped(object? sender, TappedEventArgs e) => await AdminMenu.ShowAsync(this, _api);

	private void ShowError(string message)
	{
		ErrorLabel.Text = message;
		ErrorLabel.IsVisible = true;
	}

	private void SetBusy(bool busy)
	{
		ErrorLabel.IsVisible = false;
		PostButton.IsEnabled = !busy;
		PostButton.Text = busy ? "" : "Publish to all users";
		Busy.IsRunning = Busy.IsVisible = busy;
	}
}
