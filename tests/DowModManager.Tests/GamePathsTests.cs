using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class GamePathsTests : IDisposable
    {
        private readonly string _dir;

        public GamePathsTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_paths_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(_dir, "Engine", "Data"));
            File.WriteAllText(Path.Combine(_dir, "DXP2.module"), "");
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void Resolve_ExactCase_IsReturnedAsIs() =>
            Assert.Equal(Path.Combine(_dir, "DXP2.module"), GamePaths.Resolve(_dir, "DXP2.module"));

        [Fact]
        public void Resolve_UsesOnDiskCasing() =>
            AssertSamePath(Path.Combine(_dir, "DXP2.module"), GamePaths.Resolve(_dir, "dxp2.module"));

        [Fact]
        public void Resolve_SplitsWindowsAndUnixSeparators()
        {
            string expected = Path.Combine(_dir, "Engine", "Data");
            AssertSamePath(expected, GamePaths.Resolve(_dir, @"engine\data"));
            AssertSamePath(expected, GamePaths.Resolve(_dir, "ENGINE/DATA"));
        }

        [Fact]
        public void Resolve_MissingSegmentsKeepGivenCasing() =>
            AssertSamePath(Path.Combine(_dir, "Engine", "NoSuch", "File.txt"),
                GamePaths.Resolve(_dir, "engine", @"NoSuch\File.txt"));

        // On Windows the exact-case fast path already hits, so the given casing is kept there.
        private static void AssertSamePath(string expected, string actual) =>
            Assert.Equal(expected, actual, ignoreCase: OperatingSystem.IsWindows());
    }
}
