using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SupportBot.App.ViewModels;

namespace SupportBot.App.Views;

/// <summary>
/// Represents the main page of the application.
/// </summary>
public sealed partial class MainPage : Page
{
    /// <summary>
    /// Gets the main view model for this page.
    /// </summary>
    internal MainViewModel ViewModel { get; } =
        App.Current.Services.GetRequiredService<MainViewModel>();

    /// <summary>
    /// Initializes a new instance of the <see cref="MainPage"/> class.
    /// </summary>
    public MainPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Handles the Loaded event for the page.
    /// </summary>
    private void OnLoaded(object _, RoutedEventArgs __)
    {
        ViewModel.DispatcherQueue = DispatcherQueue;
        ViewModel.Initialize();
    }

    /// <summary>
    /// Called when the page is navigated from.
    /// </summary>
    /// <param name="e">Navigation event data.</param>
    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        ViewModel.Cleanup();
    }
}
