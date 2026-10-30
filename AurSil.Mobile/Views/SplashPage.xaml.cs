using AurSil.Mobile.Services;

namespace AurSil.Mobile.Views;

public partial class SplashPage : ContentPage
{
	private bool _started;

	public SplashPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (_started)
			return;
		_started = true;

		await Task.WhenAll(
			Logo.FadeToAsync(1, 600, Easing.CubicOut),
			Logo.ScaleToAsync(1, 700, Easing.SpringOut));
		await Brand.FadeToAsync(1, 400);
		await Tagline.FadeToAsync(1, 400);

		StatusLabel.Text = "Loading your account...";
		await Task.Delay(1200);

		await Shell.Current.GoToAsync(Session.IsLoggedIn ? Session.HomeRoute : "//login");
	}
}
