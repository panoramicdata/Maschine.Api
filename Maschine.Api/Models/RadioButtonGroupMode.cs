namespace Maschine.Api.Models;

/// <summary>
/// Selection behavior for a key radio button group.
/// </summary>
public enum RadioButtonGroupMode
{
	/// <summary>Exactly one key in the group is always on.</summary>
	AlwaysOneOn = 0,

	/// <summary>At most one key in the group is on; all-off is allowed.</summary>
	OneOrZeroOn = 1,
}
