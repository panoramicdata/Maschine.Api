namespace Maschine.Api.Models;

/// <summary>
/// Pixel rectangle on the 128x32 dot-matrix display.
/// </summary>
public readonly record struct DisplayZone
{
	/// <summary>Left coordinate in pixels.</summary>
	public int X { get; }
	/// <summary>Top coordinate in pixels.</summary>
	public int Y { get; }
	/// <summary>Width in pixels.</summary>
	public int Width { get; }
	/// <summary>Height in pixels.</summary>
	public int Height { get; }

	/// <summary>Right edge (exclusive).</summary>
	public int Right => X + Width;
	/// <summary>Bottom edge (exclusive).</summary>
	public int Bottom => Y + Height;

	/// <summary>
	/// Creates a new display zone.
	/// </summary>
	/// <param name="x">Left coordinate in pixels.</param>
	/// <param name="y">Top coordinate in pixels.</param>
	/// <param name="width">Width in pixels.</param>
	/// <param name="height">Height in pixels.</param>
	public DisplayZone(int x, int y, int width, int height)
	{
		if (width <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be > 0.");
		}

		if (height <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be > 0.");
		}

		X = x;
		Y = y;
		Width = width;
		Height = height;
	}

	/// <summary>
	/// Returns <see langword="true"/> when this zone intersects another zone.
	/// </summary>
	public bool Intersects(DisplayZone other)
		=> X < other.Right && Right > other.X && Y < other.Bottom && Bottom > other.Y;

	/// <summary>
	/// Returns <see langword="true"/> when this zone fits inside the provided bounds.
	/// </summary>
	public bool IsWithin(int width, int height)
		=> X >= 0 && Y >= 0 && Right <= width && Bottom <= height;
}
