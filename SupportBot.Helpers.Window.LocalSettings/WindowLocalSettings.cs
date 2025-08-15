namespace SupportBot.Helpers.Window.LocalSettings;

/// <summary>
/// Provides helper methods for persisting and retrieving the window position using the application's
/// local settings storage (roamed only within the local machine profile).
/// </summary>
/// <remarks>
/// Thread-safety: Windows.Storage.ApplicationData.Current.LocalSettings is internally synchronized for simple
/// value writes/reads; no additional locking is required for the current lightweight usage.
/// Data Format: The position is stored as a comma-separated string "X,Y" using invariant formatting implied
/// by <see cref="double.ToString()"/> / <see cref="double.TryParse(string?, out double)"/> without explicit culture.
/// Failure Handling: Retrieval returns <c>null</c> when the stored value is absent or malformed.
/// </remarks>
public static class WindowLocalSettings
{
    /// <summary>
    /// The key under which the window position (X,Y) is stored inside local settings.
    /// </summary>
    private const string WINDOW_POSITION_KEY = "WindowPosition";

    /// <summary>
    /// Saves the window position to the local application settings.
    /// </summary>
    /// <param name="x">The X coordinate (horizontal) of the window's top-left corner.</param>
    /// <param name="y">The Y coordinate (vertical) of the window's top-left corner.</param>
    /// <remarks>
    /// Overwrites any existing stored position. Coordinates are persisted as a simple comma-delimited string.
    /// </remarks>
    public static void SaveWindowPosition(double x, double y)
    {
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        var position = new { X = x, Y = y };
        localSettings.Values[WINDOW_POSITION_KEY] = $"{position.X},{position.Y}";
    }

    /// <summary>
    /// Retrieves the previously stored window position from local application settings.
    /// </summary>
    /// <returns>
    /// A tuple (x, y) representing the stored coordinates, or <c>null</c> if no valid position is stored.
    /// </returns>
    /// <remarks>
    /// Parsing fails gracefully: any malformed or incomplete stored value results in a <c>null</c> return.
    /// </remarks>
    public static (double x, double y)? GetWindowPosition()
    {
        var localSettings = Windows.Storage.ApplicationData.Current.LocalSettings;
        if (localSettings.Values.TryGetValue(WINDOW_POSITION_KEY, out object? value))
        {
            var position = value?.ToString()?.Split(',');
            if (
                position?.Length == 2
                && double.TryParse(position[0], out double x)
                && double.TryParse(position[1], out double y)
            )
            {
                return (x, y);
            }
        }
        return null;
    }
}
