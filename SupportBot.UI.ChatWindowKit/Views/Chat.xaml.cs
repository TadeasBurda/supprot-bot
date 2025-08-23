using System.Threading;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SupportBot.UI.ChatWindowKit.ViewModels;

namespace SupportBot.UI.ChatWindowKit.Views;

/// <summary>
/// Represents the chat user control for the application.
/// Handles initialization, cleanup, and disposal of the associated <see cref="ChatViewModel"/>.
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Resolve and initialize the <see cref="ViewModel"/> when the control is loaded.
/// - Provide the current dispatcher and a fresh <see cref="CancellationToken"/> to the view model.
/// - Cancel and dispose transient resources when the control is unloaded.
/// UI behavior:
/// - Automatically focuses the user input text box on load and after each message is processed.
/// </remarks>
/// <seealso cref="UserControl"/>
public sealed partial class Chat : UserControl
{
    /// <summary>
    /// Backing <see cref="CancellationTokenSource"/> used to propagate cancellation to background operations
    /// started while the control is loaded.
    /// </summary>
    /// <remarks>
    /// The source is recreated on each load, and is cancelled and disposed during unload via
    /// <see cref="CleanupCancellationTokenSource"/> to ensure deterministic shutdown of in-flight work.
    /// </remarks>
    private CancellationTokenSource? _cancellationTokenSource;

    /// <summary>
    /// Gets the view model associated with this chat control.
    /// </summary>
    internal ChatViewModel ViewModel { get; } = DependencyConfiguration.GetService<ChatViewModel>();

    /// <summary>
    /// Initializes a new instance of the <see cref="Chat"/> class.
    /// Hooks into the Loaded and Unloaded events to manage the lifetime of the view model.
    /// </summary>
    public Chat()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>
    /// Handles the Loaded event for the control.
    /// Initializes the view model, wires events, assigns the dispatcher queue, and sets focus to the user input box.
    /// </summary>
    /// <param name="_">The sender of the event (not used).</param>
    /// <param name="__">The event arguments (not used).</param>
    private void OnLoaded(object _, RoutedEventArgs __)
    {
        UserInputTextBox.Focus(FocusState.Programmatic);

        CleanupCancellationTokenSource();
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        ViewModel.Initialize(DispatcherQueue, cancellationToken);
        ViewModel.MessageReceived += OnMessageReceived;
    }

    /// <summary>
    /// Handles the <see cref="ChatViewModel.MessageReceived"/> callback.
    /// Restores focus to the user input text box after a message is processed.
    /// </summary>
    private void OnMessageReceived()
    {
        UserInputTextBox.Focus(FocusState.Programmatic);
    }

    /// <summary>
    /// Handles the Unloaded event for the control.
    /// Performs cleanup, disposes the view model, detaches events, and clears dispatcher reference.
    /// </summary>
    /// <param name="_">The sender of the event (not used).</param>
    /// <param name="__">The event arguments (not used).</param>
    private void OnUnloaded(object _, RoutedEventArgs __)
    {
        ViewModel.Cleanup();
        CleanupCancellationTokenSource();
        ViewModel.MessageReceived -= OnMessageReceived;
    }

    /// <summary>
    /// Handles key down events in the user input text box.
    /// When the Enter key is pressed and the send command can execute, triggers message sending.
    /// </summary>
    /// <param name="_">The sender of the event (not used).</param>
    /// <param name="e">The key event arguments containing the pressed key.</param>
    /// <remarks>
    /// No newline insertion logic is provided; pressing Enter attempts to send the message.
    /// </remarks>
    private void TextBox_KeyDown(object _, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.Enter)
            return;

        if (!ViewModel.SendMessageCommand.CanExecute(null))
            return;

        ViewModel.SendMessageCommand.Execute(null);
    }

    /// <summary>
    /// Cancels and disposes the existing <see cref="CancellationTokenSource"/>, if any,
    /// and resets the reference to <c>null</c>. Safe to call multiple times.
    /// </summary>
    private void CleanupCancellationTokenSource()
    {
        _cancellationTokenSource?.Cancel();
        _cancellationTokenSource?.Dispose();
        _cancellationTokenSource = null;
    }
}
