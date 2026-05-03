using Maschine.Api.Models;

namespace Maschine.Api.Internal;

internal sealed class LedBrightnessController
{
	private int _percent;

	internal LedBrightnessController(int percent)
	{
		Percent = percent;
	}

	internal int Percent
	{
		get => Volatile.Read(ref _percent);
		set => Volatile.Write(ref _percent, Math.Clamp(value, 0, 100));
	}

	internal byte Scale(byte value)
	{
		var percent = Percent;
		if (percent == 100)
		{
			return value;
		}

		return (byte)((value * percent + 50) / 100);
	}

	internal PadColor Scale(PadColor color)
		=> new(Scale(color.R), Scale(color.G), Scale(color.B));
}
