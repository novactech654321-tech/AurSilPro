namespace AurSil.Api.Models;

// A photo or voice clip the user captured in the app. The bytes themselves are only sent on download.
public record MediaItemDto(long MediaId, string Kind, string ContentType, int SizeBytes, DateTime CreatedAt);
