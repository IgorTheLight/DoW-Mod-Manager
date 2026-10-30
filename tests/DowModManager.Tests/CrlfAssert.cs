using Xunit;

namespace DowModManager.Tests
{
    /// <summary>Asserts that a file uses Windows line endings only.</summary>
    internal static class CrlfAssert
    {
        public static void File(string path)
        {
            string text = System.IO.File.ReadAllText(path);
            string withoutCrlf = text.Replace("\r\n", "");
            Assert.Contains("\r\n", text);
            Assert.DoesNotContain("\n", withoutCrlf);
            Assert.DoesNotContain("\r", withoutCrlf);
        }
    }
}
