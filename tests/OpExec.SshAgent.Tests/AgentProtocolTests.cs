// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using Xunit;

namespace OpExec.SshAgent.Tests
{
    public sealed class AgentProtocolTests
    {
        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(255)]
        public async Task UnsupportedMessageReturnsFailure(byte messageType)
        {
            var response = await AgentProtocol.HandleAsync(
                new byte[] { messageType },
                new TestIdentityProvider(),
                CancellationToken.None);

            Assert.Equal(new byte[] { AgentMessageType.Failure }, response);
        }

        [Fact]
        public async Task EmptyMessageReturnsFailure()
        {
            var response = await AgentProtocol.HandleAsync(
                Array.Empty<byte>(),
                new TestIdentityProvider(),
                CancellationToken.None);

            Assert.Equal(new byte[] { AgentMessageType.Failure }, response);
        }

        [Fact]
        public async Task IdentityRequestReturnsTestIdentity()
        {
            var response = await AgentProtocol.HandleAsync(
                new byte[] { AgentMessageType.RequestIdentities },
                new TestIdentityProvider(),
                CancellationToken.None);

            AssertTestIdentityResponse(response);
        }

        [Fact]
        public async Task IdentityRequestWithTrailingDataReturnsFailure()
        {
            var response = await AgentProtocol.HandleAsync(
                new byte[] { AgentMessageType.RequestIdentities, 0 },
                new TestIdentityProvider(),
                CancellationToken.None);

            Assert.Equal(new byte[] { AgentMessageType.Failure }, response);
        }

        [Fact]
        public async Task SignRequestReturnsNestedSignatureBlobAndForwardsFlags()
        {
            var identity = new SshIdentity("ssh-ed25519", new byte[] { 1, 2, 3 }, "test");
            var provider = new RecordingIdentityProvider(identity);
            var request = CreateSignRequest(identity.PublicKeyBlob.Span, new byte[] { 4, 5 }, 42);

            var response = await AgentProtocol.HandleAsync(
                request,
                provider,
                CancellationToken.None);

            var reader = new AgentMessageReader(response);
            Assert.True(reader.TryReadByte(out var responseType));
            Assert.Equal(AgentMessageType.SignResponse, responseType);
            Assert.True(reader.TryReadBlob(out var signatureBlob));
            Assert.Equal(0, reader.RemainingLength);

            var signatureReader = new AgentMessageReader(signatureBlob);
            Assert.True(signatureReader.TryReadString(out var algorithm));
            Assert.Equal("ssh-ed25519", algorithm);
            Assert.True(signatureReader.TryReadBlob(out var signature));
            Assert.Equal(new byte[] { 6, 7, 8 }, signature.ToArray());
            Assert.Equal(0, signatureReader.RemainingLength);
            Assert.Equal(identity, provider.SignedIdentity);
            Assert.Equal(new byte[] { 4, 5 }, provider.SignedData);
            Assert.Equal(42U, provider.SignedFlags);
        }

        [Fact]
        public async Task SignRequestForUnknownIdentityReturnsFailure()
        {
            var identity = new SshIdentity("ssh-ed25519", new byte[] { 1 }, "test");
            var provider = new RecordingIdentityProvider(identity);

            var response = await AgentProtocol.HandleAsync(
                CreateSignRequest(new byte[] { 2 }, new byte[] { 3 }, 0),
                provider,
                CancellationToken.None);

            Assert.Equal(new byte[] { AgentMessageType.Failure }, response);
            Assert.Null(provider.SignedIdentity);
        }

        [Theory]
        [MemberData(nameof(MalformedSignRequests))]
        public async Task MalformedSignRequestReturnsFailure(byte[] request)
        {
            var response = await AgentProtocol.HandleAsync(
                request,
                new TestIdentityProvider(),
                CancellationToken.None);

            Assert.Equal(new byte[] { AgentMessageType.Failure }, response);
        }

        public static IEnumerable<object[]> MalformedSignRequests()
        {
            yield return new object[] { new byte[] { AgentMessageType.SignRequest } };
            yield return new object[] { new byte[] { AgentMessageType.SignRequest, 0, 0, 0, 1 } };

            var identity = new TestIdentityProvider()
                .GetIdentitiesAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult()[0];
            var valid = CreateSignRequest(identity.PublicKeyBlob.Span, new byte[] { 1 }, 0);
            yield return new object[] { valid[..^1] };
            yield return new object[] { valid.Concat(new byte[] { 0 }).ToArray() };
        }

        internal static void AssertTestIdentityResponse(byte[] response)
        {
            var reader = new AgentMessageReader(response);

            Assert.True(reader.TryReadByte(out var responseType));
            Assert.Equal(AgentMessageType.IdentitiesAnswer, responseType);
            Assert.True(reader.TryReadUInt32(out var identityCount));
            Assert.Equal(1U, identityCount);
            Assert.True(reader.TryReadBlob(out var publicKeyBlob));
            var identity = new TestIdentityProvider()
                .GetIdentitiesAsync(CancellationToken.None)
                .GetAwaiter()
                .GetResult()[0];
            Assert.Equal(identity.PublicKeyBlob.ToArray(), publicKeyBlob.ToArray());
            Assert.True(reader.TryReadString(out var comment));
            Assert.Equal(TestIdentityProvider.Comment, comment);
            Assert.Equal(0, reader.RemainingLength);
        }

        internal static byte[] CreateSignRequest(
            ReadOnlySpan<byte> publicKeyBlob,
            ReadOnlySpan<byte> data,
            uint flags)
        {
            var writer = new AgentMessageWriter();
            writer.WriteByte(AgentMessageType.SignRequest);
            writer.WriteBlob(publicKeyBlob);
            writer.WriteBlob(data);
            writer.WriteUInt32(flags);
            return writer.ToArray();
        }

        private sealed class RecordingIdentityProvider : ISshIdentityProvider
        {
            private readonly IReadOnlyList<SshIdentity> _identities;

            public RecordingIdentityProvider(SshIdentity identity)
            {
                _identities = new[] { identity };
            }

            public SshIdentity? SignedIdentity { get; private set; }

            public byte[]? SignedData { get; private set; }

            public uint? SignedFlags { get; private set; }

            public Task<IReadOnlyList<SshIdentity>> GetIdentitiesAsync(
                CancellationToken cancellationToken)
            {
                return Task.FromResult(_identities);
            }

            public Task<SshSignature> SignAsync(
                SshIdentity identity,
                ReadOnlyMemory<byte> data,
                uint flags,
                CancellationToken cancellationToken)
            {
                SignedIdentity = identity;
                SignedData = data.ToArray();
                SignedFlags = flags;
                return Task.FromResult(
                    new SshSignature("ssh-ed25519", new byte[] { 6, 7, 8 }));
            }
        }
    }
}
