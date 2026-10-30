namespace AurSil.Mobile.Services;

// Records a short voice clip from the microphone into an m4a file, then hands back the bytes.
// Uses Android's built-in MediaRecorder, so no extra NuGet package is needed.
public class VoiceRecorder
{
	public bool IsRecording { get; private set; }

#if ANDROID
	private Android.Media.MediaRecorder? _recorder;
	private string? _path;

	// Returns false if the user denied microphone permission
	public async Task<bool> StartAsync()
	{
		var status = await Permissions.RequestAsync<Permissions.Microphone>();
		if (status != PermissionStatus.Granted)
			return false;

		_path = Path.Combine(FileSystem.CacheDirectory, $"voice-{DateTime.Now:yyyyMMdd-HHmmss}.m4a");
		_recorder = OperatingSystem.IsAndroidVersionAtLeast(31)
			? new Android.Media.MediaRecorder(Platform.AppContext)
			: new Android.Media.MediaRecorder();
		_recorder.SetAudioSource(Android.Media.AudioSource.Mic);
		_recorder.SetOutputFormat(Android.Media.OutputFormat.Mpeg4);
		_recorder.SetAudioEncoder(Android.Media.AudioEncoder.Aac);
		_recorder.SetOutputFile(_path);
		_recorder.Prepare();
		_recorder.Start();
		IsRecording = true;
		return true;
	}

	// Stops and returns the recorded bytes, or null if nothing usable was captured
	public async Task<(byte[] Data, string ContentType)?> StopAsync()
	{
		if (_recorder is null)
			return null;

		try { _recorder.Stop(); }
		catch { /* too short / nothing recorded */ }
		finally
		{
			_recorder.Release();
			_recorder.Dispose();
			_recorder = null;
			IsRecording = false;
		}

		if (_path is null || !File.Exists(_path))
			return null;
		var bytes = await File.ReadAllBytesAsync(_path);
		return bytes.Length == 0 ? null : (bytes, "audio/mp4");
	}
#else
	public Task<bool> StartAsync() => Task.FromResult(false);
	public Task<(byte[] Data, string ContentType)?> StopAsync() => Task.FromResult<(byte[], string)?>(null);
#endif
}
