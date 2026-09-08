namespace Maschine.Demo;

/// <summary>
/// The one-shot modes the demo can be started in, selected from the command line.
/// </summary>
/// <param name="LedSelfTest">Run the LED self-test sweep.</param>
/// <param name="FullBrightness">Drive every pad and button LED to full brightness.</param>
/// <param name="PadColorSpace">Show the pad colour gamut.</param>
/// <param name="DisplayTest">Show the dot-matrix test pattern.</param>
/// <param name="DisplayZebra">Show the dot-matrix zebra pattern.</param>
/// <param name="DisplayShowcase">Run the animated dot-matrix dashboard showcase.</param>
internal readonly record struct DemoModes(
	bool LedSelfTest = false,
	bool FullBrightness = false,
	bool PadColorSpace = false,
	bool DisplayTest = false,
	bool DisplayZebra = false,
	bool DisplayShowcase = false)
{
	/// <summary>
	/// Whether the demo should react to pads, buttons and encoders. The LED-oriented modes take
	/// exclusive control of the surface, so interactive handling is suppressed for them.
	/// </summary>
	public bool IsInteractive => !FullBrightness && !PadColorSpace;
}
