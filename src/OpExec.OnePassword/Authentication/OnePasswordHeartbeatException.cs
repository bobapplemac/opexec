// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

namespace OpExec.OnePassword
{
    internal sealed class OnePasswordHeartbeatException : Exception
    {
        public OnePasswordHeartbeatException(string message)
            : base(message)
        {
        }
    }
}
