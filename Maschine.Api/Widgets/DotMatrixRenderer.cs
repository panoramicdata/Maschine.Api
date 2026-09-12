using Maschine.Api.Interfaces;
using Maschine.Api.Internal;
using Maschine.Api.Models;
using System.Text;

namespace Maschine.Api.Widgets;

/// <summary>
/// Rasterizes widgets into the 1-bit dot-matrix display buffer.
/// </summary>
/// <remarks>
/// A stateless type rather than part of <see cref="DotMatrixDashboard"/>: rendering needs
/// nothing from a dashboard instance, which leaves the dashboard as just widget management.
/// </remarks>
internal static class DotMatrixRenderer
{

	/// <summary>Draws one widget over its zone.</summary>
	internal static void RenderWidget(byte[] bitmap, IDotMatrixWidget widget)
	{
		var background = widget.Invert;
		var foreground = !background;

		DotMatrixCanvas.FillZone(bitmap, widget.Zone, background);

		switch (widget)
		{
			case TextWidget text:
				DotMatrixTextRenderer.RenderTextWidget(bitmap, text, foreground);
				break;
			case SpectrumWidget spectrum:
				RenderSpectrumWidget(bitmap, spectrum, foreground);
				break;
			case VuWidget vu:
				RenderVuWidget(bitmap, vu, foreground);
				break;
			default:
				// Unknown widget kinds render as their cleared background only.
				break;
		}
	}

	private static void RenderSpectrumWidget(byte[] bitmap, SpectrumWidget spectrum, bool on)
	{
		var levels = spectrum.BandLevels;
		if (levels.Count == 0)
		{
			return;
		}

		var zone = spectrum.Zone;
		var bands = Math.Min(levels.Count, zone.Width);
		var gap = Math.Clamp(spectrum.GapPixels, 0, 8);
		for (var i = 0; i < bands; i++)
		{
			RenderSpectrumBand(bitmap, spectrum, i, bands, gap, on);
		}
	}

	private static void RenderSpectrumBand(byte[] bitmap, SpectrumWidget spectrum, int index, int bands, int gap, bool on)
	{
		var zone = spectrum.Zone;
		var slotStart = zone.X + (index * zone.Width) / bands;
		var slotEnd = zone.X + ((index + 1) * zone.Width) / bands;
		var x0 = slotStart + Math.Min(gap, Math.Max(0, (slotEnd - slotStart) - 1));
		var x1 = Math.Max(x0, slotEnd - 1);

		var level = Math.Clamp(spectrum.BandLevels[index], 0f, 1f);
		var barHeight = (int)Math.Round(level * zone.Height, MidpointRounding.AwayFromZero);
		DotMatrixCanvas.FillRect(bitmap, x0, x1, zone.Bottom - barHeight, zone.Bottom - 1, on);

		var peaks = spectrum.PeakLevels;
		if (spectrum.ShowPeakMarkers && index < peaks.Count)
		{
			var markerY = zone.Bottom - 1 - (int)Math.Round(Math.Clamp(peaks[index], 0f, 1f) * (zone.Height - 1));
			DotMatrixCanvas.FillRect(bitmap, x0, x1, markerY, markerY, on);
		}
	}

	private static void RenderVuWidget(byte[] bitmap, VuWidget vu, bool on)
	{
		if (vu.Style == VuWidgetStyle.Needle)
		{
			RenderNeedleVu(bitmap, vu, on);
			return;
		}

		RenderBarVu(bitmap, vu, on);
	}

	/// <summary>
	/// Draws a bar VU meter, growing left-to-right in a wide zone and bottom-to-top in a tall one.
	/// </summary>
	private static void RenderBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		if (vu.Zone.Width >= vu.Zone.Height)
		{
			RenderHorizontalBarVu(bitmap, vu, on);
			return;
		}

		RenderVerticalBarVu(bitmap, vu, on);
	}

	private static void RenderHorizontalBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var level = Math.Clamp(vu.Level, 0f, 1f);
		var width = (int)Math.Round(level * zone.Width, MidpointRounding.AwayFromZero);
		DotMatrixCanvas.FillRect(bitmap, zone.X, zone.X + width - 1, zone.Y, zone.Bottom - 1, on);

		if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
		{
			var markerX = zone.X + (int)Math.Round(Math.Clamp(peak, 0f, 1f) * (zone.Width - 1));
			DotMatrixCanvas.FillRect(bitmap, markerX, markerX, zone.Y, zone.Bottom - 1, on);
		}
	}

	private static void RenderVerticalBarVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var level = Math.Clamp(vu.Level, 0f, 1f);
		var height = (int)Math.Round(level * zone.Height, MidpointRounding.AwayFromZero);
		DotMatrixCanvas.FillRect(bitmap, zone.X, zone.Right - 1, zone.Bottom - height, zone.Bottom - 1, on);

		if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
		{
			var markerY = zone.Bottom - 1 - (int)Math.Round(Math.Clamp(peak, 0f, 1f) * (zone.Height - 1));
			DotMatrixCanvas.FillRect(bitmap, zone.X, zone.Right - 1, markerY, markerY, on);
		}
	}

	private static void RenderNeedleVu(byte[] bitmap, VuWidget vu, bool on)
	{
		var zone = vu.Zone;
		var centerX = zone.X + (zone.Width / 2);
		var centerY = zone.Bottom - 1;
		var radius = Math.Max(1, Math.Min((zone.Width / 2) - 1, zone.Height - 1));

		// Transform the logical gauge sweep so a bottom-pivot needle reads quiet-left to loud-right.
		var startAngle = 90.0 - vu.NeedleStartDegrees;
		var sweepAngle = -vu.NeedleSweepDegrees;

		if (ResolveNeedleDetailMode(vu, zone) == VuNeedleDetailMode.Detailed)
		{
			for (var t = 0; t <= 4; t++)
			{
				var angle = DotMatrixCanvas.DegreesToRadians(startAngle + (t * (sweepAngle / 4.0)));
				var tx = centerX + (int)Math.Round(Math.Cos(angle) * radius);
				var ty = centerY - (int)Math.Round(Math.Sin(angle) * radius);
				DotMatrixCanvas.SetPixel(bitmap, tx, ty, on);
			}
		}

		var levelAngle = DotMatrixCanvas.DegreesToRadians(startAngle + (Math.Clamp(vu.Level, 0f, 1f) * sweepAngle));
		var x2 = centerX + (int)Math.Round(Math.Cos(levelAngle) * radius);
		var y2 = centerY - (int)Math.Round(Math.Sin(levelAngle) * radius);
		DotMatrixCanvas.DrawLine(bitmap, centerX, centerY, x2, y2, on);

		if (vu.ShowPeakMarker && vu.PeakLevel is float peak)
		{
			var peakAngle = DotMatrixCanvas.DegreesToRadians(startAngle + (Math.Clamp(peak, 0f, 1f) * sweepAngle));
			RenderNeedlePeakArc(bitmap, centerX, centerY, radius, peakAngle, on);
		}
	}

	private static void RenderNeedlePeakArc(byte[] bitmap, int centerX, int centerY, int radius, double peakAngle, bool on)
	{
		const double HalfSpanRadians = Math.PI / 48.0; // ~3.75° each side of the peak
		var arcStart = peakAngle - HalfSpanRadians;
		var arcEnd = peakAngle + HalfSpanRadians;
		var angleDelta = arcEnd - arcStart;
		var steps = Math.Max(1, (int)Math.Ceiling(Math.Abs(angleDelta) / (Math.PI / 180.0))); // ~1° steps

		for (var i = 0; i <= steps; i++)
		{
			var t = i / (double)steps;
			var angle = arcStart + (angleDelta * t);
			var x = centerX + (int)Math.Round(Math.Cos(angle) * radius);
			var y = centerY - (int)Math.Round(Math.Sin(angle) * radius);
			DotMatrixCanvas.SetPixel(bitmap, x, y, on);
		}
	}

	private static VuNeedleDetailMode ResolveNeedleDetailMode(VuWidget vu, DisplayZone zone)
	{
		if (vu.NeedleDetailMode != VuNeedleDetailMode.Auto)
		{
			return vu.NeedleDetailMode;
		}

		return zone.Width >= 16 && zone.Height >= 8
			? VuNeedleDetailMode.Detailed
			: VuNeedleDetailMode.Simple;
	}
}
