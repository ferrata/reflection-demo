using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace test.Utils
{
    public static class Process
    {
        public static async Task<string> RunAsync(string applicationPath, string arguments = null)
        {
            var process = System.Diagnostics.Process.Start(new ProcessStartInfo
            {
                FileName = applicationPath,
                Arguments = arguments ?? "",
                UseShellExecute = false,
                RedirectStandardOutput = true
            });

            await process.WaitForExitAsync(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            if (process == null || process.ExitCode != 0)
            {
                throw new InvalidOperationException($"{applicationPath} exited with {process?.ExitCode ?? -1000}");
            }

            return await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
        }

    }
}