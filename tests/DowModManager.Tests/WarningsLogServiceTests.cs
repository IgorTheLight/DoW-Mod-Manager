using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class WarningsLogServiceTests : IDisposable
    {
        private readonly string _dir;
        private readonly IWarningsLogService _service = new WarningsLogService();

        public WarningsLogServiceTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "dmm_warn_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_dir))
                Directory.Delete(_dir, recursive: true);
        }

        [Fact]
        public void GetFatalDataErrors_ReturnsOnlyMatchingLines()
        {
            string[] lines =
            {
                "Some info line",
                "blah Fatal Data Error: missing file A",
                "another info",
                "Fatal Data Error: missing file B"
            };
            File.WriteAllLines(Path.Combine(_dir, "warnings.log"), lines);

            IReadOnlyList<string> errors = _service.GetFatalDataErrors(_dir);

            Assert.Equal(2, errors.Count);
            Assert.All(errors, e => Assert.Contains("Fatal Data Error", e));
        }

        [Fact]
        public void GetFatalDataErrors_ReturnsEmpty_WhenFileAbsent()
        {
            IReadOnlyList<string> errors = _service.GetFatalDataErrors(_dir);

            Assert.Empty(errors);
        }

        [Fact]
        public void GetFatalDataErrors_ReturnsEmpty_WhenNoMatches()
        {
            File.WriteAllLines(Path.Combine(_dir, "warnings.log"), new[] { "all", "clear", "here" });

            IReadOnlyList<string> errors = _service.GetFatalDataErrors(_dir);

            Assert.Empty(errors);
        }
    }
}
