// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using System.Text;

namespace OpExec.SshAgent.Tests
{
    // PUBLIC TEST FIXTURE: this private key is intentionally published and is not a secret.
    // Never authorize it on a real system or use it to protect data. See docs/testing.md.
    internal static class RsaOpenSshTestKey
    {
        public const string PublicKey =
            "ssh-rsa " +
            "AAAAB3NzaC1yc2EAAAADAQABAAABAQDjzejeQs7p2ouN/N47kpA2bVOFOR/VIgpFyDeae+LxzetlLR2G0LveVdcBhx9fKjXbvWQk6Ri6hQRMmVEdcpWcBME/G21pOMQDyW5XkYbFuylmq3sJ4Y0A2Q580LtsCZcCipCDbnSidkXr9mk4Jq5uAVj+Xk07Ee9gGZNMXZD7e2VDfEeNOpLWFCe5mcEXfaDEj4ONe82ZpwrgxtwwVwmt0t2TwNkbsb9QBo0hLGALIB74qzrPR3Az/LMX7n+cd5rTLuL/bi6cMSJMpNoPb8V3Caf01kUfXZRrEgY5kwkxKP6iwssrBYkcLPRiwZnSRGfQTGHFZr8GTeYk33G+w6sV " +
            "opexec-rsa-test-key";

        private const string PrivateKey =
            "-----BEGIN OPENSSH PRIVATE KEY-----\n" +
            "b3BlbnNzaC1rZXktdjEAAAAABG5vbmUAAAAEbm9uZQAAAAAAAAABAAABFwAAAAdzc2gtcn\n" +
            "NhAAAAAwEAAQAAAQEA483o3kLO6dqLjfzeO5KQNm1ThTkf1SIKRcg3mnvi8c3rZS0dhtC7\n" +
            "3lXXAYcfXyo1271kJOkYuoUETJlRHXKVnATBPxttaTjEA8luV5GGxbspZqt7CeGNANkOfN\n" +
            "C7bAmXAoqQg250onZF6/ZpOCaubgFY/l5NOxHvYBmTTF2Q+3tlQ3xHjTqS1hQnuZnBF32g\n" +
            "xI+DjXvNmacK4MbcMFcJrdLdk8DZG7G/UAaNISxgCyAe+Ks6z0dwM/yzF+5/nHea0y7i/2\n" +
            "4unDEiTKTaD2/Fdwmn9NZFH12UaxIGOZMJMSj+osLLKwWJHCz0YsGZ0kRn0ExhxWa/Bk3m\n" +
            "JN9xvsOrFQAAA9BrydeRa8nXkQAAAAdzc2gtcnNhAAABAQDjzejeQs7p2ouN/N47kpA2bV\n" +
            "OFOR/VIgpFyDeae+LxzetlLR2G0LveVdcBhx9fKjXbvWQk6Ri6hQRMmVEdcpWcBME/G21p\n" +
            "OMQDyW5XkYbFuylmq3sJ4Y0A2Q580LtsCZcCipCDbnSidkXr9mk4Jq5uAVj+Xk07Ee9gGZ\n" +
            "NMXZD7e2VDfEeNOpLWFCe5mcEXfaDEj4ONe82ZpwrgxtwwVwmt0t2TwNkbsb9QBo0hLGAL\n" +
            "IB74qzrPR3Az/LMX7n+cd5rTLuL/bi6cMSJMpNoPb8V3Caf01kUfXZRrEgY5kwkxKP6iws\n" +
            "srBYkcLPRiwZnSRGfQTGHFZr8GTeYk33G+w6sVAAAAAwEAAQAAAQBp42mN/qHtQ7/AtTyb\n" +
            "lkngcrry3xWz3UnHHAT0kwdvfFchxXHHG7ln5KC3PhbQBm/Yf1VQbuUmtfPEozN4K63tQq\n" +
            "yM5/PYiCFc3UGkBKoPuSrKZYUgl64SKdK5I9Eqq958uOnpnesrJSzlPLm19wz3Zzr1qeHe\n" +
            "Rjztf5XfVlKxAc8PXqJs6LvrAoAxcHHKpvOwEDGgeJMEMQbDUoptPCsq04HbMHKa9X2AK9\n" +
            "7cw6x5M+9m3yiZ5uKJzd3urpAk+SNZSn7mLm6TCqBMvbVVekmiLDsdKeP8+m0Zgi+KWyYS\n" +
            "RaBpVXy4HEJGSbXoA7ssNpYAayqYLOmmkFO9wMty1PsBAAAAgQCvxOXRiI2YcJ0B/qYfSU\n" +
            "JAXK9wRby9VCjOUiirSra2GAk0IAx6nBIloZvn5NWDx+kSRMNsuegURoYQGYgLoPu1+By6\n" +
            "+euahx+m63K0MJKM9rQXXFrC0egN8jZjxfYE8g85Foa5VHYqf2+PjQCLGZErvgskG34cha\n" +
            "zs+og77EAK9AAAAIEA9KOw7A3pePfk7f9+oOkNjnqtaiSSBzfuiueHIC2+JVmney5D1JOs\n" +
            "ugRhcxL9S0i10Yjh1yxFF24H4ZhEB8MtAIw04CIn88iSPI1wJZS3bPgZvq/hioYfPVxpPN\n" +
            "LYQdJQCjXhItrgZJZ2zCJztxo8v9nCGHwuX3bi4CjGeBxtkdUAAACBAO5iFKOLqRA5AelQ\n" +
            "JcVr2o0S8xo4KXhsU2NBZmj5xt1lVRpJkaRXAkCEzahOjR/LYOTL07aa+KDjMpQyrQ7VGq\n" +
            "HJV/II2lIb8NJW/gfCAubdzG27iicXNeb0Cug88wd2JRgqk996WSy8iXk0qxJtQaGIwxYa\n" +
            "4NerXmYHfQby2RRBAAAAE29wZXhlYy1yc2EtdGVzdC1rZXkBAgMEBQYH\n" +
            "-----END OPENSSH PRIVATE KEY-----\n";

        public static SecretBuffer CreatePrivateKeyBuffer()
        {
            var bytes = Encoding.ASCII.GetBytes(PrivateKey);
            return new SecretBuffer(bytes, bytes.Length);
        }

        public static SshIdentity CreateIdentity()
        {
            if (!OpenSshPublicKey.TryParse(PublicKey, out var algorithm, out var publicKeyBlob))
            {
                throw new InvalidOperationException("The OpenSSH RSA test key is invalid.");
            }

            return new SshIdentity(algorithm, publicKeyBlob, "opexec-rsa-test-key");
        }
    }
}
