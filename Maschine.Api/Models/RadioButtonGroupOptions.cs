namespace Maschine.Api.Models;

/// <summary>
/// Startup configuration for a key radio button group.
/// </summary>
public sealed class RadioButtonGroupOptions
{
	/// <summary>
	/// Group mode. Defaults to <see cref="RadioButtonGroupMode.AlwaysOneOn"/>.
	/// </summary>
	public RadioButtonGroupMode Mode { get; set; } = RadioButtonGroupMode.AlwaysOneOn;

	/// <summary>
	/// Ordered keys in this group. For <see cref="RadioButtonGroupMode.AlwaysOneOn"/>,
	/// the first key is selected initially.
	/// </summary>
	public IReadOnlyList<MikroMk3Button> Keys { get; set; } = [];
}
