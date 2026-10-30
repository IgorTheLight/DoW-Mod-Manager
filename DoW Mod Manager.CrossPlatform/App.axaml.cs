using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using DoW_Mod_Manager.Composition;
using DoW_Mod_Manager.ViewModels;
using DoW_Mod_Manager.Views;
using Microsoft.Extensions.DependencyInjection;

namespace DoW_Mod_Manager;

public partial class App : Application
{
    /// <summary>Root DI container. Built once in <see cref="OnFrameworkInitializationCompleted"/>.</summary>
    public static IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        services.AddDowModManager();
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Avalonia's default binding also tries to validate via INotifyDataErrorInfo;
            // remove the duplicate that fights CommunityToolkit.Mvvm validation.
            DisableAvaloniaDataAnnotationValidation();

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>()
            };
            // Stops background work such as waiting for a launched game's process.
            desktop.MainWindow.Closed += (_, _) => Services.GetRequiredService<ModManagerViewModel>().OnClosed();
        }

        base.OnFrameworkInitializationCompleted();
    }

    // DataValidators is marked as unsafe to trim because the plugins it holds use reflection.
    // This only removes one of them.
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "Removes a plugin; no plugin is looked up or invoked by reflection.")]
    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var toRemove = BindingPlugins.DataValidators
            .OfType<DataAnnotationsValidationPlugin>()
            .ToArray();

        foreach (var plugin in toRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
