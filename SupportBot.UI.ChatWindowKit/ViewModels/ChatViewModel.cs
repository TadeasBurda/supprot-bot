using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SupportBot.Helpers.CommunityToolkit.ViewModels;
using SupportBot.UI.ChatWindowKit.Models;
using SupportBot.UI.ChatWindowKit.Services;

namespace SupportBot.UI.ChatWindowKit.ViewModels;

/// <summary>
/// View model for the chat window. Manages the collection of chat messages, relays user input to the chat session service,
/// receives assistant responses, and exposes an observable collection for UI binding.
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Holds user input text (two-way bound to a TextBox).
/// - Maintains a bounded in-memory message history (<see cref="MAX_MESSAGES"/> limit).
/// - Coordinates with <see cref="IChatSessionService"/> for assistant responses.
/// - Raises <see cref="MessageReceived"/> after any message (user or assistant) is appended.
/// Thread Safety: This type is intended to be accessed on the UI thread only.
/// Disposal: Ensures event handlers are detached and state cleared during cleanup / disposal.
/// </remarks>
/// <param name="chatSession">Chat session abstraction used to interact with the AI assistant.</param>
internal sealed partial class ChatViewModel(IChatSessionService chatSession) : BaseViewModel
{
    /// <summary>
    /// Maximum number of messages retained in the <see cref="Messages"/> collection before the oldest is evicted.
    /// </summary>
    private const int MAX_MESSAGES = 100;

    /// <summary>
    /// Underlying chat session service used to send user content and receive assistant responses.
    /// </summary>
    private readonly IChatSessionService _chatSession = chatSession;

    /// <summary>
    /// Backing field for the <see cref="MessageReceived"/> event.
    /// </summary>
    private MessageReceivedEventHandler? _messageReceived;

    /// <summary>
    /// Occurs after a message (user or assistant) is added to the <see cref="Messages"/> collection.
    /// Subscribers can use this to react (e.g., focus input, auto-scroll).
    /// </summary>
    internal event MessageReceivedEventHandler? MessageReceived
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
    /// Performs validation, appends the user message, clears the input field, and triggers downstream session logic.
    /// </summary>
    /// <remarks>
    /// This method only queues the user message locally; assistant responses are populated asynchronously through
    /// the session service via <see cref="OnMessageReceived(string)"/> callback.
    /// </remarks>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput))
            return;

        AddMessage(content: UserInput, role: Role.User);
        await _chatSession.AddMessageAsync(content: UserInput);

        UserInput = string.Empty;
    }

    /// <summary>
    /// Cleans up resources used by the chat view model, including clearing the message collection,
    /// detaching subscribers, and resetting transient state.
    /// </summary>
    public override void Cleanup()
    {
        Messages.Clear();
        _messageReceived = null;
        UserInput = string.Empty;
    }

    /// <summary>
    /// Initializes the chat view model for a fresh chat session.
    /// Clears existing state, resets disposal flag, starts a new session, and subscribes to session events.
    /// </summary>
    public override void Initialize()
    {
        Cleanup();
        _disposed = false;
        _chatSession.StartSession();
        _chatSession.MessageReceived += OnMessageReceived;
    }

    /// <summary>
    /// Handles assistant message notifications from the underlying chat session service.
    /// Adds assistant messages to the UI message collection.
    /// </summary>
    /// <param name="content">The textual content of the assistant response.</param>
    private void OnMessageReceived(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return;

        AddMessage(content: content, role: Role.Assistant);
    }

    /// <summary>
    /// Releases resources held by the view model.
    /// Ensures managed cleanup when <paramref name="disposing"/> is <c>true</c>.
    /// </summary>
    /// <param name="disposing">True when invoked from <see cref="IDisposable.Dispose"/>, false during finalization.</param>
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
    /// Adds a new message (user or assistant) to the <see cref="Messages"/> collection.
    /// Enforces the maximum capacity and raises <see cref="MessageReceived"/> for observers.
    /// </summary>
    /// <param name="content">The textual content of the message.</param>
    /// <param name="role">The semantic role of the message (user or assistant).</param>
    private void AddMessage(string content, Role role)
    {
        var message = new Message
        {
            MsgText = content,
            MsgAlignment = role == Role.User ? HorizontalAlignment.Right : HorizontalAlignment.Left,
            MsgDateTime = DateTime.Now.ToString("hh:mm tt"),
        };
        Messages.Add(message);

        if (Messages.Count > MAX_MESSAGES)
        {
            Messages.RemoveAt(0);
        }

        _messageReceived?.Invoke(content: content); // Notify subscribers that a message has been received
    }

    /// <summary>
    /// Enumerates the internal roles used to distinguish user-authored messages from assistant responses.
    /// </summary>
    private enum Role
    {
        /// <summary>
        /// Message authored by the local user.
        /// </summary>
        User,

        /// <summary>
        /// Message generated by the assistant/model.
        /// </summary>
        Assistant,
    }
}
