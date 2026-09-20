// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        AgentProtocol.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Implements the bounded SSH-agent identity and signing request protocol, including
//              strict message parsing, algorithm validation, failure isolation, and response
//              construction.
// ------------------------------------------------------------------------------------------

namespace OpExec.SshAgent
{
    internal static class AgentProtocol
    {
        public static async Task<byte[]> HandleAsync(
            ReadOnlyMemory<byte> request,
            ISshIdentityProvider identityProvider,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(identityProvider);

            var reader = new AgentMessageReader(request.Span);

            if (!reader.TryReadByte(out var messageType))
            {
                return CreateFailureResponse();
            }

            if (messageType == AgentMessageType.RequestIdentities && reader.RemainingLength == 0)
            {
                var identities = await identityProvider.GetIdentitiesAsync(cancellationToken);
                return CreateIdentitiesResponse(identities);
            }

            if (messageType == AgentMessageType.SignRequest &&
                TryReadSignRequest(ref reader, out var signRequest))
            {
                return await CreateSignResponseAsync(
                    signRequest,
                    identityProvider,
                    cancellationToken);
            }

            return CreateFailureResponse();
        }

        public static byte[] CreateFailureResponse()
        {
            var writer = new AgentMessageWriter();
            writer.WriteByte(AgentMessageType.Failure);
            return writer.ToArray();
        }

        private static byte[] CreateIdentitiesResponse(IReadOnlyList<SshIdentity> identities)
        {
            ArgumentNullException.ThrowIfNull(identities);

            var writer = new AgentMessageWriter();
            writer.WriteByte(AgentMessageType.IdentitiesAnswer);
            writer.WriteUInt32(checked((uint)identities.Count));

            foreach (var identity in identities)
            {
                writer.WriteBlob(identity.PublicKeyBlob.Span);
                writer.WriteString(identity.Comment);
            }

            return writer.ToArray();
        }

        private static bool TryReadSignRequest(
            ref AgentMessageReader reader,
            out SignRequest signRequest)
        {
            signRequest = default;

            if (!reader.TryReadBlob(out var publicKeyBlob) ||
                !reader.TryReadBlob(out var data) ||
                !reader.TryReadUInt32(out var flags) ||
                reader.RemainingLength != 0)
            {
                return false;
            }

            signRequest = new SignRequest(publicKeyBlob.ToArray(), data.ToArray(), flags);
            return true;
        }

        private static async Task<byte[]> CreateSignResponseAsync(
            SignRequest request,
            ISshIdentityProvider identityProvider,
            CancellationToken cancellationToken)
        {
            var identities = await identityProvider.GetIdentitiesAsync(cancellationToken);
            var identity = identities.FirstOrDefault(candidate =>
                candidate.MatchesPublicKeyBlob(request.PublicKeyBlob));

            if (identity is null)
            {
                return CreateFailureResponse();
            }

            var signature = await identityProvider.SignAsync(
                identity,
                request.Data,
                request.Flags,
                cancellationToken);
            var signatureBlobWriter = new AgentMessageWriter();
            signatureBlobWriter.WriteString(signature.Algorithm);
            signatureBlobWriter.WriteBlob(signature.SignatureBlob.Span);

            var responseWriter = new AgentMessageWriter();
            responseWriter.WriteByte(AgentMessageType.SignResponse);
            responseWriter.WriteBlob(signatureBlobWriter.ToArray());
            return responseWriter.ToArray();
        }

        private readonly record struct SignRequest(
            byte[] PublicKeyBlob,
            byte[] Data,
            uint Flags);
    }
}
