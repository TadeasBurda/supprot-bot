using Microsoft.UI.Dispatching;
using System;

namespace SupportBot.App.ViewModels;

/// <summary>
/// Provides a base class for all view models in the application.
/// Implements the <see cref="IDisposable"/> interface for resource management.
/// </summary>
internal abstract class BaseViewModel : IDisposable
{
    /// <summary>
    /// Indicates whether the object has been disposed.
    /// </summary>
    protected bool _disposed;

    /// <summary>
    /// Gets or sets the dispatcher queue associated with the view model.
    /// Used to marshal calls to the UI thread.
    /// </summary>
    internal DispatcherQueue? DispatcherQueue { get; set; }

    /// <summary>
    /// Initializes the view model.
    /// </summary>
    internal abstract void Initialize();

    /// <summary>
    /// Cleans up resources used by the view model.
    /// </summary>
    internal abstract void Cleanup();

    /// <summary>
    /// Releases the unmanaged resources used by the view model and optionally releases the managed resources.
    /// </summary>
    /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Free managed resources here
        }

        // Free unmanaged resources here
        _disposed = true;
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Finalizes the instance of the <see cref="BaseViewModel"/> class.
    /// </summary>
    ~BaseViewModel()
    {
        Dispose(false);
    }
}
