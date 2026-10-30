using System.Globalization;

namespace AurSil.Mobile.Services;

public static class Format
{
	private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

	public static readonly Color Up = Color.FromArgb("#16A34A");
	public static readonly Color Down = Color.FromArgb("#DC2626");

	public static string Inr(decimal amount) => "₹" + amount.ToString("N2", India);

	public static string InrWhole(decimal amount) => "₹" + amount.ToString("N0", India);

	public static string Grams(decimal grams) => grams.ToString("0.####", CultureInfo.InvariantCulture) + " g";

	public static string Percent(decimal pct) => (pct >= 0 ? "▲ " : "▼ ") + Math.Abs(pct).ToString("0.00", CultureInfo.InvariantCulture) + "%";

	public static string SignedInr(decimal amount) => (amount >= 0 ? "+" : "-") + Inr(Math.Abs(amount));

	public static Color Trend(decimal value) => value >= 0 ? Up : Down;

	public static string Metal(string metal) => metal switch { "gold" => "Gold", "silver" => "Silver", _ => "Platinum" };

	public static string Symbol(string metal) => metal switch { "gold" => "Au", "silver" => "Ag", _ => "Pt" };

	public static Color LineColor(string metal) => metal switch
	{
		"gold" => Color.FromArgb("#F5B82E"),
		"silver" => Color.FromArgb("#C0C7D1"),
		_ => Color.FromArgb("#A78BFA"),
	};

	public static string MetalBrushKey(string metal) => metal switch { "gold" => "GoldBrush", "silver" => "SilverBrush", _ => "PlatinumBrush" };

	// Same rounding as the server's StoreService.DiscountedRate
	public static decimal DiscountedRate(decimal buyRate, decimal percent) =>
		percent > 0 ? Math.Round(buyRate * (1 - percent / 100), 2) : buyRate;

	public static bool TryParseDecimal(string? text, out decimal value) =>
		decimal.TryParse(text?.Trim().Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out value);
}
