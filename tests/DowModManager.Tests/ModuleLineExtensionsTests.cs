using DoW_Mod_Manager.Services;
using Xunit;

namespace DowModManager.Tests
{
    public class ModuleLineExtensionsTests
    {
        [Theory]
        [InlineData("ModFolder = DawnOfWar", false, "DawnOfWar")]
        [InlineData("ModVersion = 1.0", false, "1.0")]
        [InlineData("RequiredMod.1 = W40k.module", false, "W40k.module")]
        [InlineData("RequiredMod.1 = W40k.module", true, "W40k")]
        [InlineData("ModFolder=NoSpaces", false, "NoSpaces")]
        [InlineData("ModFolder   =   Spaced   Out", false, "SpacedOut")]
        public void GetValueFromLine_ExtractsValue(string line, bool deleteModule, string expected)
        {
            Assert.Equal(expected, line.GetValueFromLine(deleteModule));
        }

        [Theory]
        [InlineData("NoEqualSignHere")]
        [InlineData("=LeadingEqualReturnsEmpty")]
        [InlineData("")]
        public void GetValueFromLine_ReturnsEmpty_WhenNoPositiveEqualIndex(string line)
        {
            Assert.Equal("", line.GetValueFromLine(false));
        }

        [Fact]
        public void GetValueFromLine_DeleteModule_StripsModuleSuffixAndSpaces()
        {
            Assert.Equal("SomeMod", "RequiredMod.2 = Some Mod.module".GetValueFromLine(true));
        }
    }
}
