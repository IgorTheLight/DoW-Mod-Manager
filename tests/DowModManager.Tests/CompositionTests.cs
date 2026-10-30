using DoW_Mod_Manager.Abstractions;
using DoW_Mod_Manager.Composition;
using DoW_Mod_Manager.Services;
using DoW_Mod_Manager.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DowModManager.Tests
{
    public class CompositionTests
    {
        /// <summary>
        /// Regression: the composition root used to re-register the platform sub-services through
        /// IPlatformService while the Linux module builds IPlatformService from them — a resolution
        /// cycle that froze the app at start-up.
        /// </summary>
        [Fact]
        public async Task Container_ResolvesPlatformAndServices_WithoutCycles()
        {
            var resolve = Task.Run(() =>
            {
                var services = new ServiceCollection();
                services.AddDowModManager();
                using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

                var platform = provider.GetRequiredService<IPlatformService>();
                Assert.Same(platform.Launcher, provider.GetRequiredService<IGameLauncher>());
                Assert.Same(platform.InstallLocator, provider.GetRequiredService<IGameInstallLocator>());

                provider.GetRequiredService<IModMergerService>();
                provider.GetRequiredService<IGameSettingsService>();
                provider.GetRequiredService<IFontsService>();
                provider.GetRequiredService<IModlistService>();
                provider.GetRequiredService<IGameExtrasService>();
                provider.GetRequiredService<IFeatureViewModelFactory>();
            });

            var finished = await Task.WhenAny(resolve, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(finished == resolve, "Service resolution did not complete (cycle?)");
            await resolve;
        }
    }
}
