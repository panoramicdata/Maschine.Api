using Maschine.Api.Interfaces;
using Maschine.Api.Models;
using Maschine.Api.Widgets;
using Microsoft.Extensions.Logging;

namespace Maschine.Demo;

/// <summary>
/// The dot-matrix showcase for <see cref="DemoController"/>: the animation loop, the dashboards
/// it cycles through, and the remaining one-shot LED routines and pad-mapping helpers.
/// </summary>
internal sealed partial class DemoController
{
	private async Task RunDotMatrixShowcaseAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Dashboard demo started. Knob position selects the active Dashboard.");
		byte[]? previousFrame = null;
		while (!cancellationToken.IsCancellationRequested)
		{
			DotMatrixDashboard dashboard;
			var invert = false;
			var frameNumber = 0;
			lock (_animationSync)
			{
				dashboard = _dashboards[_selectedDashboardIndex];
				invert = _isDashboardInverted;
				frameNumber = _audioFrame++;
			}

			UpdateFakeAudioWidgets(dashboard, frameNumber);

			var frame = dashboard.BuildBitmap();
			if (invert)
			{
				InvertBitmap(frame);
			}

			if (previousFrame is null || !frame.AsSpan().SequenceEqual(previousFrame))
			{
				await _client.SetDotMatrixBitmapAsync(frame, cancellationToken: cancellationToken).ConfigureAwait(false);
				previousFrame = frame;
			}

			var signedVelocity = GetDisplayVelocity();
			dashboard.AdvanceFrame(Math.Sign(signedVelocity));

			var velocity = Math.Abs(signedVelocity);
			var delayMs = velocity == 0 ? 100 : s_zebraSpeedMs[Math.Clamp(velocity, 1, s_zebraSpeedMs.Length) - 1];
			await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
		}
	}

	private int GetDisplayVelocity() => _displayVelocity;

	private static string GetDashboardTitle(int index) => index switch
	{
		0 => "Overview",
		1 => "Status",
		2 => "Mix A",
		3 => "Mix B",
		4 => "Levels",
		5 => "Spectrum",
		6 => "Needle",
		7 => "Mini",
		8 => "Large",
		_ => $"Dashboard {index}",
	};

	private static DotMatrixDashboard[] BuildDashboards()
	{
		return
		[
			BuildOverviewDashboard(),
			BuildStatusDashboard(),
			BuildMixDashboard("A", 1),
			BuildMixDashboard("B", 2),
			BuildLevelsDashboard(),
			BuildSpectrumDashboard(),
			BuildNeedleDashboard(),
			BuildMiniDashboard(),
			BuildLargeDashboard(),
		];
	}

	private static DotMatrixDashboard BuildOverviewDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["Dashboard Overview"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new TextWidget("sub", new DisplayZone(0, 8, 128, 8), ["Knob picks dashboard"], TextOverflowMode.Scroll)
		{
			OverflowStepPixels = 1,
			ScrollPadding = 4,
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new SpectrumWidget("eq", new DisplayZone(0, 16, 64, 16), [0.1f, 0.5f, 0.8f, 0.4f, 0.6f, 0.2f, 0.9f, 0.3f])
		{
			GapPixels = 0,
			ShowPeakMarkers = true,
			PeakHoldFrames = 10,
			PeakDecayPerFrame = 0.02f,
			ResponseRise = 0.65f,
			ResponseFall = 0.2f,
		});
		dashboard.AddWidget(new VuWidget("vu", new DisplayZone(64, 16, 64, 16), VuWidgetStyle.Bar, level: 0.6f, peakLevel: 0.8f)
		{
			PeakHoldFrames = 10,
			PeakDecayPerFrame = 0.02f,
			ResponseRise = 0.65f,
			ResponseFall = 0.2f,
			ShowPeakMarker = true,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildStatusDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("header", new DisplayZone(0, 0, 128, 16), ["Status", "Buttons Pads Encoders"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new TextWidget("ticker", new DisplayZone(0, 16, 128, 16), ["Widgets render inside Dashboards with no overlap allowed"], TextOverflowMode.Scroll)
		{
			OverflowStepPixels = 2,
			ScrollPadding = 5,
			FontKind = TextFontKind.Proportional12,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildMixDashboard(string suffix, int variant)
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 64, 8), [$"Mix {suffix}"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new VuWidget("vuL", new DisplayZone(0, 8, 20, 24), VuWidgetStyle.Bar, level: 0.2f * variant + 0.2f, peakLevel: 0.85f)
		{
			PeakHoldFrames = 6,
			PeakDecayPerFrame = 0.03f,
		});
		dashboard.AddWidget(new VuWidget("vuR", new DisplayZone(22, 8, 20, 24), VuWidgetStyle.Bar, level: 0.3f * variant + 0.1f, peakLevel: 0.9f, invert: true)
		{
			PeakHoldFrames = 6,
			PeakDecayPerFrame = 0.03f,
		});
		dashboard.AddWidget(new SpectrumWidget("eq", new DisplayZone(48, 8, 80, 24), [0.15f, 0.30f, 0.60f, 0.75f, 0.50f, 0.40f, 0.70f, 0.95f])
		{
			GapPixels = 1,
			PeakHoldFrames = 8,
			PeakDecayPerFrame = 0.03f,
			ResponseRise = 0.6f,
			ResponseFall = 0.2f,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildLevelsDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["Levels"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new VuWidget("top", new DisplayZone(0, 8, 128, 8), VuWidgetStyle.Bar, level: 0.35f, peakLevel: 0.5f));
		dashboard.AddWidget(new VuWidget("mid", new DisplayZone(0, 16, 128, 8), VuWidgetStyle.Bar, level: 0.65f, peakLevel: 0.8f, invert: true));
		dashboard.AddWidget(new VuWidget("low", new DisplayZone(0, 24, 128, 8), VuWidgetStyle.Bar, level: 0.9f, peakLevel: 1.0f));
		return dashboard;
	}

	private static DotMatrixDashboard BuildSpectrumDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["Spectrum"], TextOverflowMode.None)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new SpectrumWidget("bands", new DisplayZone(0, 8, 128, 24), [0.05f, 0.12f, 0.20f, 0.35f, 0.50f, 0.75f, 0.95f, 0.85f, 0.65f, 0.45f, 0.30f, 0.18f, 0.10f, 0.06f])
		{
			GapPixels = 1,
			ShowPeakMarkers = true,
			PeakHoldFrames = 14,
			PeakDecayPerFrame = 0.015f,
			ResponseRise = 0.7f,
			ResponseFall = 0.15f,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildNeedleDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 8), ["VU Needle Widgets"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional8,
		});
		dashboard.AddWidget(new VuWidget("left", new DisplayZone(0, 8, 64, 24), VuWidgetStyle.Needle, VuNeedleDetailMode.Detailed, level: 0.3f, peakLevel: 0.45f)
		{
			NeedleStartDegrees = -70,
			NeedleSweepDegrees = 140,
			PeakHoldFrames = 10,
			PeakDecayPerFrame = 0.02f,
		});
		dashboard.AddWidget(new VuWidget("right", new DisplayZone(64, 8, 64, 24), VuWidgetStyle.Needle, VuNeedleDetailMode.Simple, level: 0.75f, peakLevel: 0.85f, invert: true)
		{
			NeedleStartDegrees = -55,
			NeedleSweepDegrees = 110,
			PeakHoldFrames = 6,
			PeakDecayPerFrame = 0.04f,
		});
		return dashboard;
	}

	private static DotMatrixDashboard BuildMiniDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("row1", new DisplayZone(0, 0, 128, 4), ["mini dashboard widget row 1 rotates"], TextOverflowMode.Rotate) { OverflowStepPixels = 1, FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row2", new DisplayZone(0, 4, 128, 4), ["row 2 scrolls with spaces"], TextOverflowMode.Scroll) { OverflowStepPixels = 1, ScrollPadding = 6, FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row3", new DisplayZone(0, 8, 128, 4), ["row 3"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row4", new DisplayZone(0, 12, 128, 4), ["ellipsized widgets still fit"], TextOverflowMode.Ellipsis) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row5", new DisplayZone(0, 16, 128, 4), ["dashboard 7"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row6", new DisplayZone(0, 20, 128, 4), ["widget layout ok"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row7", new DisplayZone(0, 24, 128, 4), ["no overlap allowed"], TextOverflowMode.None) { FontKind = TextFontKind.Proportional4 });
		dashboard.AddWidget(new TextWidget("row8", new DisplayZone(0, 28, 128, 4), ["knob = dashboard"], TextOverflowMode.Ellipsis) { FontKind = TextFontKind.Proportional4 });
		return dashboard;
	}

	private static DotMatrixDashboard BuildLargeDashboard()
	{
		var dashboard = new DotMatrixDashboard();
		dashboard.AddWidget(new TextWidget("title", new DisplayZone(0, 0, 128, 12), ["12px Bold Helvetica"], TextOverflowMode.Ellipsis)
		{
			FontKind = TextFontKind.Proportional12Bold,
		});
		dashboard.AddWidget(new TextWidget("sub", new DisplayZone(0, 12, 128, 12), ["Proportional 12 regular scrolls across the display"], TextOverflowMode.Scroll)
		{
			OverflowStepPixels = 1,
			ScrollPadding = 4,
			FontKind = TextFontKind.Proportional12,
		});
		dashboard.AddWidget(new VuWidget("vuL", new DisplayZone(0, 24, 62, 8), VuWidgetStyle.Bar, level: 0.6f, peakLevel: 0.75f)
		{
			PeakHoldFrames = 8,
			PeakDecayPerFrame = 0.025f,
		});
		dashboard.AddWidget(new VuWidget("vuR", new DisplayZone(66, 24, 62, 8), VuWidgetStyle.Bar, level: 0.4f, peakLevel: 0.55f, invert: true)
		{
			PeakHoldFrames = 8,
			PeakDecayPerFrame = 0.025f,
		});
		return dashboard;
	}

	private async Task TryClearDotMatrixAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _client.ClearDotMatrixAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// Best-effort cleanup.
		}
	}

	private async Task RunLedSelfTestAsync(CancellationToken cancellationToken)
	{
		_logger.LogInformation("Running LED self-test: global colors, pad chase, button chase.");

		var globalColors = new[]
		{
			PadColor.Red,
			PadColor.Green,
			PadColor.Blue,
			PadColor.White,
		};

		foreach (var color in globalColors)
		{
			await TrySetAllLedsAsync(color, 127, "self-test-global", cancellationToken).ConfigureAwait(false);
			await Task.Delay(220, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-reset", cancellationToken).ConfigureAwait(false);

		for (var pad = 0; pad < MaschineDeviceConstants.MikroMk3PadCount; pad++)
		{
			await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-pad-reset", cancellationToken).ConfigureAwait(false);
			await TrySetPadColorAsync(pad, GetPadBaseColor(pad)).ConfigureAwait(false);
			_logger.LogInformation("Self-test pad chase: P{Pad,2}", pad);
			await Task.Delay(120, cancellationToken).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-before-buttons", cancellationToken).ConfigureAwait(false);

		for (var button = 0; button < MaschineDeviceConstants.MikroMk3ButtonCount; button++)
		{
			await TrySetButtonLedAsync(button, 127).ConfigureAwait(false);
			_logger.LogInformation("Self-test button chase: B{Button,2}", button);
			await Task.Delay(80, cancellationToken).ConfigureAwait(false);
			await TrySetButtonLedAsync(button, 0).ConfigureAwait(false);
		}

		await TrySetAllLedsAsync(PadColor.Off, 0, "self-test-complete", cancellationToken).ConfigureAwait(false);
		_logger.LogInformation("LED self-test complete. Interactive mode continues.");
	}

	// ── Helpers ─────────────────────────────────────────────────────────────

	private static PadColor GetPadBaseColor(int rawPadIndex)
	{
		var mappedIndex = MapPadIndexWithVerticalFlip(rawPadIndex);
		return s_padColors[mappedIndex];
	}

	private static int MapPadIndexWithVerticalFlip(int rawPadIndex)
	{
		var row = rawPadIndex / 4;
		var col = rawPadIndex % 4;
		var flippedRow = 3 - row;
		return (flippedRow * 4) + col;
	}

	private static int ToUserPadNumber(int rawPadIndex)
		=> MapPadIndexWithVerticalFlip(rawPadIndex) + 1;

	private static string FormatColor(PadColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

	private static void InvertBitmap(byte[] bitmap)
	{
		for (var i = 0; i < bitmap.Length; i++)
		{
			bitmap[i] = (byte)~bitmap[i];
		}
	}

	private static void UpdateFakeAudioWidgets(DotMatrixDashboard dashboard, int frame)
	{
		var time = frame / 30.0;
		foreach (var widget in dashboard.Widgets)
		{
			// Only the animated widget kinds are driven here; anything else is left as-is.
			if (widget is VuWidget vu)
			{
				AdvanceFakeVu(vu, time);
			}
			else if (widget is SpectrumWidget spectrum)
			{
				AdvanceFakeSpectrum(spectrum, time);
			}
		}
	}

	private static void AdvanceFakeVu(VuWidget vu, double time)
	{
		var phase = (Math.Abs(vu.Id.GetHashCode()) % 13) * 0.37;
		var signal = 0.5 + (0.5 * Math.Sin((time * 2.8) + phase));
		var wobble = 0.12 * Math.Sin((time * 11.0) + (phase * 2));
		vu.Advance((float)Math.Clamp(signal + wobble, 0.0, 1.0));
	}

	private static void AdvanceFakeSpectrum(SpectrumWidget spectrum, double time)
	{
		var bandCount = spectrum.BandLevels.Count;
		if (bandCount == 0)
		{
			return;
		}

		var levels = new float[bandCount];
		for (var i = 0; i < bandCount; i++)
		{
			var norm = i / Math.Max(1.0, bandCount - 1.0);
			var sweep = 0.5 + (0.5 * Math.Sin((time * 3.3) + (norm * 8.0)));
			var bass = 0.3 * Math.Sin((time * 1.2) + (norm * 2.0));
			var sparkle = 0.12 * Math.Sin((time * 14.0) + (i * 0.7));
			levels[i] = (float)Math.Clamp((0.1 + (0.8 * sweep) + bass + sparkle), 0.0, 1.0);
		}

		spectrum.Advance(levels);
	}

	private void PrintMappings()
	{
		_logger.LogInformation("=== Maschine Mikro MK3 Reactive Demo ===");
		_logger.LogInformation("Behavior:");
		_logger.LogInformation("  PAD MODE / KEYBOARD / CHORDS -> API radio group (one lit), press active mode again to cycle 3 sounds");
		_logger.LogInformation("  Button LEDs are managed by API key modes (except explicit EventsOnly keys)");
		_logger.LogInformation("  Pad press     -> flash white and trigger a velocity-aware drum hit");
		_logger.LogInformation("  Knob          -> selects the active Dashboard by absolute position");
		_logger.LogInformation("  Logo button   -> toggles Dashboard invert mode");
		_logger.LogInformation("  Slider        -> updates strip LEDs and controls demo drum volume");
		_logger.LogInformation("  Dashboard     -> whole display made of non-overlapping Widgets");
		_logger.LogInformation("  Demo pages     -> {DashboardCount} Dashboards available", _dashboards.Length);
	}
}
