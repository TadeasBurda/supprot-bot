using System;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;

namespace SupportBot.Helpers.CommunityToolkit.ViewModels;

/// <summary>
/// Provides a base class for all view models in the application.
/// Implements the <see cref="IDisposable"/> interface for resource management.
/// </summary>
/// <remarks>
/// <para>
/// This base type standardizes lifecycle management for view models, including initialization,
/// cleanup, and deterministic disposal of managed resources. It also exposes a shared
/// <see cref="ILogger"/> instance for structured diagnostics and an optional
/// <see cref="DispatcherQueue"/> for marshaling property updates or callbacks onto the UI thread.
/// </para>
/// <para>
/// Threading: if the view model is bound to UI, use <see cref="Initialize(DispatcherQueue, CancellationToken)"/>
/// to supply a valid dispatcher. Consumers are responsible for invoking <see cref="Cleanup"/> and/or
/// <see cref="Dispose()"/> as appropriate when the view model goes out of scope.
/// </para>
/// </remarks>
/// <param name="logger">
/// Logger injected from the hosting container and available to derived classes for emitting diagnostic,
/// performance, and error information.
/// </param>
/// <seealso cref="ObservableObject"/>
/// <seealso cref="IDisposable"/>
public abstract class BaseViewModel(ILogger logger) : ObservableObject, IDisposable
{
    /// <summary>
    /// Logger available to derived classes for emitting diagnostic, performance, and error information.
    /// </summary>
    /// <remarks>Prefer structured logging (e.g. <c>_logger.LogInformation("Loaded {Count} items", count);</c>).</remarks>
    protected readonly ILogger _logger = logger;

    /// <summary>
    /// Cancellation token provided at initialization time to propagate cancellation to background operations started by the view model.
    /// </summary>
    protected CancellationToken _cancellationToken = default;

    /// <summary>
    /// Indicates whether the instance has already been disposed (guards against double disposal).
    /// </summary>
    protected bool _disposed;

    /// <summary>
    /// Dispatcher queue used to marshal callbacks or property changes onto the UI thread. May be null when not UI-bound.
    /// </summary>
    protected DispatcherQueue? _dispatcherQueue;

    /// <summary>
    /// Performs initialization logic (idempotent): first calls <see cref="Cleanup"/> to reset state, then stores the dispatcher and cancellation token.
    /// </summary>
    /// <param name="dispatcherQueue">Dispatcher queue for UI-thread marshaling (must not be null for UI updates).</param>
    /// <param name="cancellationToken">Optional token used to cancel background work started during this lifecycle.</param>
    /// <remarks>Override to add additional setup logic (subscribe events, start tasks, etc.).</remarks>
    public virtual void Initialize(
        DispatcherQueue dispatcherQueue,
        CancellationToken cancellationToken = default
    )
    {
        Cleanup();
        _dispatcherQueue = dispatcherQueue;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Reverts initialization effects: clears dispatcher reference and resets the cancellation token.
    /// </summary>
    /// <remarks>Must be safe to call multiple times.</remarks>
    public virtual void Cleanup()
    {
        _dispatcherQueue = null;
        _cancellationToken = default;
    }

    /// <summary>
    /// Core dispose routine implementing the dispose pattern.
    /// </summary>
    /// <param name="disposing">
    /// True to release managed resources (explicit disposal), false if invoked by the finalizer (managed objects may not be referenced).
    /// </param>
    /// <remarks>Calls <see cref="Cleanup"/> when <paramref name="disposing"/> is true.</remarks>
    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            // Release managed resources (unsubscribe events, dispose IDisposable fields, cancel tokens, etc.)
            Cleanup();
        }

        // Release unmanaged resources here if introduced.

        _disposed = true;
    }

    /// <summary>
    /// Performs deterministic disposal of this instance and suppresses finalization.
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
