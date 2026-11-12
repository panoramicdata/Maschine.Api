using Maschine.Api.Widgets;

namespace Maschine.Demo;

/// <summary>
/// Synthetic audio levels that animate the VU and spectrum widgets in the demo.
/// </summary>
internal static class DemoFakeAudio
{
	internal static void Update(DotMatrixDashboard dashboard, int frame)
	{
		var time = frame / 30.0;
		foreach (var widget in dashboard.Widgets)
		{
			// Only the animated widget kinds are driven here; anything else is left as-is.
			if (widget is VuWidget vu)
			{
				AdvanceVu(vu, time);
			}
			else if (widget is SpectrumWidget spectrum)
			{
				AdvanceSpectrum(spectrum, time);
			}
		}
	}

	private static void AdvanceVu(VuWidget vu, double time)
	{
		var phase = (Math.Abs(vu.Id.GetHashCode()) % 13) * 0.37;
		var signal = 0.5 + (0.5 * Math.Sin((time * 2.8) + phase));
		var wobble = 0.12 * Math.Sin((time * 11.0) + (phase * 2));
		vu.Advance((float)Math.Clamp(signal + wobble, 0.0, 1.0));
	}

	private static void AdvanceSpectrum(SpectrumWidget spectrum, double time)
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
}
