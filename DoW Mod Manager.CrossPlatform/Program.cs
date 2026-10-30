using System;
using Avalonia;

namespace DoW_Mod_Manager;

internal static class Program
{
    // Avalonia configuration; the visual designer also needs this exact signature.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp()
        .StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
