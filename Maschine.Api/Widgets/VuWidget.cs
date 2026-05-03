using Maschine.Api.Models;

namespace Maschine.Api.Widgets;

/// <summary>
/// VU widget supporting bar and needle styles.
/// </summary>
public sealed class VuWidget : DotMatrixWidgetBase
{
	private float _level;
	private float? _peak;
	private int _peakHoldCountdown;

	/// <summary>
	/// VU render style.
	/// </summary>
	public VuWidgetStyle Style { get; set; }

	/// <summary>
	/// Needle detail mode used when <see cref="Style"/> is <see cref="VuWidgetStyle.Needle"/>.
	/// </summary>
	public VuNeedleDetailMode NeedleDetailMode { get; set; }

	/// <summary>
	/// Current level normalized to 0..1.
	/// </summary>
	public float Level
	{
		get => _level;
		set => _level = Math.Clamp(value, 0f, 1f);
	}

	/// <summary>
	/// Optional peak marker level normalized to 0..1.
	/// </summary>
	public float? PeakLevel
	{
		get => _peak;
		set => _peak = value is null ? null : Math.Clamp(value.Value, 0f, 1f);
	}

	/// <summary>
	/// Minimum input level mapped to 0.
	/// </summary>
	public float MinLevel { get; set; }

	/// <summary>
	/// Maximum input level mapped to 1.
	/// </summary>
	public float MaxLevel { get; set; } = 1f;

	/// <summary>
	/// Rise smoothing factor (0..1). Larger values rise faster.
	/// </summary>
	public float ResponseRise { get; set; } = 0.6f;

	/// <summary>
	/// Fall smoothing factor (0..1). Larger values fall faster.
	/// </summary>
	public float ResponseFall { get; set; } = 0.2f;

	/// <summary>
	/// Frames to hold the peak before decay starts.
	/// </summary>
	public int PeakHoldFrames { get; set; } = 8;

	/// <summary>
	/// Peak decay amount per frame.
	/// </summary>
	public float PeakDecayPerFrame { get; set; } = 0.03f;

	/// <summary>
	/// True to render a peak marker.
	/// </summary>
	public bool ShowPeakMarker { get; set; } = true;

	/// <summary>
	/// Start angle in degrees for needle mode.
	/// </summary>
	public float NeedleStartDegrees { get; set; } = -60f;

	/// <summary>
	/// Sweep angle in degrees for needle mode.
	/// </summary>
	public float NeedleSweepDegrees { get; set; } = 120f;

	/// <summary>
	/// Creates a VU widget.
	/// </summary>
	/// <param name="id">Stable widget identifier.</param>
	/// <param name="zone">Widget display zone in pixels.</param>
	/// <param name="style">VU render style.</param>
	/// <param name="needleDetailMode">Needle detail mode for needle rendering.</param>
	/// <param name="level">Current level normalized to 0..1.</param>
	/// <param name="peakLevel">Optional peak level normalized to 0..1.</param>
	/// <param name="invert">Whether to invert widget colors.</param>
	public VuWidget(string id, DisplayZone zone, VuWidgetStyle style, VuNeedleDetailMode needleDetailMode = VuNeedleDetailMode.Auto, float level = 0f, float? peakLevel = null, bool invert = false)
		: base(id, zone, invert)
	{
		Style = style;
		NeedleDetailMode = needleDetailMode;
		Level = level;
		PeakLevel = peakLevel;
	}

	/// <summary>
	/// Advances the VU reading with smoothing and peak hold/decay.
	/// </summary>
	/// <param name="sourceLevel">Input source level.</param>
	public void Advance(float sourceLevel)
	{
		var min = MinLevel;
		var max = MaxLevel;
		if (max <= min)
		{
			max = min + 1f;
		}

		var target = Math.Clamp((sourceLevel - min) / (max - min), 0f, 1f);
		var factor = target >= Level ? Math.Clamp(ResponseRise, 0f, 1f) : Math.Clamp(ResponseFall, 0f, 1f);
		Level += (target - Level) * factor;

		if (!ShowPeakMarker)
		{
			PeakLevel = null;
			return;
		}

		var peak = PeakLevel ?? 0f;
		if (Level >= peak)
		{
			PeakLevel = Level;
			_peakHoldCountdown = Math.Max(0, PeakHoldFrames);
		}
		else if (_peakHoldCountdown > 0)
		{
			_peakHoldCountdown--;
		}
		else
		{
			PeakLevel = Math.Max(Level, peak - Math.Clamp(PeakDecayPerFrame, 0f, 1f));
		}
	}
}
