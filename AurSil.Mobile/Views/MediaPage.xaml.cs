using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class MediaPage : ContentPage
{
	private readonly ApiClient _api;
	private readonly VoiceRecorder _recorder = new();

	public MediaPage(ApiClient api)
	{
		InitializeComponent();
		_api = api;
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RefreshListAsync();
	}

	private async Task RefreshListAsync()
	{
		var result = await _api.GetMediaAsync();
		if (result.Ok && result.Data is { } items)
			MediaList.ItemsSource = items;
	}

	private async void OnTakePhotoClicked(object? sender, EventArgs e)
	{
		if (!MediaPicker.Default.IsCaptureSupported)
		{
			await DisplayAlertAsync("Camera", "This device has no camera available.", "OK");
			return;
		}

		try
		{
			var photo = await MediaPicker.Default.CapturePhotoAsync();
			if (photo is null) // user cancelled
				return;

			SetStatus("Uploading photo…");
			using var stream = await photo.OpenReadAsync();
			using var ms = new MemoryStream();
			await stream.CopyToAsync(ms);

			var result = await _api.UploadPhotoAsync(ms.ToArray(), photo.ContentType ?? "image/jpeg");
			await AfterUpload(result, "Photo saved ✅");
		}
		catch (PermissionException)
		{
			await DisplayAlertAsync("Camera permission needed", "Allow camera access in Settings to take a photo.", "OK");
			SetStatus(null);
		}
		catch (FeatureNotSupportedException)
		{
			await DisplayAlertAsync("Camera", "Taking photos isn't supported on this device.", "OK");
			SetStatus(null);
		}
		catch (Exception ex)
		{
			SetStatus(null);
			await DisplayAlertAsync("Camera error", ex.Message, "OK");
		}
	}

	private async void OnRecordClicked(object? sender, EventArgs e)
	{
		if (_recorder.IsRecording)
		{
			SetStatus("Saving recording…");
			VoiceButton.Text = "🎙  Record Voice";
			var clip = await _recorder.StopAsync();
			if (clip is null)
			{
				await DisplayAlertAsync("Voice", "Nothing was recorded. Hold the recording a little longer.", "OK");
				SetStatus(null);
				return;
			}
			var result = await _api.UploadVoiceAsync(clip.Value.Data, clip.Value.ContentType);
			await AfterUpload(result, "Voice recording saved ✅");
			return;
		}

		var started = await _recorder.StartAsync();
		if (!started)
		{
			await DisplayAlertAsync("Microphone permission needed", "Allow microphone access to record a voice note.", "OK");
			return;
		}
		VoiceButton.Text = "⏹  Stop Recording";
		SetStatus("Recording… tap Stop when done");
	}

	private async Task AfterUpload(ApiResult<MediaItemDto> result, string successMessage)
	{
		if (!result.Ok)
		{
			SetStatus(null);
			await DisplayAlertAsync("Upload failed", result.Message, "OK");
			return;
		}
		SetStatus(null);
		await RefreshListAsync();
		await DisplayAlertAsync("Saved", successMessage, "OK");
	}

	private void SetStatus(string? text)
	{
		StatusLabel.Text = text ?? "";
		StatusLabel.IsVisible = text is not null;
	}

	private async void OnBackTapped(object? sender, TappedEventArgs e) => await Shell.Current.GoToAsync("..");
}
