using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NUnit.Framework;
using test.Utils;

namespace test
{
    [TestFixture]
    public abstract class Fixture
    {
        [OneTimeSetUp]
        public async Task SetUpFixture()
        {
            var consolePath = Path.Combine(GetConsoleDebugDir(), "console.exe");
            await Process.RunAsync(consolePath).ConfigureAwait(false);
        }

        protected static string GetConsoleDebugDir([CallerFilePath] string sourceFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(sourceFilePath), "..", "console", "bin", "Debug");

        protected static string GetOwnDebugDir([CallerFilePath] string sourceFilePath = "") =>
            Path.Combine(Path.GetDirectoryName(sourceFilePath) ?? ".", "bin", "Debug");
    }
}