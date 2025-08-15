using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using SupportBot.Helpers.Window.LocalSettings;

namespace SupportBot.App;

/// <summary>
/// Represents the main window of the SupportBot application.
/// This window initializes its position from persisted settings, restores to last saved location,
/// and starts maximized (if the presenter supports it).
/// </summary>
/// <remarks>
/// Lifecycle:
/// 1. Constructor initializes components.
/// 2. Attempts to restore prior window position from local settings.
/// 3. Maximizes the window (if supported by the current presenter).
/// 4. Persists the window position on close.
/// Persistence Scope: Uses <see cref="Windows.Storage.ApplicationData.LocalSettings"/> for per-user, per-machine storage.
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow()
    {
        InitializeComponent();
        SetWindowPosition();
        MaximizeWindow();

        Closed += OnClosed;
    }

    /// <summary>
    /// Handles the window Closed event and persists the last known window position.
    /// </summary>
    /// <param name="sender">The source of the event (the window instance).</param>
    /// <param name="args">Event data associated with the window closing.</param>
    /// <remarks>
    /// Only the top-left position is saved; size/state is not persisted because the window is forced to maximize on startup.
    /// </remarks>
    private void OnClosed(object sender, WindowEventArgs args)
    {
        SaveWindowPosition();
    }

    /// <summary>
    /// Maximizes the window if the current presenter supports overlapped window behavior.
    /// </summary>
    /// <remarks>
    /// Safe no-op if the presenter is not an <see cref="OverlappedPresenter"/>.
    /// </remarks>
    private void MaximizeWindow()
    {
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Maximize();
        }
    }

    /// <summary>
    /// Saves the current window's top-left screen position into local settings for future restoration.
    /// </summary>
    /// <remarks>
    /// The position reflects the window location prior to maximizing if the platform reports it accordingly.
    /// </remarks>
    private void SaveWindowPosition()
    {
        WindowLocalSettings.SaveWindowPosition(x: AppWindow.Position.X, y: AppWindow.Position.Y);
    }

    /// <summary>
    /// Attempts to restore a previously saved window position from local settings.
    /// </summary>
    /// <remarks>
    /// Falls back silently if no stored position exists or the stored data is invalid.
    /// The window is moved before being maximized to ensure a consistent restore experience if future logic changes.
    /// </remarks>
    private void SetWindowPosition()
    {
        if (WindowLocalSettings.GetWindowPosition() is { } position)
        {
            AppWindow.Move(new Windows.Graphics.PointInt32((int)position.x, (int)position.y));
        }
    }
}
