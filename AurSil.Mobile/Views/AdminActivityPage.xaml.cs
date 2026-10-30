using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class AdminActivityPage : ContentPage
{
	private readonly ApiClient _api;

	public AdminActivityPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
		DayPicker.Date = DateTime.Today;
		DayPicker.MaximumDate = DateTime.Today;
	}

	private DateTime Day => DayPicker.Date ?? DateTime.Today;

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

	private async void OnDateSelected(object? sender, DateChangedEventArgs e) => await LoadAsync();

	private async void OnSearchCompleted(object? sender, EventArgs e) => await LoadAsync();

	private async Task LoadAsync()
	{
		var result = await _api.GetActivityAsync(Day, SearchEntry.Text);
		if (!result.Ok || result.Data is null)
		{
			CountLabel.Text = result.Message;
			return;
		}
		LogList.ItemsSource = result.Data;
		var day = Day.Date == DateTime.Today ? "today" : Day.ToString("dd MMM yyyy");
		CountLabel.Text = $"{result.Data.Count} activities {day}" +
		                  (string.IsNullOrWhiteSpace(SearchEntry.Text) ? "" : $" matching \"{SearchEntry.Text.Trim()}\"");
	}

	// Downloads the day's log as activity-yyyy-MM-dd.csv and opens the share sheet (save to Drive, WhatsApp, email...)
	private async void OnDownloadClicked(object? sender, EventArgs e)
	{
		var result = await _api.DownloadActivityFileAsync(Day);
		if (!result.Ok || result.Data is null)
		{
			await DisplayAlertAsync("Download failed", result.Message, "OK");
			return;
		}
		await Share.Default.RequestAsync(new ShareFileRequest
		{
			Title = $"AurSil activity log {Day:dd MMM yyyy}",
			File = new ShareFile(result.Data, "text/csv"),
		});
	}

	private async void OnMenuTapped(object? sender, TappedEventArgs e) => await AdminMenu.ShowAsync(this, _api);
}
