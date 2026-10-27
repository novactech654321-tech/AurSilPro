using System.Text.RegularExpressions;

namespace AurSil.Mobile.Services;

// Mirrors the rules in AurSil.Api so users see errors before a round-trip
public static partial class Validators
{
	[GeneratedRegex(@"^[A-Za-z0-9_]{4,50}$")]
	private static partial Regex UserIdRegex();

	[GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
	private static partial Regex EmailRegex();

	[GeneratedRegex(@"^[6-9]\d{9}$")]
	private static partial Regex PhoneRegex();

	public static bool IsUserId(string? s) => UserIdRegex().IsMatch(s?.Trim() ?? "");
	public static bool IsEmail(string? s) => EmailRegex().IsMatch(s?.Trim() ?? "");
	public static bool IsPhone(string? s) => PhoneRegex().IsMatch(s?.Trim() ?? "");

	public static string? PasswordError(string? password)
	{
		if (string.IsNullOrEmpty(password) || password.Length < 8)
			return "Password must be at least 8 characters.";
		if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
			return "Password must contain letters and numbers.";
		return null;
	}
}
