using Microsoft.Extensions.Logging;
using SupportBot.Helpers.CommunityToolkit.ViewModels;

namespace SupportBot.App.ViewModels;

/// <summary>
/// Represents the main view model for the application.
/// Inherits from <see cref="BaseViewModel"/> and provides initialization, cleanup, and disposal logic.
/// </summary>
internal sealed partial class MainViewModel(ILogger<MainViewModel> logger)
    : BaseViewModel(logger)
{ }
