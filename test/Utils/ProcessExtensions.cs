using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32.SafeHandles;

namespace test.Utils
{
    internal static class ProcessExtensions
    {
        public static Task<bool> WaitForExitAsync(this System.Diagnostics.Process process, TimeSpan timeout)
        {
            var processWaitObject = new ManualResetEvent(false);
            processWaitObject.SafeWaitHandle = new SafeWaitHandle(process.Handle, false);

            var completionSource = new TaskCompletionSource<bool>();
            RegisteredWaitHandle registeredProcessWaitHandle = null;

            registeredProcessWaitHandle = ThreadPool.RegisterWaitForSingleObject(
                processWaitObject,
                (_, timedOut) =>
                {
                    if (!timedOut)
                    {
                        // ReSharper disable once PossibleNullReferenceException, AccessToModifiedClosure
                        registeredProcessWaitHandle.Unregister(null);
                    }

                    processWaitObject.Dispose();
                    completionSource.SetResult(!timedOut);
                },
                null /* state */,
                timeout,
                true /* executeOnlyOnce */);

            return completionSource.Task;
        }
    }
}