#pragma warning disable OPENAI001 // For evaluation purposes only

using System;
using System.Text.Json;
using System.Threading.Tasks;
using OpenAI;
using OpenAI.Assistants;
using SupportBot.Assistants.Onboarding;
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
public interface IOrchestrator : IDisposable
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
/// Main implementation of <see cref="IOrchestrator"/> that encapsulates interaction with the OpenAI Assistants API.
/// Responsible for:
/// - Creating and configuring an assistant.
/// - Managing a single conversation thread lifecycle.
/// - Sending user messages and triggering runs.
/// - Polling run status until terminal.
/// - Publishing full message history upon completion.
/// </summary>
/// <param name="openAIClient">The configured <see cref="OpenAIClient"/> used to obtain assistant clients.</param>
/// <param name="assistantId">The unique identifier of an existing Assistant to use for this orchestrator.</param>
/// <param name="onboardingAssistant">Secondary agent used to handle onboarding-related tool calls delegated by the main agent.</param>
internal sealed partial class Orchestrator(
    OpenAIClient openAIClient,
    string assistantId,
    IOnboardingAssistant onboardingAssistant
) : IOrchestrator
{
    /// <summary>
    /// Secondary agent used to handle onboarding-related tool calls delegated by the main agent.
    /// </summary>
    private readonly IOnboardingAssistant _onboardingAssistant = onboardingAssistant;

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
    /// Active assistant instance (null until initialized).
    /// </summary>
    private Assistant? _assistant;

    /// <summary>
    /// Backing store for the <see cref="IOrchestrator.MessageReceived"/> event.
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

    /// <summary>
    /// Raised after a run completes; handlers are added to or removed from the internal backing field.
    /// </summary>
    /// <remarks>
    /// The event payload provides the full ordered message collection representing the current thread history.
    /// </remarks>
    event MessageReceivedEventHandler? IOrchestrator.MessageReceived
    {
        add => _messageReceived += value;
        remove => _messageReceived -= value;
    }

    /// <summary>
    /// Initializes the main assistant and related runtime state, then initializes the onboarding assistant.
    /// </summary>
    /// <returns>A task that completes when both assistants are initialized.</returns>
    /// <remarks>
    /// This method is safe to call multiple times. It clears prior state via <see cref="Cleanup"/> before initializing.
    /// </remarks>
    public async Task InitializeAsync()
    {
        Cleanup();
        await InitializeAssistantAsync(_assistantId);
        await _onboardingAssistant.InitializeAsync();
    }

    /// <summary>
    /// Resets internal state by clearing the main agent's references and delegating cleanup to the onboarding assistant.
    /// </summary>
    /// <remarks>
    /// This does not delete any remote resources; it only clears local references and event subscriptions.
    /// </remarks>
    public void Cleanup()
    {
        _onboardingAssistant.Cleanup();
        _assistant = null;
        _messageReceived = null;
        _threadId = null;
        _assistantClient = null;
    }

    /// <summary>
    /// Processes a user message by creating or continuing a thread, starting a run, waiting for completion,
    /// and notifying subscribers with the updated thread messages through <see cref="_messageReceived"/>.
    /// </summary>
    /// <param name="content">Plaintext user message content.</param>
    /// <returns>A task that completes when processing finishes and notifications are raised.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the assistant or assistant client is not initialized, or if creating a run fails.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="content"/> is null, empty, or whitespace.</exception>
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

        string? runId;
        if (string.IsNullOrEmpty(_threadId))
        {
            (_threadId, runId) = await StartNewThreadRunAsync(
                _assistantClient,
                content,
                _assistantId
            );
        }
        else
        {
            runId = await ContinueThreadRunAsync(_assistantClient, content, _assistantId);
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
    /// <param name="assistantId">Unique identifier of the Assistant to retrieve.</param>
    /// <returns>Task representing the asynchronous creation operation.</returns>
    private async Task InitializeAssistantAsync(string assistantId)
    {
        var assistantClient = _openAIClient.GetAssistantClient();
        _assistant = await assistantClient.GetAssistantAsync(assistantId);
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
    /// <see cref="IOrchestrator.MessageReceived"/> event.
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
    /// Also handles tool calls by delegating to the onboarding assistant and submitting tool outputs.
    /// </summary>
    /// <param name="assistantClient">Assistant client for status checks and tool output submission.</param>
    /// <param name="threadId">Identifier of the thread containing the run.</param>
    /// <param name="runId">Identifier of the run to poll.</param>
    /// <returns>A task that completes when the run reaches a terminal state.</returns>
    private async Task PollRunStatusAsync(
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

            if (runStatus.RequiredActions?.Count > 0)
            {
                foreach (var action in runStatus.RequiredActions)
                {
                    if (action.FunctionName == "CallOnboardingAgent")
                    {
                        var query = string.Empty;

                        if (action.FunctionArguments is string argsJson)
                        {
                            try
                            {
                                using var doc = JsonDocument.Parse(argsJson);
                                var root = doc.RootElement;

                                if (
                                    root.TryGetProperty("query", out var queryElement)
                                    && queryElement.ValueKind == JsonValueKind.String
                                )
                                {
                                    query = queryElement.GetString() ?? query;
                                }
                            }
                            catch (JsonException)
                            {
                                // Ignore malformed JSON and keep defaults.
                            }
                        }

                        var agentOutput = await _onboardingAssistant.HandleCustomerMessageAsync(
                            content: query
                        );
                        await assistantClient.SubmitToolOutputsToRunAsync(
                            threadId,
                            runId,
                            [
                                new ToolOutput()
                                {
                                    Output = agentOutput,
                                    ToolCallId = action.ToolCallId,
                                },
                            ]
                        );
                    }
                }
            }
        } while (runStatus is { Status.IsTerminal: false });
    }

    /// <summary>
    /// Releases resources and clears state by delegating to <see cref="Cleanup"/> and disposing the onboarding assistant.
    /// </summary>
    public void Dispose()
    {
        Cleanup();
        _onboardingAssistant.Dispose();
    }
}
