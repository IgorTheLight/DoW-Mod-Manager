using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Logging;
using Avalonia.Threading;
using DoW_Mod_Manager;
using DoW_Mod_Manager.Composition;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;
using DoW_Mod_Manager.ViewModels;
using DoW_Mod_Manager.Views;
using DoW_Mod_Manager.Views.About;
using DoW_Mod_Manager.Views.ModDownloader;
using DoW_Mod_Manager.Views.ModMerger;
using DoW_Mod_Manager.Views.Settings;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(DowModManager.Tests.Ui.TestAppBuilder))]

namespace DowModManager.Tests.Ui
{
    public static class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }

    /// <summary>
    /// Opens every feature window on a fake Soulstorm install, checks that XAML loads without
    /// binding errors and saves a screenshot (set DMM_SCREENSHOTS to a directory to keep them).
    /// </summary>
    public sealed class WindowSmokeTests : IDisposable
    {
        private readonly string _game;
        private readonly ServiceProvider _services;
        private readonly GameInstall _install;
        private readonly BindingErrorSink _sink = new();

        public WindowSmokeTests()
        {
            _game = FakeGame.Create();
            _install = new GameInstall { InstallDir = _game, ExecutableName = "Soulstorm.exe", Variant = GameVariant.Soulstorm };

            var cache = Path.Combine(_game, "cache");
            Directory.CreateDirectory(cache);
            File.Copy(Path.Combine(FakeGame.RepoAppDir, "ModList", ModlistService.ModlistFileName),
                      Path.Combine(cache, ModlistService.ModlistFileName));

            var sc = new ServiceCollection();
            sc.AddDowModManager();
            sc.AddSingleton<IDialogService, RecordingDialogs>();
            sc.AddSingleton<IModlistService>(new ModlistService(new OfflineDownloads(), cache));
            _services = sc.BuildServiceProvider();

            Logger.Sink = _sink;
        }

        public void Dispose()
        {
            _services.Dispose();
            Directory.Delete(_game, recursive: true);
        }

        private IFeatureViewModelFactory Factory => _services.GetRequiredService<IFeatureViewModelFactory>();

        private void Render(Window window, ViewModelBase vm, string name)
        {
            window.DataContext = vm;
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var frame = window.CaptureRenderedFrame();
            Assert.NotNull(frame);
            if (Environment.GetEnvironmentVariable("DMM_SCREENSHOTS") is { Length: > 0 } dir)
            {
                Directory.CreateDirectory(dir);
                frame!.Save(Path.Combine(dir, name + ".png"));
            }

            window.Close();
            Assert.Empty(_sink.Errors);
        }

        [AvaloniaFact]
        public void ModMergerWindow_Renders()
        {
            var vm = Factory.CreateModMerger(_install, () => { });
            Assert.Equal(["ModA", "ModB"], vm.Modules.Select(m => m.Name));
            Assert.Equal(["ModB", "Ghost"], vm.Entries.Select(e => e.Name));

            vm.SelectedAvailableMod = vm.AvailableMods.First();
            vm.AddCommand.Execute(null);
            Assert.True(vm.SaveCommand.CanExecute(null));

            Render(new ModMergerWindow(), vm, "mod-merger");
        }

        [AvaloniaFact]
        public void ModDownloaderWindow_Renders()
        {
            var vm = Factory.CreateModDownloader(_install, () => { });
            Dispatcher.UIThread.RunJobs();
            SpinWait.SpinUntil(() => !vm.IsLoading, TimeSpan.FromSeconds(5));
            Dispatcher.UIThread.RunJobs();
            Assert.NotEmpty(vm.Mods);

            vm.SearchText = "adeptus";
            Assert.All(vm.Mods, m => Assert.Contains("adeptus", m.Name, StringComparison.OrdinalIgnoreCase));

            Render(new ModDownloaderWindow(), vm, "mod-downloader");
        }

        [AvaloniaFact]
        public void SettingsWindow_Renders_AndPresetMarksDirty()
        {
            LaunchFlagChange? applied = null;
            var vm = Factory.CreateSettings(_install, c => applied = c);
            Assert.False(vm.IsDirty);
            Assert.Equal("1920×1080", vm.ScreenResolution);
            Assert.Equal("Alice", vm.SelectedProfile?.PlayerName);

            vm.ApplyGraphicsPresetCommand.Execute(GraphicsPreset.UltraPlus);
            Assert.True(vm.IsDirty);
            Assert.Equal(3, vm.ShadowsDetail);

            Render(new SettingsWindow(), vm, "settings");

            vm.SaveCommand.Execute(null);
            Assert.Equal(new LaunchFlagChange(true, false), applied);
            Assert.Contains("shadowvolume=1", File.ReadAllText(Path.Combine(_game, "Local.ini")));
        }

        [AvaloniaFact]
        public void SystemPerformanceWindow_Renders() =>
            Render(new SystemPerformanceWindow(), Factory.CreateSystemPerformance(_install), "system-performance");

        [AvaloniaFact]
        public void FontsManagerWindow_Renders()
        {
            var vm = Factory.CreateFontsManager(_install, "1920");
            Assert.True(vm.IsAvailable);
            Assert.Equal("1280", vm.SelectedWidth);
            Assert.Equal(10, vm.Rows.Count);

            Render(new FontsManagerWindow(), vm, "fonts-manager");
        }

        [AvaloniaFact]
        public void AboutWindow_Renders() =>
            Render(new AboutWindow(), Factory.CreateAbout(), "about");

        [AvaloniaFact]
        public void MainWindow_Renders()
        {
            var vm = _services.GetRequiredService<MainWindowViewModel>();
            vm.ModManager.ManualPath = _game;
            vm.ModManager.LoadManualPathCommand.Execute(null);
            Dispatcher.UIThread.RunJobs();

            Render(new MainWindow { Width = 940, Height = 660 }, vm, "main");
        }

        private sealed class BindingErrorSink : ILogSink
        {
            public List<string> Errors { get; } = [];

            public bool IsEnabled(LogEventLevel level, string area) =>
                level >= LogEventLevel.Warning && area == LogArea.Binding;

            public void Log(LogEventLevel level, string area, object? source, string messageTemplate) =>
                Errors.Add(messageTemplate);

            public void Log(LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
                Errors.Add(messageTemplate + " " + string.Join(", ", propertyValues));
        }

        private sealed class RecordingDialogs : IDialogService
        {
            public Task ShowMessageAsync(string message, string title = "") => Task.CompletedTask;
            public Task<bool> ConfirmAsync(string message, string title = "") => Task.FromResult(true);
            public Task OpenUrlAsync(string url) => Task.CompletedTask;
            public Task<string?> PickFileAsync(string title, string? startDirectory = null) => Task.FromResult<string?>(null);
            public void ShowWindow(ViewModelBase viewModel) { }
        }

        private sealed class OfflineDownloads : IDownloadService
        {
            public Task<string> DownloadStringAsync(string url, CancellationToken cancellationToken = default) =>
                throw new HttpRequestException("offline");

            public Task DownloadFileAsync(string url, string destPath, CancellationToken cancellationToken = default) =>
                throw new HttpRequestException("offline");
        }
    }

    /// <summary>A minimal Soulstorm install on disk.</summary>
    internal static class FakeGame
    {
        public static string RepoAppDir
        {
            get
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "DoW Mod Manager", "ModList")))
                    dir = dir.Parent;
                return Path.Combine(dir!.FullName, "DoW Mod Manager");
            }
        }

        public static string Create()
        {
            string game = Path.Combine(Path.GetTempPath(), "dmm_ui_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(game);

            File.WriteAllBytes(Path.Combine(game, "Soulstorm.exe"), []);
            File.WriteAllText(Path.Combine(game, "W40k.module"), "Playable = 0\n");
            File.WriteAllText(Path.Combine(game, "DXP2.module"), "Playable = 0\n");
            File.WriteAllText(Path.Combine(game, "ModA.module"),
                "UIName = Mod A\nPlayable = 1\nModFolder = ModA\nModVersion = 1.0\n" +
                "RequiredMod.1 = DXP2\nRequiredMod.2 = W40k\nRequiredMod.3 = ModB\nRequiredMod.4 = Ghost\n");
            File.WriteAllText(Path.Combine(game, "ModB.module"), "Playable = 1\nModFolder = ModB\n");
            File.WriteAllText(Path.Combine(game, "ModC.module"), "Playable = 0\nModFolder = ModC\n");
            Directory.CreateDirectory(Path.Combine(game, "ModA"));

            File.WriteAllText(Path.Combine(game, "Local.ini"),
                "[global]\nscreenwidth=1920\nscreenheight=1080\nplayerprofile=Profile1\n");
            Directory.CreateDirectory(Path.Combine(game, "Profiles", "Profile1"));
            File.WriteAllText(Path.Combine(game, "Profiles", "Profile1", "name.dat"), "Alice", System.Text.Encoding.Unicode);

            string fonts = Path.Combine(game, "Engine", "Locale", "English", "Data", "font");
            Directory.CreateDirectory(fonts);
            foreach (string file in Directory.GetFiles(Path.Combine(RepoAppDir, "FONT", "font")))
            {
                if (Path.GetFileName(file) != "defaults.zip")
                    File.Copy(file, Path.Combine(fonts, Path.GetFileName(file)));
            }

            return game;
        }
    }
}
