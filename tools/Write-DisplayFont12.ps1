$ErrorActionPreference = 'Stop'

$arrays = Get-Content "$env:TEMP\helv12-arrays.txt" -Raw
$target = "c:\Users\david\source\repos\panoramicdata\Maschine.Api\Maschine.Api\Internal\DisplayFont12.cs"

$header = @"
namespace Maschine.Api.Internal;

internal static partial class DisplayFont
{
	/// <summary>Glyph height in rows for the 12px Helvetica-derived font set.</summary>
	internal const int Font12Height = 12;

	/// <summary>
	/// Helvetica-derived proportional regular glyphs for ASCII 0x20-0x7E.
	/// Source BDF: Adobe X11 75dpi <c>helvR10.bdf</c>, baseline-normalized to 12 rows.
	/// License: permissive Adobe/DEC X11 terms (see THIRD-PARTY-NOTICES.md).
	/// Bit convention: bit 0 is the leftmost pixel.
	/// </summary>
"@

$footer = @"

	/// <summary>Returns the 12 row-words for the given ASCII character from the regular 12px font.</summary>
	internal static ReadOnlySpan<ushort> GetGlyph12Regular(char c)
	{
		var idx = c < 0x20 || c > 0x7E ? 0 : c - 0x20;
		var data = Font12x12HelvRegularGlyphs;
		return data.Slice(idx * Font12Height, Font12Height);
	}

	/// <summary>Returns the 12 row-words for the given ASCII character from the bold 12px font.</summary>
	internal static ReadOnlySpan<ushort> GetGlyph12Bold(char c)
	{
		var idx = c < 0x20 || c > 0x7E ? 0 : c - 0x20;
		var data = Font12x12HelvBoldGlyphs;
		return data.Slice(idx * Font12Height, Font12Height);
	}
}
"@

Set-Content -Path $target -Value ($header + $arrays + $footer) -Encoding UTF8
Write-Host "Wrote $target"
(Get-Content $target | Measure-Object -Line).Lines | Write-Host
