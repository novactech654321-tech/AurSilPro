using System.Globalization;

namespace AurSil.Mobile.Views;

// Line chart for live price history. Set Points + LineColor, then call Invalidate() on the GraphicsView.
public class PriceTrendDrawable : IDrawable
{
	private static readonly CultureInfo India = CultureInfo.GetCultureInfo("en-IN");

	public IReadOnlyList<double> Points { get; set; } = [];
	public Color LineColor { get; set; } = Color.FromArgb("#F5B82E");
	public Color GridColor { get; set; } = Color.FromArgb("#2A4470");
	public Color LabelColor { get; set; } = Color.FromArgb("#AEB8CC");
	public bool ShowLabels { get; set; } = true;

	public void Draw(ICanvas canvas, RectF rect)
	{
		var labelWidth = ShowLabels ? 62f : 0f;
		var plot = new RectF(rect.Left, rect.Top + 6, rect.Width - labelWidth, rect.Height - 12);

		canvas.StrokeColor = GridColor;
		canvas.StrokeSize = 1;
		for (var i = 0; i <= 3; i++)
		{
			var y = plot.Top + plot.Height * i / 3f;
			canvas.DrawLine(plot.Left, y, plot.Right, y);
		}

		if (Points.Count < 2)
		{
			canvas.FontColor = LabelColor;
			canvas.FontSize = 12;
			canvas.DrawString("Loading chart...", rect, HorizontalAlignment.Center, VerticalAlignment.Center);
			return;
		}

		var min = Points.Min();
		var max = Points.Max();
		var span = max - min;
		if (span < 0.0001)
			span = 1;
		var step = plot.Width / (Points.Count - 1);
		PointF At(int i) => new(plot.Left + i * step, plot.Bottom - (float)((Points[i] - min) / span) * plot.Height);

		// Soft fill under the line
		var fill = new PathF(plot.Left, plot.Bottom);
		for (var i = 0; i < Points.Count; i++)
			fill.LineTo(At(i));
		fill.LineTo(plot.Right, plot.Bottom);
		fill.Close();
		canvas.SetFillPaint(new LinearGradientPaint(
			[new PaintGradientStop(0, LineColor.WithAlpha(0.35f)), new PaintGradientStop(1, LineColor.WithAlpha(0f))],
			new Point(0, 0), new Point(0, 1)), plot);
		canvas.FillPath(fill);

		var line = new PathF(At(0));
		for (var i = 1; i < Points.Count; i++)
			line.LineTo(At(i));
		canvas.StrokeColor = LineColor;
		canvas.StrokeSize = 2.5f;
		canvas.StrokeLineJoin = LineJoin.Round;
		canvas.DrawPath(line);

		var last = At(Points.Count - 1);
		canvas.FillColor = LineColor;
		canvas.FillCircle(last.X, last.Y, 4.5f);

		if (ShowLabels)
		{
			canvas.FontColor = LabelColor;
			canvas.FontSize = 10;
			var labelRect = new RectF(plot.Right + 4, 0, labelWidth - 4, 14);
			canvas.DrawString(Short(max), labelRect with { Y = plot.Top - 6 }, HorizontalAlignment.Left, VerticalAlignment.Center);
			canvas.DrawString(Short(min), labelRect with { Y = plot.Bottom - 8 }, HorizontalAlignment.Left, VerticalAlignment.Center);
		}
	}

	private static string Short(double v) => "₹" + v.ToString(v >= 1000 ? "N0" : "N2", India);
}
