using System;
using SupportBot.Helpers.CommunityToolkit.ViewModels;

namespace SupportBot.UI.ChatWindowKit.ViewModels;

internal sealed partial class ChatViewModel : BaseViewModel
{
    public override void Cleanup()
    {
        throw new NotImplementedException();
    }

    public override void Initialize()
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// Releases the unmanaged resources used by the main view model and optionally releases the managed resources.
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
        }

        // Dispose unmanaged resources here if any

        _disposed = true;

        base.Dispose(disposing);
    }
}
