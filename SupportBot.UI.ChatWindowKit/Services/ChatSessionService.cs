using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OpenAI.Chat;
using SupportBot.UI.ChatWindowKit.Models;

namespace SupportBot.UI.ChatWindowKit.Services;

/// <summary>
/// Defines the contract for a chat session service that manages a conversational session
/// with an AI assistant, including lifecycle control and message exchange.
/// </summary>
internal interface IChatSessionService : IDisposable
{
    /// <summary>
    /// Starts (or restarts) a chat session, clearing any existing state and messages.
    /// </summary>
    void StartSession();

    /// <summary>
    /// Ends the current chat session and releases any allocated resources/state.
    /// </summary>
    void EndSession();

    /// <summary>
    /// Adds a user message to the session and asynchronously processes an assistant response.
    /// </summary>
    /// <param name="content">The text content of the user message to send.</param>
    Task AddMessageAsync(string content);

    /// <summary>
    /// Occurs when a new assistant message has been received and processed.
    /// </summary>
    event MessageReceivedEventHandler? MessageReceived;
}

/// <summary>
/// Provides an implementation of <see cref="IChatSessionService"/> that manages a bounded
/// sequence of chat messages and interacts with an OpenAI <see cref="ChatClient"/> to obtain
/// assistant responses.
/// </summary>
/// <remarks>
/// Capacity: Retains only the most recent messages up to <see cref="MAX_MESSAGES"/> to
/// constrain memory usage. Oldest messages are discarded when the limit is exceeded.
/// Thread Safety: This implementation is not thread-safe; callers should ensure serialization
/// of access if used from multiple threads.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="ChatSessionService"/> class.
/// </remarks>
/// <param name="chatClient">The OpenAI chat client used to generate assistant responses.</param>
internal sealed partial class ChatSessionService(ChatClient chatClient) : IChatSessionService
{
    /// <summary>
    /// Maximum number of messages retained in the session history.
    /// </summary>
    private const int MAX_MESSAGES = 100;

    /// <summary>
    /// The underlying OpenAI chat client used to request completions.
    /// </summary>
    private readonly ChatClient _chatClient = chatClient;

    /// <summary>
    /// In-memory list of chat messages representing the conversation context passed to the model.
    /// </summary>
    private readonly List<ChatMessage> _chatMessages = [];

    /// <summary>
    /// Backing field for the <see cref="IChatSessionService.MessageReceived"/> event.
    /// </summary>
    private MessageReceivedEventHandler? _messageReceived;

    /// <summary>
    /// Event raised when the assistant produces a response message.
    /// </summary>
    event MessageReceivedEventHandler? IChatSessionService.MessageReceived
    {
        add => _messageReceived += value;
        remove => _messageReceived -= value;
    }

    /// <inheritdoc />
    void IChatSessionService.StartSession()
    {
        Cleanup();
    }

    /// <summary>
    /// Clears session state including messages and event subscriptions.
    /// </summary>
    private void Cleanup()
    {
        _chatMessages.Clear();
        _messageReceived = null; // Clear any existing event handlers
    }

    /// <inheritdoc />
    void IChatSessionService.EndSession()
    {
        Cleanup();
    }

    /// <inheritdoc />
    async Task IChatSessionService.AddMessageAsync(string content)
    {
        _chatMessages.Add(new UserChatMessage(content));

        if (_chatMessages.Count > MAX_MESSAGES)
        {
            _chatMessages.RemoveAt(0);
        }

        await ProcessAssistantResponseAsync();
    }

    /// <summary>
    /// Requests and processes the assistant response using the current message history.
    /// Adds the assistant message to the session if completion is successful, enforcing
    /// the message capacity constraint, and raises the <see cref="IChatSessionService.MessageReceived"/> event.
    /// </summary>
    /// <exception cref="NotImplementedException">
    /// Thrown for unsupported finish reasons (e.g., tool calls, length limits, content filters) or unhandled states.
    /// </exception>
    private async Task ProcessAssistantResponseAsync()
    {
        ChatCompletion completion = await _chatClient.CompleteChatAsync(_chatMessages);

        switch (completion.FinishReason)
        {
            case ChatFinishReason.Stop:
                {
                    _chatMessages.Add(new AssistantChatMessage(completion));

                    if (_chatMessages.Count > MAX_MESSAGES)
                    {
                        _chatMessages.RemoveAt(0);
                    }

                    string? content =
                        completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
                    if (!string.IsNullOrWhiteSpace(content))
                    {
                        _messageReceived?.Invoke(content);
                    }
                    break;
                }

            case ChatFinishReason.ToolCalls:
                throw new NotImplementedException(
                    "Tool calls are not yet implemented. Please implement the tool call handling logic."
                );

            case ChatFinishReason.Length:
                throw new NotImplementedException(
                    "Incomplete model output due to MaxTokens parameter or token limit exceeded."
                );

            case ChatFinishReason.ContentFilter:
                throw new NotImplementedException("Omitted content due to a content filter flag.");

            case ChatFinishReason.FunctionCall:
                throw new NotImplementedException("Deprecated in favor of tool calls.");

            default:
                throw new NotImplementedException(completion.FinishReason.ToString());
        }
    }

    /// <summary>
    /// Disposes the service, clearing internal state and detaching event handlers.
    /// </summary>
    public void Dispose()
    {
        Cleanup();
    }
}
