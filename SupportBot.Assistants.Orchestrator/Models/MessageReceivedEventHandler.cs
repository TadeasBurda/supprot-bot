#pragma warning disable OPENAI001 // For evaluation purposes only

using OpenAI.Assistants;
using System.ClientModel;

namespace SupportBot.Assistants.Orchestrator.Models;

public delegate void MessageReceivedEventHandler(CollectionResult<ThreadMessage> messages);
