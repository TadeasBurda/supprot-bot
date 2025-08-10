using System;
using System.Collections.ObjectModel;
using Microsoft.UI.Xaml;
using SupportBot.Helpers.CommunityToolkit.ViewModels;
using SupportBot.UI.ChatWindowKit.Models;

namespace SupportBot.UI.ChatWindowKit.ViewModels;

/// <summary>
/// View model for the chat window. Manages the collection of chat messages and handles initialization and cleanup logic.
/// </summary>
internal sealed partial class ChatViewModel : BaseViewModel
{
    private const int MAX_MESSAGES = 100;

    /// <summary>
    /// Gets the collection of chat messages displayed in the chat window.
    /// </summary>
    internal ObservableCollection<Message> Messages { get; } = [];

    /// <summary>
    /// Cleans up resources used by the chat view model, including clearing the message collection.
    /// </summary>
    public override void Cleanup()
    {
        Messages.Clear();
    }

    /// <summary>
    /// Initializes the chat view model by clearing existing messages and adding default welcome messages.
    /// </summary>
    public override void Initialize()
    {
        Messages.Clear();
        AddMessage(
            new Message
            {
                MsgText = "Welcome to the chat!",
                MsgAlignment = HorizontalAlignment.Left,
                MsgDateTime = DateTime.Now.ToString("hh:ss tt"),
            }
        );
        AddMessage(
            new Message
            {
                MsgText = "How can I assist you today?",
                MsgAlignment = HorizontalAlignment.Right,
                MsgDateTime = DateTime.Now.ToString("hh:ss tt"),
            }
        );
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
    /// If the number of messages exceeds the maximum allowed, removes the oldest message.
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
