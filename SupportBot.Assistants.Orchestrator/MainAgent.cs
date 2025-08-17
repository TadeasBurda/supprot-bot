#pragma warning disable OPENAI001 // For evaluation purposes only

using System;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Assistants;
using SupportBot.Assistants.Orchestrator.Models;

namespace SupportBot.Assistants.Orchestrator;

/// <summary>
/// Contract for the main support agent orchestrator that manages lifecycle of an OpenAI Assistant,
/// processing of customer messages, and notification of newly available assistant messages.
/// </summary>
/// <remarks>
/// Usage flow:
/// 1. Call <see cref="InitializeAsync"/> to create / reset the assistant.
/// 2. Repeatedly call <see cref="HandleCustomerMessageAsync(string)"/> to send user input.
/// 3. Subscribe to <see cref="MessageReceived"/> to react to message updates.
/// 4. Call <see cref="Cleanup"/> or <see cref="IDisposable.Dispose"/> when finished.
/// Thread Safety: This abstraction is not guaranteed to be thread-safe; external synchronization may be required.
/// </remarks>
public interface IMainAgent : IDisposable
{
    /// <summary>
    /// Raised after a run completes and the full message collection (thread history) is retrieved.
    /// </summary>
    event MessageReceivedEventHandler? MessageReceived;

    /// <summary>
    /// Initializes or re-initializes the assistant and related state.
    /// </summary>
    /// <returns>Task representing the asynchronous initialization work.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Resets internal state, clears references, and detaches event subscribers.
    /// Safe to call multiple times.
    /// </summary>
    void Cleanup();

    /// <summary>
    /// Handles a new customer (user) message by ensuring an assistant + thread exist,
    /// appending the message, creating a run, polling its completion, and finally
    /// raising <see cref="MessageReceived"/> with updated thread messages.
    /// </summary>
    /// <param name="content">Plaintext user message content.</param>
    /// <exception cref="InvalidOperationException">Assistant or assistant client not initialized.</exception>
    /// <exception cref="ArgumentException">Content is null, empty, or whitespace.</exception>
    Task HandleCustomerMessageAsync(string content);
}

/// <summary>
/// Main implementation of <see cref="IMainAgent"/> that encapsulates interaction with the OpenAI Assistants API.
/// Responsible for:
/// - Creating and configuring an assistant.
/// - Managing a single conversation thread lifecycle.
/// - Sending user messages and triggering runs.
/// - Polling run status until terminal.
/// - Publishing full message history upon completion.
/// </summary>
/// <param name="openAIClient">The configured <see cref="OpenAIClient"/> used to obtain assistant clients.</param>
internal sealed partial class MainAgent(OpenAIClient openAIClient) : IMainAgent
{
    /// <summary>
    /// Root OpenAI client used to obtain an <see cref="AssistantClient"/>.
    /// </summary>
    private readonly OpenAIClient _openAIClient = openAIClient;

    /// <summary>
    /// Active assistant instance (null until initialized).
    /// </summary>
    private Assistant? _assistant;

    /// <summary>
    /// Backing store for the <see cref="IMainAgent.MessageReceived"/> event.
    /// </summary>
    private MessageReceivedEventHandler? _messageReceived;

    /// <summary>
    /// Identifier of the current assistant thread (null until first message).
    /// </summary>
    private string? _threadId;

    /// <summary>
    /// Assistant client used for all assistant operations.
    /// </summary>
    private AssistantClient? _assistantClient;

    /// <inheritdoc />
    event MessageReceivedEventHandler? IMainAgent.MessageReceived
    {
        add => _messageReceived += value;
        remove => _messageReceived -= value;
    }

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        Cleanup();
        await InitializeAssistantAsync();
    }

    /// <inheritdoc />
    public void Cleanup()
    {
        _assistant = null;
        _messageReceived = null;
        _threadId = null;
        _assistantClient = null;
    }

    /// <inheritdoc />
    public async Task HandleCustomerMessageAsync(string content)
    {
        if (_assistant == null)
        {
            throw new InvalidOperationException(
                "Assistant is not initialized. Call InitializeAsync first."
            );
        }
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or empty.", nameof(content));
        }
        if (_assistantClient == null)
        {
            throw new InvalidOperationException(
                "Assistant client is not initialized. Call InitializeAsync first."
            );
        }

        var assistantId = _assistant.Id;

        string? runId;
        if (string.IsNullOrEmpty(_threadId))
        {
            (_threadId, runId) = await StartNewThreadRunAsync(
                _assistantClient,
                content,
                assistantId
            );
        }
        else
        {
            runId = await ContinueThreadRunAsync(_assistantClient, content, assistantId);
        }

        if (string.IsNullOrEmpty(runId))
        {
            throw new InvalidOperationException("Failed to create or retrieve run ID.");
        }

        await PollRunStatusAsync(_assistantClient, _threadId, runId);
        NotifyMessageReceived(_assistantClient);
    }

    /// <summary>
    /// Creates a new assistant instance with predefined instructions and configures the internal assistant client.
    /// </summary>
    /// <returns>Task representing the asynchronous creation operation.</returns>
    private async Task InitializeAssistantAsync()
    {
        var assistantClient = _openAIClient.GetAssistantClient();
        var assistantOptions = new AssistantCreationOptions()
        {
            Name = "MainSupportAgent",
            Instructions =
                "You are a support assistant. Your job is to understand customer issues and delegate them to the correct specialized agent.",
        };
        _assistant = await assistantClient.CreateAssistantAsync("gpt-4o", assistantOptions);
        _assistantClient = _openAIClient.GetAssistantClient();
    }

    /// <summary>
    /// Continues an existing thread by appending a new user message and creating a new run.
    /// </summary>
    /// <param name="assistantClient">Assistant client to use for API calls.</param>
    /// <param name="content">User message content.</param>
    /// <param name="assistantId">Identifier of the assistant.</param>
    /// <returns>The newly created run identifier.</returns>
    private async Task<string> ContinueThreadRunAsync(
        AssistantClient assistantClient,
        string content,
        string assistantId
    )
    {
        _ = await assistantClient.CreateMessageAsync(_threadId, MessageRole.User, [content]);
        var run = await assistantClient.CreateRunAsync(_threadId, assistantId);
        return run.Value.Id;
    }

    /// <summary>
    /// Starts a brand new thread with an initial user message and immediately creates a run.
    /// </summary>
    /// <param name="assistantClient">Assistant client to use for API calls.</param>
    /// <param name="content">Initial user message content.</param>
    /// <param name="assistantId">Identifier of the assistant.</param>
    /// <returns>Tuple containing the new thread identifier and the run identifier.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the thread + run creation fails.</exception>
    private static async Task<(string threadId, string runId)> StartNewThreadRunAsync(
        AssistantClient assistantClient,
        string content,
        string assistantId
    )
    {
        var threadOptions = new ThreadCreationOptions()
        {
            InitialMessages = { new ThreadInitializationMessage(MessageRole.User, [content]) },
        };
        var threadRun =
            await assistantClient.CreateThreadAndRunAsync(assistantId, threadOptions)
            ?? throw new InvalidOperationException("Failed to create thread run.");
        return (threadRun.Value.ThreadId, threadRun.Value.Id);
    }

    /// <summary>
    /// Retrieves the full ordered message history for the current thread and raises the
    /// <see cref="IMainAgent.MessageReceived"/> event.
    /// </summary>
    /// <param name="assistantClient">Assistant client used to fetch messages.</param>
    private void NotifyMessageReceived(AssistantClient assistantClient)
    {
        // Retrieve full message history (ascending for oldest -> newest)
        var options = new MessageCollectionOptions() { Order = MessageCollectionOrder.Ascending };
        var messages = assistantClient.GetMessages(_threadId, options);

        _messageReceived?.Invoke(messages);
    }

    /// <summary>
    /// Polls an assistant run until its status becomes terminal (completed, failed, cancelled, etc.).
    /// </summary>
    /// <param name="assistantClient">Assistant client for status checks.</param>
    /// <param name="threadId">Identifier of the thread containing the run.</param>
    /// <param name="runId">Identifier of the run to poll.</param>
    private static async Task PollRunStatusAsync(
        AssistantClient assistantClient,
        string threadId,
        string runId
    )
    {
        ThreadRun? runStatus;
        do
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250));
            runStatus = (await assistantClient.GetRunAsync(threadId, runId)).Value;
        } while (runStatus is { Status.IsTerminal: false });
    }

    /// <summary>
    /// Releases resources and clears state by delegating to <see cref="Cleanup"/>.
    /// </summary>
    public void Dispose()
    {
        Cleanup();
    }
}
