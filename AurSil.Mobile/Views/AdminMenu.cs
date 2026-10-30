using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

// The ⋮ menu in the header of every admin page
public static class AdminMenu
{
	public static async Task ShowAsync(Page page, ApiClient api)
	{
		var choice = await page.DisplayActionSheetAsync("Admin", "Close", null, "Change password", "Server address", "Logout");
		switch (choice)
		{
			case "Change password":
				await Shell.Current.GoToAsync("password");
				break;
			case "Server address":
				var url = await page.DisplayPromptAsync("Server address", "AurSil API URL:", initialValue: api.BaseUrl, keyboard: Keyboard.Url);
				if (!string.IsNullOrWhiteSpace(url))
					api.BaseUrl = url;
				break;
			case "Logout":
				if (!await page.DisplayAlertAsync("Logout", "Log out of the admin console?", "Logout", "Cancel"))
					return;
				await api.LogoutAsync();
				Session.SignOut();
				await Shell.Current.GoToAsync("//login");
				break;
		}
	}
}
