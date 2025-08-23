using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SupportBot.App.ViewModels;

namespace SupportBot.App.Views;

/// <summary>
/// Represents the main page of the application.
/// </summary>
/// <remarks>
/// This page is responsible for:
///  - Resolving its <see cref="ViewModel"/> from the application DI container.
///  - Initializing the view model with the current <see cref="Microsoft.UI.Dispatching.DispatcherQueue"/> and a fresh <see cref="CancellationToken"/> when the page is loaded.
///  - Cleaning up resources and cancelling background work when the page is navigated away from.
/// The page ensures deterministic disposal of transient resources created during its active lifetime.
/// </remarks>
/// <seealso cref="Page"/>
public sealed partial class MainPage : Page
{
    /// <summary>
    /// Backing <see cref="CancellationTokenSource"/> that governs asynchronous operations started by the page
    /// and provided to the view model during <see cref="OnLoaded(object, RoutedEventArgs)"/>.
    /// </summary>
    /// <remarks>
    /// The source is cancelled and disposed when navigating away to prevent resource leaks and to stop in-flight work.
    /// </remarks>
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Gets the main view model for this page.
    /// </summary>
    /// <remarks>
    /// The instance is resolved from the application's dependency injection container and owned by the page.
    /// Consumers should not dispose this instance directly; its lifecycle is managed by the page and the DI container.
    /// </remarks>
    internal MainViewModel ViewModel { get; } =
        App.Current.Services.GetRequiredService<MainViewModel>();

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Handles the <see cref="FrameworkElement.Loaded"/> event for the page by creating a fresh
    /// <see cref="CancellationTokenSource"/> and initializing the <see cref="ViewModel"/> with the current dispatcher.
    /// </summary>
    /// <param name="_">Unused event sender.</param>
    /// <param name="__">Unused routed event arguments.</param>
    private void OnLoaded(object _, RoutedEventArgs __)
    {
        CleanupCancellationTokenSource();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        ViewModel.Initialize(DispatcherQueue, cancellationToken);
    }

    /// <summary>
    /// Called when the page is navigated from.
    /// </summary>
    /// <param name="e">Navigation event data.</param>
    /// <remarks>
    /// Ensures the view model is cleaned up before cancelling tokens and disposing local resources,
    /// then delegates to the base implementation.
    /// </remarks>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        ViewModel.Cleanup();
        CleanupCancellationTokenSource();
        base.OnNavigatedFrom(e);
    }

    /// <summary>
    /// Cancels and disposes the existing <see cref="CancellationTokenSource"/>, if any,
    /// and resets the reference to <c>null</c>.
    /// </summary>
    /// <remarks>
    /// This method is idempotent and safe to call multiple times.
    /// If the token source is active, it will be cancelled before being disposed to signal cooperative shutdown.
    /// </remarks>
    private void CleanupCancellationTokenSource()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
