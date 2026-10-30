using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Models;
using DoW_Mod_Manager.Services;
using DoW_Mod_Manager.ViewModels;
using Xunit;

namespace DowModManager.Tests
{
    /// <summary>The Mod Downloader tells the main window to reload its mod list after an install or uninstall.</summary>
    public class ModDownloaderViewModelTests
    {
        private static readonly DownloadableMod Mod = new("My Mod", "MyMod", "https://site", "https://dl/mymod", "");

        private static readonly GameInstall Install = new() { InstallDir = "/game", ExecutableName = "Soulstorm.exe" };

        private static async Task<ModDownloaderViewModel> Create(FakeInstaller installer, Action onModulesChanged)
        {
            var vm = new ModDownloaderViewModel(Install, "", new FakeModlist(), new FakeDialogs(), null!, installer, onModulesChanged);
            await WaitUntil(() => !vm.IsLoading);
            return vm;
        }

        private static async Task WaitUntil(Func<bool> condition)
        {
            for (int i = 0; i < 200 && !condition(); i++)
                await Task.Delay(10);
            Assert.True(condition());
        }

        [Fact]
        public async Task SuccessfulInstall_ReloadsTheMainModList()
        {
            int reloads = 0;
            var installer = new FakeInstaller { Result = new ModInstallResult(true, "installed") };
            var vm = await Create(installer, () => reloads++);

            await vm.InstallCommand.ExecuteAsync(null);

            Assert.Equal(1, reloads);
            Assert.True(vm.SelectedEntry!.IsInstalled);
        }

        [Fact]
        public async Task FailedInstall_DoesNotReload()
        {
            int reloads = 0;
            var installer = new FakeInstaller { Result = new ModInstallResult(false, "cancelled") };
            var vm = await Create(installer, () => reloads++);

            await vm.InstallCommand.ExecuteAsync(null);

            Assert.Equal(0, reloads);
            Assert.Equal("cancelled", vm.Status);
        }

        [Fact]
        public async Task SuccessfulUninstall_ReloadsTheMainModList()
        {
            int reloads = 0;
            var installer = new FakeInstaller { Installed = true, Result = new ModInstallResult(true, "uninstalled") };
            var vm = await Create(installer, () => reloads++);

            await vm.UninstallCommand.ExecuteAsync(null);

            Assert.Equal(1, reloads);
            Assert.False(vm.SelectedEntry!.IsInstalled);
        }

        private sealed class FakeInstaller : IModInstallService
        {
            public bool Installed { get; set; }
            public ModInstallResult Result { get; set; } = new(true, "");

            public string? DownloadsFolder => "/downloads";
            public bool IsInstalled(GameInstall install, DownloadableMod mod) => Installed;
            public bool CanUninstall(GameInstall install, DownloadableMod mod) => Installed;
            public IReadOnlyList<string> FindDownloadedFiles(DownloadableMod mod) => ["/downloads/mymod.zip"];

            public Task<string> DownloadInBrowserAsync(string url, Func<string, Task> openUrl,
                IProgress<ModInstallProgress> progress, CancellationToken ct = default) => throw new NotSupportedException();

            public Task<ModInstallResult> InstallAsync(GameInstall install, DownloadableMod mod, IReadOnlyList<string> files,
                IProgress<ModInstallProgress> progress, CancellationToken ct = default)
            {
                Installed = Result.Success;
                return Task.FromResult(Result);
            }

            public Task<ModInstallResult> UninstallAsync(GameInstall install, DownloadableMod mod,
                IProgress<ModInstallProgress> progress, CancellationToken ct = default)
            {
                Installed = !Result.Success;
                return Task.FromResult(Result);
            }
        }

        private sealed class FakeModlist : IModlistService
        {
            public string ModlistPath => "/tmp/list";
            public Task<IReadOnlyList<DownloadableMod>> LoadAsync(GameVariant variant, CancellationToken ct = default) =>
                Task.FromResult<IReadOnlyList<DownloadableMod>>([Mod]);
            public IReadOnlyList<DownloadableMod> Parse(string content, GameVariant variant) => [Mod];
        }

        private sealed class FakeDialogs : IDialogService
        {
            public Task ShowMessageAsync(string message, string title = "") => Task.CompletedTask;
            public Task<bool> ConfirmAsync(string message, string title = "") => Task.FromResult(true);
            public Task OpenUrlAsync(string url) => Task.CompletedTask;
            public Task<string?> PickFileAsync(string title, string? startDirectory = null) => Task.FromResult<string?>(null);
            public void ShowWindow(ViewModelBase viewModel) { }
        }
    }
}
