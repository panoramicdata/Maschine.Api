using System.ComponentModel.DataAnnotations;

namespace Maschine.Api.Models;

/// <summary>
/// Helpers for converting button enum values to user-facing labels.
/// </summary>
public static class MikroMk3ButtonExtensions
{
	/// <summary>
	/// Attempts to map a raw button index to a known <see cref="MikroMk3Button"/> enum value.
	/// </summary>
	/// <param name="index">Zero-based raw button index from HID reports.</param>
	/// <param name="button">Mapped enum value when available.</param>
	/// <returns><see langword="true"/> when a mapping exists; otherwise <see langword="false"/>.</returns>
	public static bool TryFromIndex(int index, out MikroMk3Button button)
	{
		if (Enum.IsDefined(typeof(MikroMk3Button), index))
		{
			button = (MikroMk3Button)index;
			return true;
		}

		button = default;
		return false;
	}

	/// <summary>
	/// Gets the controller's custom button number for a named button.
	/// </summary>
	public static int ToCustomNumber(this MikroMk3Button button) => (int)button;

	/// <summary>
	/// Gets a human-friendly button label from <see cref="DisplayAttribute.Name"/>.
	/// Falls back to the enum member name when no display name is present.
	/// </summary>
	public static string GetDisplayName(this MikroMk3Button button)
	{
		var member = typeof(MikroMk3Button).GetMember(button.ToString());
		if (member.Length == 0)
		{
			return button.ToString();
		}

		var display = (DisplayAttribute?)Attribute.GetCustomAttribute(member[0], typeof(DisplayAttribute));
		return string.IsNullOrWhiteSpace(display?.Name) ? button.ToString() : display.Name!;
	}

	/// <summary>
	/// Gets a human-friendly label without line breaks for compact logging.
	/// </summary>
	public static string GetDisplayNameSingleLine(this MikroMk3Button button)
		=> button.GetDisplayName().Replace("\r\n", " / ", StringComparison.Ordinal).Replace("\n", " / ", StringComparison.Ordinal);
}
