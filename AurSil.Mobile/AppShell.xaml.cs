using AurSil.Mobile.Views;

namespace AurSil.Mobile;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();

		Routing.RegisterRoute("register", typeof(RegisterPage));
		Routing.RegisterRoute("otp", typeof(OtpPage));
		Routing.RegisterRoute("forgot", typeof(ForgotPasswordPage));
		Routing.RegisterRoute("buy", typeof(BuyPage));
		Routing.RegisterRoute("sell", typeof(SellPage));
		Routing.RegisterRoute("payment", typeof(PaymentPage));
		Routing.RegisterRoute("bank", typeof(BankPage));
	}
}
