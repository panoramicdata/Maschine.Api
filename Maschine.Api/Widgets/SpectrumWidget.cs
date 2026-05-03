using Maschine.Api.Models;

namespace Maschine.Api.Widgets;

/// <summary>
/// Spectrum widget rendered as vertical bands with optional peak markers.
/// </summary>
public class SpectrumWidget : DotMatrixWidgetBase
{
	private float[] _levels;
	private float[] _peaks;
	private int[] _peakHoldCountdown;

	/// <summary>
	/// Current normalized band levels.
	/// </summary>
	public IReadOnlyList<float> BandLevels => _levels;

	/// <summary>
	/// Minimum input level mapped to 0.
	/// </summary>
	public float MinLevel { get; set; }

	/// <summary>
	/// Maximum input level mapped to 1.
	/// </summary>
	public float MaxLevel { get; set; } = 1f;

	/// <summary>
	/// Band spacing in pixels.
	/// </summary>
	public int GapPixels { get; set; }

	/// <summary>
	/// True to draw per-band peak markers.
	/// </summary>
	public bool ShowPeakMarkers { get; set; } = true;

	/// <summary>
	/// Frames to hold a peak marker before decay starts.
	/// </summary>
	public int PeakHoldFrames { get; set; } = 8;

	/// <summary>
	/// Peak decay per animation frame.
	/// </summary>
	public float PeakDecayPerFrame { get; set; } = 0.04f;

	/// <summary>
	/// Rise smoothing factor (0..1). Larger values rise faster.
	/// </summary>
	public float ResponseRise { get; set; } = 0.55f;

	/// <summary>
	/// Fall smoothing factor (0..1). Larger values fall faster.
	/// </summary>
	public float ResponseFall { get; set; } = 0.15f;

	/// <summary>
	/// Current normalized peak levels.
	/// </summary>
	public IReadOnlyList<float> PeakLevels => _peaks;

	/// <summary>
	/// Creates a spectrum widget.
	/// </summary>
	public SpectrumWidget(string id, DisplayZone zone, IReadOnlyList<float> bandLevels, bool invert = false)
		: base(id, zone, invert)
	{
		_levels = NormalizeLevels(bandLevels ?? []);
		_peaks = _levels.ToArray();
		_peakHoldCountdown = new int[_levels.Length];
	}

	/// <summary>
	/// Replaces current levels without smoothing.
	/// </summary>
	public void SetLevels(IReadOnlyList<float> levels)
	{
		_levels = NormalizeLevels(levels ?? []);
		_peaks = _levels.ToArray();
		_peakHoldCountdown = new int[_levels.Length];
	}

	/// <summary>
	/// Advances levels and peaks using smoothing and decay parameters.
	/// </summary>
	public void Advance(IReadOnlyList<float> sourceLevels)
	{
		var normalizedSource = NormalizeLevels(sourceLevels ?? []);
		EnsureCapacity(normalizedSource.Length);

		for (var i = 0; i < normalizedSource.Length; i++)
		{
			var source = normalizedSource[i];
			var current = _levels[i];
			var factor = source >= current ? Math.Clamp(ResponseRise, 0f, 1f) : Math.Clamp(ResponseFall, 0f, 1f);
			_levels[i] = current + ((source - current) * factor);

			if (_levels[i] >= _peaks[i])
			{
				_peaks[i] = _levels[i];
				_peakHoldCountdown[i] = Math.Max(0, PeakHoldFrames);
			}
			else if (_peakHoldCountdown[i] > 0)
			{
				_peakHoldCountdown[i]--;
			}
			else
			{
				_peaks[i] = Math.Max(_levels[i], _peaks[i] - Math.Clamp(PeakDecayPerFrame, 0f, 1f));
			}
		}
	}

	private void EnsureCapacity(int length)
	{
		if (_levels.Length == length)
		{
			return;
		}

		var newLevels = new float[length];
		var newPeaks = new float[length];
		var newHolds = new int[length];
		var copy = Math.Min(length, _levels.Length);
		Array.Copy(_levels, newLevels, copy);
		Array.Copy(_peaks, newPeaks, copy);
		Array.Copy(_peakHoldCountdown, newHolds, copy);
		_levels = newLevels;
		_peaks = newPeaks;
		_peakHoldCountdown = newHolds;
	}

	private float[] NormalizeLevels(IReadOnlyList<float> levels)
	{
		var result = new float[levels.Count];
		var min = MinLevel;
		var max = MaxLevel;
		if (max <= min)
		{
			max = min + 1f;
		}

		for (var i = 0; i < levels.Count; i++)
		{
			result[i] = Math.Clamp((levels[i] - min) / (max - min), 0f, 1f);
		}

		return result;
	}
}
