#pragma warning disable OPENAI001 // For evaluation purposes only

using System;
using System.ClientModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using OpenAI.Assistants;
using SupportBot.Assistants.Orchestrator;
using SupportBot.Helpers.CommunityToolkit.ViewModels;
using SupportBot.UI.ChatWindowKit.Models;

namespace SupportBot.UI.ChatWindowKit.ViewModels;

/// <summary>
/// View model for the chat window. Manages the collection of chat messages, relays user input to the chat session service,
/// receives assistant responses, and exposes an observable collection for UI binding.
/// </summary>
/// <remarks>
/// Responsibilities:
/// - Holds user input text (two-way bound to a TextBox).
/// - Coordinates with <see cref="IOrchestrator"/> for assistant responses.
/// - Raises <see cref="MessageReceived"/> after any message (user or assistant) is appended.
/// Thread Safety: This type is intended to be accessed on the UI thread only.
/// Disposal: Ensures event handlers are detached and state cleared during cleanup / disposal.
/// </remarks>
/// <param name="mainAgent">Chat session abstraction used to interact with the AI assistant.</param>
internal sealed partial class ChatViewModel(ILogger<ChatViewModel> logger, IOrchestrator mainAgent)
    : BaseViewModel(logger)
{
    /// <summary>
    /// Reference to the main agent responsible for orchestrating assistant communication.
    /// </summary>
    private readonly IOrchestrator _mainAgent = mainAgent;

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
    /// </summary>
    internal ObservableCollection<Message> Messages { get; } = [];

    /// <summary>
    /// Command handler invoked to send the current user input as a chat message.
    /// Performs validation, appends the user message, clears the input field, and triggers downstream session logic.
    /// </summary>
    /// <remarks>
    /// This method only queues the user message locally; assistant responses are populated asynchronously through
    /// the session service via <see cref="OnMainAgentMessageReceived(CollectionResult{ThreadMessage})"/> callback.
    /// </remarks>
    [RelayCommand]
    private async Task SendMessageAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput))
            return;

        await _mainAgent.HandleCustomerMessageAsync(content: UserInput);

        UserInput = string.Empty;
    }

    /// <summary>
    /// Cleans up resources used by the chat view model, including clearing the message collection,
    /// detaching subscribers, and resetting transient state.
    /// </summary>
    public override void Cleanup()
    {
        UserInput = string.Empty;

        Messages.Clear();

        _messageReceived = null;

        _mainAgent.MessageReceived -= OnMainAgentMessageReceived;
        _mainAgent.Cleanup();

        base.Cleanup();
    }

    public override void Initialize(
        DispatcherQueue dispatcherQueue,
        CancellationToken cancellationToken = default
    )
    {
        Cleanup();
        base.Initialize(dispatcherQueue, cancellationToken);
        _ = Task.Run(
            async () =>
            {
                await _mainAgent.InitializeAsync();
                _mainAgent.MessageReceived += OnMainAgentMessageReceived;
            },
            _cancellationToken
        );
    }

    /// <summary>
    /// Handles messages received from the <see cref="IOrchestrator"/> by translating them into
    /// UI <see cref="Message"/> instances and appending them to the <see cref="Messages"/> collection.
    /// Also surfaces any file annotations as separate informational messages.
    /// </summary>
    /// <param name="messages">The collection of thread messages returned by the assistant.</param>
    private void OnMainAgentMessageReceived(CollectionResult<ThreadMessage> messages)
    {
        _ = _dispatcherQueue?.TryEnqueue(Messages.Clear);

        foreach (ThreadMessage message in messages)
        {
            var role = message.Role == MessageRole.User ? Role.User : Role.Assistant;
            foreach (
                var contentItem in message.Content.Where(contentItem =>
                    !string.IsNullOrEmpty(contentItem.Text)
                )
            )
            {
                AddMessage(contentItem.Text, role);
                foreach (TextAnnotation annotation in contentItem.TextAnnotations)
                {
                    if (!string.IsNullOrEmpty(annotation.InputFileId))
                    {
                        AddMessage($"* File citation, file ID: {annotation.InputFileId}", role);
                    }
                    if (!string.IsNullOrEmpty(annotation.OutputFileId))
                    {
                        AddMessage($"* File output, new file ID: {annotation.OutputFileId}", role);
                    }
                }
            }
        }

        _messageReceived?.Invoke();
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
        _ = _dispatcherQueue?.TryEnqueue(() => Messages.Add(message));
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
