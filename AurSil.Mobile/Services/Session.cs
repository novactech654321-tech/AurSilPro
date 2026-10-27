using System.Text.Json;

namespace AurSil.Mobile.Services;

public static class Session
{
	private const string UserKey = "current_user";
	private const string TokenKey = "auth_token";

	public static UserProfile? CurrentUser
	{
		get
		{
			var json = Preferences.Default.Get(UserKey, "");
			return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UserProfile>(json);
		}
	}

	public static string? Token
	{
		get
		{
			var token = Preferences.Default.Get(TokenKey, "");
			return string.IsNullOrEmpty(token) ? null : token;
		}
	}

	public static bool IsLoggedIn => CurrentUser is not null && Token is not null;

	public static void SignIn(string token, UserProfile user)
	{
		Preferences.Default.Set(TokenKey, token);
		Preferences.Default.Set(UserKey, JsonSerializer.Serialize(user));
	}

	public static void SignOut()
	{
		Preferences.Default.Remove(UserKey);
		Preferences.Default.Remove(TokenKey);
	}
}
