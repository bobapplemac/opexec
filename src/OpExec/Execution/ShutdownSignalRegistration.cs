// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Runtime.InteropServices;

namespace OpExec
{
    internal sealed class ShutdownSignalRegistration : IDisposable
    {
        private readonly PosixSignalRegistration? _terminationRegistration;

        public ShutdownSignalRegistration(CancellationTokenSource shutdown)
        {
            ArgumentNullException.ThrowIfNull(shutdown);

            if (OperatingSystem.IsLinux())
            {
                _terminationRegistration = PosixSignalRegistration.Create(
                    PosixSignal.SIGTERM,
                    context =>
                    {
                        context.Cancel = true;
                        shutdown.Cancel();
                    });
            }
        }

        public void Dispose()
        {
            _terminationRegistration?.Dispose();
        }
    }
}
