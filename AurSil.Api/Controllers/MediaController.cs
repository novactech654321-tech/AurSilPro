using AurSil.Api.Models;
using AurSil.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AurSil.Api.Controllers;

// Photos and voice clips the signed-in user captures in the app.
// The app only calls these after the user taps capture and grants camera / microphone permission.
[ApiController]
[Authorize]
[Route("api/media")]
public class MediaController(MediaService media, ActivityService activity) : ControllerBase
{
    private const long MaxBytes = 15 * 1024 * 1024; // 15 MB

    [HttpPost("photo")]
    [RequestSizeLimit(MaxBytes)]
    public Task<IActionResult> Photo(IFormFile file) => SaveAsync(file, "photo", "image/", Activity.PhotoCaptured);

    [HttpPost("voice")]
    [RequestSizeLimit(MaxBytes)]
    public Task<IActionResult> Voice(IFormFile file) => SaveAsync(file, "voice", "audio/", Activity.VoiceRecorded);

    private async Task<IActionResult> SaveAsync(IFormFile? file, string kind, string typePrefix, string action)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new ApiMessage("No file was received."));
        if (file.Length > MaxBytes)
            return BadRequest(new ApiMessage("File is too large (max 15 MB)."));

        var contentType = file.ContentType ?? "";
        if (!contentType.StartsWith(typePrefix, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new ApiMessage($"Expected a {kind} file."));

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var saved = await media.SaveAsync(User.UserId(), kind, contentType, ms.ToArray());
        await activity.LogAsync(User.UserId(), action, $"{kind} saved from the app ({saved.SizeBytes} bytes)");
        return Ok(saved);
    }

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await media.ListAsync(User.UserId()));

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Download(long id)
    {
        var blob = await media.GetAsync(User.UserId(), id);
        return blob is null ? NotFound() : File(blob.Data, blob.ContentType);
    }
}
