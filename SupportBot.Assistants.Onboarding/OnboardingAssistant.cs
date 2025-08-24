#pragma warning disable OPENAI001 // For evaluation purposes only

using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Assistants;

namespace SupportBot.Assistants.Onboarding;

/// <summary>
/// Contract for the main support agent orchestrator that manages lifecycle of an OpenAI Assistant,
/// processing of customer messages, and optionally notification of newly available assistant messages.
/// </summary>
/// <remarks>
/// Usage flow:
/// 1. Call <see cref="InitializeAsync"/> to create / reset the assistant.
/// 2. Repeatedly call <see cref="HandleCustomerMessageAsync(string)"/> to send user input.
/// 3. If applicable, react to message updates according to your UI or state management needs.
/// 4. Call <see cref="Cleanup"/> or <see cref="IDisposable.Dispose"/> when finished.
/// Thread Safety: This abstraction is not guaranteed to be thread-safe; external synchronization may be required.
/// </remarks>
public interface IOnboardingAssistant : IDisposable
{
    /// <summary>
    /// Initializes or re-initializes the assistant and related state (e.g., creating the assistant client).
    /// Safe to call multiple times; previous state will be cleared via <see cref="Cleanup"/>.
    /// </summary>
    /// <returns>Task representing the asynchronous initialization work.</returns>
    Task InitializeAsync();

    /// <summary>
    /// Resets internal state, clears references, and detaches event subscribers if any.
    /// Safe to call multiple times and idempotent.
    /// </summary>
    void Cleanup();

    /// <summary>
    /// Handles a new customer (user) message by ensuring an assistant and thread exist,
    /// appending the message, creating a run, polling its completion, and returning
    /// the latest assistant message text for the active thread.
    /// </summary>
    /// <param name="content">Plaintext user message content.</param>
    /// <returns>The latest assistant-authored message content after the run completes; empty string if none.</returns>
    /// <exception cref="InvalidOperationException">Assistant or assistant client not initialized.</exception>
    /// <exception cref="ArgumentException">Content is null, empty, or whitespace.</exception>
    Task<string> HandleCustomerMessageAsync(string content);
}

/// <summary>
/// Main implementation of <see cref="IOnboardingAssistant"/> that encapsulates interaction with the OpenAI Assistants API.
/// Responsible for:
/// - Creating and configuring an assistant.
/// - Managing a single conversation thread lifecycle.
/// - Sending user messages and triggering runs.
/// - Polling run status until terminal.
/// - Publishing or returning the latest message content upon completion.
/// </summary>
/// <param name="openAIClient">The configured <see cref="OpenAIClient"/> used to obtain assistant clients.</param>
/// <param name="assistantId">The unique identifier of an existing Assistant to use for this orchestrator.</param>
internal sealed partial class OnboardingAssistant(OpenAIClient openAIClient, string assistantId)
    : IOnboardingAssistant
{
    /// <summary>
    /// Unique identifier of the Assistant instance to load and operate against.
    /// Provided via the primary constructor.
    /// </summary>
    private readonly string _assistantId = assistantId;

    /// <summary>
    /// Root OpenAI client used to obtain an <see cref="AssistantClient"/>.
    /// </summary>
    private readonly OpenAIClient _openAIClient = openAIClient;

    /// <summary>
    /// Active assistant instance (null until initialized via <see cref="InitializeAsync"/>).
    /// </summary>
    private Assistant? _assistant;

    /// <summary>
    /// Identifier of the current assistant thread (null until first message).
    /// </summary>
    private string? _threadId;

    /// <summary>
    /// Assistant client used for all assistant operations.
    /// </summary>
    private AssistantClient? _assistantClient;

    /// <summary>
    /// Initializes or re-initializes the assistant and associated client state.
    /// This clears any existing state and retrieves the assistant by <see cref="_assistantId"/>.
    /// </summary>
    /// <returns>A task that completes when initialization is finished.</returns>
    /// <remarks>
    /// This method is idempotent and safe to call multiple times. It will reset internal state before initializing.
    /// </remarks>
    public async Task InitializeAsync()
    {
        Cleanup();
        await InitializeAssistantAsync(_assistantId);
    }

    /// <summary>
    /// Resets internal state by releasing references to the assistant, thread, and assistant client.
    /// </summary>
    /// <remarks>
    /// This does not delete the assistant or thread on the server; it only clears local references.
    /// </remarks>
    public void Cleanup()
    {
        _assistant = null;
        _threadId = null;
        _assistantClient = null;
    }

    /// <summary>
    /// Handles a new customer (user) message by ensuring an assistant and thread exist,
    /// appending the message, creating a run, polling its completion, and finally
    /// returning the text of the latest assistant-authored message for the thread.
    /// </summary>
    /// <param name="content">Plaintext user message content.</param>
    /// <returns>The latest assistant-authored message content after the run completes; empty string if none.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the assistant or assistant client is not initialized.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty, or whitespace.</exception>
    public async Task<string> HandleCustomerMessageAsync(string content)
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

        (_threadId, string runId) = await StartNewThreadRunAsync(
            _assistantClient,
            content,
            _assistantId
        );

        await PollRunStatusAsync(_assistantClient, _threadId, runId);
        return GetLatestAssistantMessage(_assistantClient);
    }

    /// <summary>
    /// Retrieves an assistant instance with the provided identifier and configures the internal assistant client.
    /// </summary>
    /// <param name="assistantId">Unique identifier of the Assistant to retrieve.</param>
    /// <returns>A task that completes when the assistant instance is retrieved and the client is set.</returns>
    /// <exception cref="InvalidOperationException">Thrown if an <see cref="AssistantClient"/> cannot be obtained.</exception>
    /// <remarks>
    /// This method assumes that <paramref name="assistantId"/> refers to an existing Assistant resource.
    /// It initializes both the in-memory assistant reference and an <see cref="AssistantClient"/>.
    /// </remarks>
    private async Task InitializeAssistantAsync(string assistantId)
    {
        var assistantClient = _openAIClient.GetAssistantClient();
        _assistant = await assistantClient.GetAssistantAsync(assistantId);
        _assistantClient = _openAIClient.GetAssistantClient();
    }

    /// <summary>
    /// Starts a brand-new thread with an initial user message and immediately creates a run.
    /// </summary>
    /// <param name="assistantClient">Assistant client to use for API calls.</param>
    /// <param name="content">Initial user message content.</param>
    /// <param name="assistantId">Identifier of the assistant to execute the run.</param>
    /// <returns>
    /// A tuple containing the new thread identifier (<c>threadId</c>) and the run identifier (<c>runId</c>).
    /// </returns>
    /// <exception cref="InvalidOperationException">Thrown if the thread and run cannot be created.</exception>
    /// <remarks>
    /// The new thread is created with a single user message containing <paramref name="content"/>.
    /// The run is created immediately against that thread and the specified assistant.
    /// </remarks>
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
    /// Retrieves the latest assistant-authored message text for the current thread.
    /// </summary>
    /// <param name="assistantClient">Assistant client used to fetch messages.</param>
    /// <returns>
    /// The text content of the most recently created assistant message for the active thread,
    /// or an empty string if no assistant messages are available.
    /// </returns>
    /// <remarks>
    /// When no messages exist or the thread is null, an empty string is returned.
    /// Messages are fetched in descending order and filtered by assistant role.
    /// </remarks>
    private string GetLatestAssistantMessage(AssistantClient assistantClient)
    {
        var latestAssistantMessage = string.Empty;

        var options = new MessageCollectionOptions() { Order = MessageCollectionOrder.Descending };
        var messages = assistantClient.GetMessages(_threadId, options);

        var assistantMessages = messages.Where(m => m.Role == MessageRole.Assistant);
        if (!assistantMessages.Any())
            return latestAssistantMessage;

        var message = assistantMessages.Last();
        foreach (
            var contentItem in message.Content.Where(contentItem =>
                !string.IsNullOrEmpty(contentItem.Text)
            )
        )
        {
            latestAssistantMessage = contentItem.Text;
        }

        return latestAssistantMessage;
    }

    /// <summary>
    /// Polls an assistant run until its status becomes terminal (completed, failed, cancelled, etc.).
    /// </summary>
    /// <param name="assistantClient">Assistant client for status checks.</param>
    /// <param name="threadId">Identifier of the thread containing the run.</param>
    /// <param name="runId">Identifier of the run to poll.</param>
    /// <returns>A task that completes when the run reaches a terminal state.</returns>
    /// <remarks>
    /// Polling is performed at a fixed interval of 250 milliseconds.
    /// The loop exits when <see cref="ThreadRun.Status"/> reports a terminal condition.
    /// </remarks>
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
