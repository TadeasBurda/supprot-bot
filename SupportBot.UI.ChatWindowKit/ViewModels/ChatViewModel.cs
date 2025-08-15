using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SupportBot.Helpers.CommunityToolkit.ViewModels;
using SupportBot.UI.ChatWindowKit.Models;

namespace SupportBot.UI.ChatWindowKit.ViewModels;

/// <summary>
/// Delegate used to notify listeners when a chat message has been processed (sent and inserted into the collection).
/// </summary>
internal delegate void MessageReceivedEventHandler();

/// <summary>
/// View model for the chat window. Manages the collection of chat messages and handles initialization and cleanup logic.
/// </summary>
internal sealed partial class ChatViewModel : BaseViewModel
{
    /// <summary>
    /// Maximum number of messages retained in the <see cref="Messages"/> collection before the oldest is evicted.
    /// </summary>
    private const int MAX_MESSAGES = 100;

    /// <summary>
    /// Backing field for the <see cref="MessageReceived"/> event.
    /// </summary>
    private MessageReceivedEventHandler? _messageReceived;

    /// <summary>
    /// Occurs after a message is sent and added to the collection.
    /// Subscribers can use this to react (e.g., move focus, scroll, etc.).
    /// </summary>
    internal event MessageReceivedEventHandler MessageReceived
    {
        add => _messageReceived += value;
        remove => _messageReceived -= value;
    }

    /// <summary>
    /// Gets or sets the current user input text bound to the chat input box.
    /// Cleared automatically after a successful send operation.
    /// </summary>
    [ObservableProperty]
    internal partial string UserInput { get; set; } = string.Empty;

    /// <summary>
    /// Gets the collection of chat messages displayed in the chat window.
    /// Maintains a bounded size enforced by <see cref="MAX_MESSAGES"/>.
    /// </summary>
    internal ObservableCollection<Message> Messages { get; } = [];

    /// <summary>
    /// Command handler invoked to send the current user input as a chat message.
    /// Performs validation, creates a <see cref="Message"/>, inserts it, enforces capacity,
    /// clears the input, and raises <see cref="MessageReceived"/>.
    /// </summary>
    [RelayCommand]
    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(UserInput))
        {
            return; // Do not send empty messages
        }

        var message = new Message
        {
            MsgText = UserInput,
            MsgAlignment = HorizontalAlignment.Right,
            MsgDateTime = DateTime.Now.ToString("hh:mm tt"),
        };
        AddMessage(message);

        UserInput = string.Empty; // Clear the input after sending the message

        _messageReceived?.Invoke(); // Notify subscribers that a message has been sent
    }

    /// <summary>
    /// Cleans up resources used by the chat view model, including clearing the message collection.
    /// </summary>
    public override void Cleanup()
    {
        Messages.Clear();
    }

    /// <summary>
    /// Initializes the chat view model by clearing existing messages.
    /// </summary>
    public override void Initialize()
    {
        Messages.Clear();
    }

    /// <summary>
    /// Releases the unmanaged resources used by the chat view model and optionally releases the managed resources.
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
            Cleanup();
        }

        // Dispose unmanaged resources here if any

        _disposed = true;

        base.Dispose(disposing);
    }

    /// <summary>
    /// Adds a new message to the chat message collection.
    /// When the collection exceeds <see cref="MAX_MESSAGES"/>, the oldest message is removed.
    /// </summary>
    /// <param name="message">The <see cref="Message"/> instance to add to the collection.</param>
    private void AddMessage(Message message)
    {
        Messages.Add(message);

        if (Messages.Count > MAX_MESSAGES)
        {
            Messages.RemoveAt(0); // Remove the oldest message if the limit is exceeded
        }
    }
}
