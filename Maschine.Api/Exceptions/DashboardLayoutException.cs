namespace Maschine.Api.Exceptions;

/// <summary>
/// Thrown when a dashboard contains an invalid widget layout.
/// </summary>
public sealed class DashboardLayoutException : Exception
{
	/// <summary>
	/// Creates a new dashboard layout exception.
	/// </summary>
	/// <param name="message">Error message describing the invalid layout.</param>
	public DashboardLayoutException(string message)
		: base(message)
	{
	}
}
