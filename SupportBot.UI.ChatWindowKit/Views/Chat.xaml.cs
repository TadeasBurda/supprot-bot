using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SupportBot.UI.ChatWindowKit.ViewModels;

namespace SupportBot.UI.ChatWindowKit.Views;

/// <summary>
/// Represents the chat user control for the application.
/// Handles initialization, cleanup, and disposal of the associated <see cref="ChatViewModel"/>.
/// </summary>
public sealed partial class Chat : UserControl
{
    /// <summary>
    /// Gets the view model associated with this chat control.
    /// </summary>
    internal ChatViewModel ViewModel { get; } = DependencyConfiguration.GetService<ChatViewModel>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Chat"/> class.
    /// </summary>
    public Chat()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Handles the Loaded event for the control.
    /// Initializes the view model and assigns the dispatcher queue.
    /// </summary>
    /// <param name="_">The sender of the event (not used).</param>
    /// <param name="__">The event arguments (not used).</param>
    private void OnLoaded(object _, RoutedEventArgs __)
    {
        ViewModel.DispatcherQueue = DispatcherQueue;
        ViewModel.Initialize();
    }

    /// <summary>
    /// Handles the Unloaded event for the control.
    /// Cleans up and disposes the view model, and clears the dispatcher queue reference.
    /// </summary>
    /// <param name="_">The sender of the event (not used).</param>
    /// <param name="__">The event arguments (not used).</param>
    private void OnUnloaded(object _, RoutedEventArgs __)
    {
        ViewModel.Cleanup();
        ViewModel.Dispose();
        ViewModel.DispatcherQueue = null;
    }
}
