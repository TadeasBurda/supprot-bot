using SupportBot.Helpers.CommunityToolkit.ViewModels;
using System;

namespace SupportBot.App.ViewModels;

/// <summary>
/// Represents the main view model for the application.
/// Inherits from <see cref="BaseViewModel"/> and provides initialization, cleanup, and disposal logic.
/// </summary>
internal sealed partial class MainViewModel : BaseViewModel
{
    /// <summary>
    /// Cleans up resources used by the main view model.
    /// </summary>
    public override void Cleanup()
    {
        // Not implemented yet
    }

    /// <summary>
    /// Initializes the main view model.
    /// </summary>
    public override void Initialize()
    {
        // Not implemented yet
    }

    /// <summary>
    /// Releases the unmanaged resources used by the main view model and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected override void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Dispose managed resources here if any
        }

        // Dispose unmanaged resources here if any

        _disposed = true;

        base.Dispose(disposing);
    }
}
