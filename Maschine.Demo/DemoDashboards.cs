using Maschine.Api.Models;
using Maschine.Api.Widgets;

namespace Maschine.Demo;

/// <summary>
/// The dashboards the demo cycles through with the knob.
/// </summary>
internal static class DemoDashboards
{
	private static readonly string[] s_titles =
	[
		"Overview", "Status", "Mix A", "Mix B", "Levels", "Spectrum", "Needle", "Mini", "Large",
	];

	private static readonly (string Id, string Line, TextOverflowMode Overflow, int? Step, int? Padding)[] s_miniRows =
	[
		("row1", "mini dashboard widget row 1 rotates", TextOverflowMode.Rotate, 1, null),
		("row2", "row 2 scrolls with spaces", TextOverflowMode.Scroll, 1, 6),
		("row3", "row 3", TextOverflowMode.None, null, null),
		("row4", "ellipsized widgets still fit", TextOverflowMode.Ellipsis, null, null),
		("row5", "dashboard 7", TextOverflowMode.None, null, null),
		("row6", "widget layout ok", TextOverflowMode.None, null, null),
		("row7", "no overlap allowed", TextOverflowMode.None, null, null),
		("row8", "knob = dashboard", TextOverflowMode.Ellipsis, null, null),
	];

	internal static string GetTitle(int index)
		=> index >= 0 && index < s_titles.Length ? s_titles[index] : $"Dashboard {index}";

	internal static DotMatrixDashboard[] Build()
	{
		return
		[
			Compose(
				Title("title", "Dashboard Overview", TextOverflowMode.Ellipsis),
				Text("sub", new DisplayZone(0, 8, 128, 8), ["Knob picks dashboard"], TextOverflowMode.Scroll, TextFontKind.Proportional8, 1, 4),
				Spectrum("eq", new DisplayZone(0, 16, 64, 16), [0.1f, 0.5f, 0.8f, 0.4f, 0.6f, 0.2f, 0.9f, 0.3f], new SpectrumTuning(0, 10, 0.02f, 0.65f, 0.2f)),
				Bar("vu", new DisplayZone(64, 16, 64, 16), 0.6f, 0.8f, false, new VuTuning(10, 0.02f, 0.65f))),
			Compose(
				Text("header", new DisplayZone(0, 0, 128, 16), ["Status", "Buttons Pads Encoders"], TextOverflowMode.Ellipsis, TextFontKind.Proportional8),
				Text("ticker", new DisplayZone(0, 16, 128, 16), ["Widgets render inside Dashboards with no overlap allowed"], TextOverflowMode.Scroll, TextFontKind.Proportional12, 2, 5)),
			BuildMix("A", 1),
			BuildMix("B", 2),
			Compose(
				Title("title", "Levels", TextOverflowMode.None),
				Bar("top", new DisplayZone(0, 8, 128, 8), 0.35f, 0.5f, false),
				Bar("mid", new DisplayZone(0, 16, 128, 8), 0.65f, 0.8f, true),
				Bar("low", new DisplayZone(0, 24, 128, 8), 0.9f, 1.0f, false)),
			Compose(
				Title("title", "Spectrum", TextOverflowMode.None),
				Spectrum("bands", new DisplayZone(0, 8, 128, 24), [0.05f, 0.12f, 0.20f, 0.35f, 0.50f, 0.75f, 0.95f, 0.85f, 0.65f, 0.45f, 0.30f, 0.18f, 0.10f, 0.06f], new SpectrumTuning(1, 14, 0.015f, 0.7f, 0.15f))),
			Compose(
				Title("title", "VU Needle Widgets", TextOverflowMode.Ellipsis),
				Needle("left", 0, VuNeedleDetailMode.Detailed, 0.3f, 0.45f, false, new NeedleTuning(-70, 140, 10, 0.02f)),
				Needle("right", 64, VuNeedleDetailMode.Simple, 0.75f, 0.85f, true, new NeedleTuning(-55, 110, 6, 0.04f))),
			Compose(s_miniRows.Select((row, i) => Text(row.Id, new DisplayZone(0, i * 4, 128, 4), [row.Line], row.Overflow, TextFontKind.Proportional4, row.Step, row.Padding)).ToArray()),
			Compose(
				Text("title", new DisplayZone(0, 0, 128, 12), ["12px Bold Helvetica"], TextOverflowMode.Ellipsis, TextFontKind.Proportional12Bold),
				Text("sub", new DisplayZone(0, 12, 128, 12), ["Proportional 12 regular scrolls across the display"], TextOverflowMode.Scroll, TextFontKind.Proportional12, 1, 4),
				Bar("vuL", new DisplayZone(0, 24, 62, 8), 0.6f, 0.75f, false, new VuTuning(8, 0.025f)),
				Bar("vuR", new DisplayZone(66, 24, 62, 8), 0.4f, 0.55f, true, new VuTuning(8, 0.025f))),
		];
	}

	private static DotMatrixDashboard BuildMix(string suffix, int variant)
		=> Compose(
			Text("title", new DisplayZone(0, 0, 64, 8), [$"Mix {suffix}"], TextOverflowMode.None, TextFontKind.Proportional8),
			Bar("vuL", new DisplayZone(0, 8, 20, 24), (0.2f * variant) + 0.2f, 0.85f, false, new VuTuning(6, 0.03f)),
			Bar("vuR", new DisplayZone(22, 8, 20, 24), (0.3f * variant) + 0.1f, 0.9f, true, new VuTuning(6, 0.03f)),
			Spectrum("eq", new DisplayZone(48, 8, 80, 24), [0.15f, 0.30f, 0.60f, 0.75f, 0.50f, 0.40f, 0.70f, 0.95f], new SpectrumTuning(1, 8, 0.03f, 0.6f, 0.2f)));

	private static DotMatrixDashboard Compose(params IDotMatrixWidget[] widgets)
	{
		var dashboard = new DotMatrixDashboard();
		foreach (var widget in widgets)
		{
			dashboard.AddWidget(widget);
		}

		return dashboard;
	}

	private static TextWidget Title(string id, string line, TextOverflowMode overflow)
		=> Text(id, new DisplayZone(0, 0, 128, 8), [line], overflow, TextFontKind.Proportional8);

	private static TextWidget Text(string id, DisplayZone zone, string[] lines, TextOverflowMode overflow, TextFontKind font, int? step = null, int? padding = null)
		=> new(id, zone, lines, overflow)
		{
			FontKind = font,
			OverflowStepPixels = step ?? 1,
			ScrollPadding = padding ?? 3,
		};

	private static VuWidget Bar(string id, DisplayZone zone, float level, float peak, bool invert, VuTuning? tuning = null)
	{
		var t = tuning ?? new VuTuning(8, 0.03f);
		return new(id, zone, VuWidgetStyle.Bar, level, peak, invert)
		{
			PeakHoldFrames = t.HoldFrames,
			PeakDecayPerFrame = t.DecayPerFrame,
			ResponseRise = t.Rise,
			ResponseFall = 0.2f,
		};
	}

	private static VuWidget Needle(string id, int x, VuNeedleDetailMode detail, float level, float peak, bool invert, NeedleTuning tuning)
		=> new(id, new DisplayZone(x, 8, 64, 24), VuWidgetStyle.Needle, detail, level, peak, invert)
		{
			NeedleStartDegrees = tuning.StartDegrees,
			NeedleSweepDegrees = tuning.SweepDegrees,
			PeakHoldFrames = tuning.HoldFrames,
			PeakDecayPerFrame = tuning.DecayPerFrame,
		};

	private static SpectrumWidget Spectrum(string id, DisplayZone zone, float[] bands, SpectrumTuning tuning)
		=> new(id, zone, bands)
		{
			GapPixels = tuning.Gap,
			ShowPeakMarkers = true,
			PeakHoldFrames = tuning.HoldFrames,
			PeakDecayPerFrame = tuning.DecayPerFrame,
			ResponseRise = tuning.Rise,
			ResponseFall = tuning.Fall,
		};

	private readonly record struct VuTuning(int HoldFrames, float DecayPerFrame, float Rise = 0.6f);

	private readonly record struct NeedleTuning(float StartDegrees, float SweepDegrees, int HoldFrames, float DecayPerFrame);

	private readonly record struct SpectrumTuning(int Gap, int HoldFrames, float DecayPerFrame, float Rise, float Fall);
}
