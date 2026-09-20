// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT
//
// ------------------------------------------------------------------------------------------
// File:        AgentDaemonControlProtocol.cs
// Revision:    r3
// Modified:    2026-09-19
// Author:      Andrew J. Moore
// License:     MIT License
// Source:      https://github.com/bobapplemac/opexec
// Description: Defines the private agent lifecycle JSON-RPC 2.0 protocol, its unsigned
//              big-endian length framing, bounded exact reads, request validation, standard
//              error responses, response correlation, and revision-safe serialization.
// ------------------------------------------------------------------------------------------

using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpExec
{
    internal static class AgentDaemonControlProtocol
    {
        private const int FrameHeaderLength = sizeof(uint);
        private const int MaximumMessageLength = 4096;
        private const long RequestId = 1;
        private const string StopMethod = "agent.stop";

        public const string SocketFileName = "control.sock";

        public static byte[] CreateStopRequest()
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                new StopRequestMessage
                {
                    Id = RequestId
                });
        }

        public static StopRequestParseResult ParseStopRequest(
            ReadOnlyMemory<byte> message)
        {
            try
            {
                using var document = JsonDocument.Parse(message);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !TryGetExactString(root, "jsonrpc", "2.0") ||
                    !TryGetIntegerId(root, out var id))
                {
                    return StopRequestParseResult.InvalidRequest();
                }

                if (!root.TryGetProperty("method", out var method) ||
                    method.ValueKind != JsonValueKind.String)
                {
                    return StopRequestParseResult.InvalidRequest(id);
                }

                if (!string.Equals(
                        method.GetString(),
                        StopMethod,
                        StringComparison.Ordinal))
                {
                    return StopRequestParseResult.MethodNotFound(id);
                }

                if (root.TryGetProperty("params", out _))
                {
                    return StopRequestParseResult.InvalidParams(id);
                }

                return StopRequestParseResult.Success(id);
            }
            catch (JsonException)
            {
                return StopRequestParseResult.ParseError();
            }
        }

        public static byte[] CreateSuccessResponse(long id)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                new SuccessResponseMessage
                {
                    Id = id,
                    Result = true
                });
        }

        public static byte[] CreateErrorResponse(
            long? id,
            int code,
            string message)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                new ErrorResponseMessage
                {
                    Id = id,
                    Error = new ErrorMessage
                    {
                        Code = code,
                        Message = message
                    }
                });
        }

        public static void ValidateSuccessResponse(
            ReadOnlyMemory<byte> message)
        {
            try
            {
                using var document = JsonDocument.Parse(message);
                var root = document.RootElement;

                if (root.ValueKind != JsonValueKind.Object ||
                    !TryGetExactString(root, "jsonrpc", "2.0") ||
                    !TryGetIntegerId(root, out var id) ||
                    id != RequestId)
                {
                    throw new InvalidDataException(
                        "The opssh daemon returned an invalid JSON-RPC response.");
                }

                if (root.TryGetProperty("error", out var error))
                {
                    throw new InvalidOperationException(
                        GetErrorMessage(error));
                }

                if (!root.TryGetProperty("result", out var result) ||
                    result.ValueKind is not JsonValueKind.True)
                {
                    throw new InvalidDataException(
                        "The opssh daemon returned an unsuccessful JSON-RPC response.");
                }
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "The opssh daemon returned invalid JSON.",
                    exception);
            }
        }

        public static async Task WriteFrameAsync(
            Stream stream,
            ReadOnlyMemory<byte> message,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            ValidateMessageLength(message.Length);
            var header = new byte[FrameHeaderLength];

            try
            {
                BinaryPrimitives.WriteUInt32BigEndian(
                    header,
                    checked((uint)message.Length));
                await stream.WriteAsync(header, cancellationToken);
                await stream.WriteAsync(message, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }
            finally
            {
                Array.Clear(header);
            }
        }

        public static async Task<byte[]> ReadFrameAsync(
            Stream stream,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(stream);
            var header = new byte[FrameHeaderLength];

            try
            {
                await ReadExactlyAsync(stream, header, cancellationToken);
                var length = BinaryPrimitives.ReadUInt32BigEndian(header);

                if (length == 0 || length > MaximumMessageLength)
                {
                    throw new InvalidDataException(
                        $"The daemon control message length must be between 1 and {MaximumMessageLength} bytes.");
                }

                var message = new byte[checked((int)length)];
                await ReadExactlyAsync(stream, message, cancellationToken);
                return message;
            }
            finally
            {
                Array.Clear(header);
            }
        }

        public static string GetControlSocketPath(string agentSocketPath)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(agentSocketPath);
            var directory = Path.GetDirectoryName(agentSocketPath)
                ?? throw new ArgumentException(
                    "The agent socket path has no parent directory.",
                    nameof(agentSocketPath));
            return Path.Combine(directory, SocketFileName);
        }

        private static async Task ReadExactlyAsync(
            Stream stream,
            Memory<byte> buffer,
            CancellationToken cancellationToken)
        {
            var offset = 0;

            while (offset < buffer.Length)
            {
                var read = await stream.ReadAsync(
                    buffer[offset..],
                    cancellationToken);

                if (read == 0)
                {
                    throw new EndOfStreamException(
                        "The daemon control connection closed before a complete frame was received.");
                }

                offset += read;
            }
        }

        private static void ValidateMessageLength(int length)
        {
            if (length <= 0 || length > MaximumMessageLength)
            {
                throw new InvalidDataException(
                    $"The daemon control message length must be between 1 and {MaximumMessageLength} bytes.");
            }
        }

        private static bool TryGetExactString(
            JsonElement root,
            string propertyName,
            string expected)
        {
            return root.TryGetProperty(propertyName, out var value) &&
                value.ValueKind == JsonValueKind.String &&
                string.Equals(
                    value.GetString(),
                    expected,
                    StringComparison.Ordinal);
        }

        private static bool TryGetIntegerId(
            JsonElement root,
            out long id)
        {
            id = default;
            return root.TryGetProperty("id", out var value) &&
                value.ValueKind == JsonValueKind.Number &&
                value.TryGetInt64(out id);
        }

        private static string GetErrorMessage(JsonElement error)
        {
            if (error.ValueKind == JsonValueKind.Object &&
                error.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return $"The opssh daemon rejected the stop request: {message.GetString()}";
            }

            return "The opssh daemon rejected the stop request.";
        }

        private sealed class StopRequestMessage
        {
            [JsonPropertyName("jsonrpc")]
            public string JsonRpc { get; } = "2.0";

            [JsonPropertyName("method")]
            public string Method { get; } = StopMethod;

            [JsonPropertyName("id")]
            public long Id { get; init; }
        }

        private sealed class SuccessResponseMessage
        {
            [JsonPropertyName("jsonrpc")]
            public string JsonRpc { get; } = "2.0";

            [JsonPropertyName("result")]
            public bool Result { get; init; }

            [JsonPropertyName("id")]
            public long Id { get; init; }
        }

        private sealed class ErrorResponseMessage
        {
            [JsonPropertyName("jsonrpc")]
            public string JsonRpc { get; } = "2.0";

            [JsonPropertyName("error")]
            public required ErrorMessage Error { get; init; }

            [JsonPropertyName("id")]
            public long? Id { get; init; }
        }

        private sealed class ErrorMessage
        {
            [JsonPropertyName("code")]
            public int Code { get; init; }

            [JsonPropertyName("message")]
            public required string Message { get; init; }
        }
    }

    internal sealed class StopRequestParseResult
    {
        private StopRequestParseResult(
            bool shouldStop,
            long? id,
            int? errorCode,
            string? errorMessage)
        {
            ShouldStop = shouldStop;
            Id = id;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool ShouldStop { get; }

        public long? Id { get; }

        public int? ErrorCode { get; }

        public string? ErrorMessage { get; }

        public static StopRequestParseResult Success(long id)
        {
            return new StopRequestParseResult(true, id, null, null);
        }

        public static StopRequestParseResult ParseError()
        {
            return Error(null, -32700, "Parse error");
        }

        public static StopRequestParseResult InvalidRequest(long? id = null)
        {
            return Error(id, -32600, "Invalid Request");
        }

        public static StopRequestParseResult MethodNotFound(long id)
        {
            return Error(id, -32601, "Method not found");
        }

        public static StopRequestParseResult InvalidParams(long id)
        {
            return Error(id, -32602, "Invalid params");
        }

        private static StopRequestParseResult Error(
            long? id,
            int code,
            string message)
        {
            return new StopRequestParseResult(
                false,
                id,
                code,
                message);
        }
    }
}
