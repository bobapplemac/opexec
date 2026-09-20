// SPDX-FileCopyrightText: © 2026 Andrew J. Moore
// SPDX-FileContributor: Andrew J. Moore
// SPDX-License-Identifier: MIT

using OpExec.OnePassword;
using Xunit;

namespace OpExec.Tests
{
    public sealed class ExecutionSupervisorTests
    {
        [Fact]
        public async Task AgentSocketIsInjectedAndAgentIsDisposedAfterChildExit()
        {
            var agent = new FakeIntegratedSshAgent("/run/user/1000/opexec/agent.sock");
            var factory = new FakeIntegratedSshAgentFactory(agent);
            var runner = new FakeForegroundProcessRunner(27);
            var authentication = new FakeAuthenticationContext(
                new Dictionary<string, string?>
                {
                    ["OP_SESSION"] = "temporary-session"
                });
            var request = CreateRequest(
                new Dictionary<string, string?>
                {
                    ["SSH_AUTH_SOCK"] = "/tmp/parent-agent.sock",
                    ["PRESERVED"] = "value"
                });

            var exitCode = await new ExecutionSupervisor(
                new FakeAuthenticationManager(authentication),
                factory,
                runner).RunAsync(
                request,
                null,
                CancellationToken.None);

            Assert.Equal(27, exitCode);
            Assert.True(agent.IsDisposed);
            Assert.True(authentication.IsDisposed);
            Assert.Equal(1, factory.StartCount);
            Assert.Equal("temporary-session", factory.EnvironmentVariables!["OP_SESSION"]);
            var childRequest = Assert.IsType<ForegroundProcessRequest>(runner.Request);
            Assert.Equal(agent.SocketPath, childRequest.EnvironmentVariables["SSH_AUTH_SOCK"]);
            Assert.Equal(
                "temporary-session",
                childRequest.EnvironmentVariables["OP_SESSION"]);
            Assert.Equal("value", childRequest.EnvironmentVariables["PRESERVED"]);
            Assert.Equal(
                "/tmp/parent-agent.sock",
                request.EnvironmentVariables["SSH_AUTH_SOCK"]);
        }

        [Fact]
        public async Task AgentIsDisposedWhenChildExecutionFails()
        {
            var agent = new FakeIntegratedSshAgent("/tmp/agent.sock");
            var expected = new InvalidOperationException("child failed");
            var runner = new FakeForegroundProcessRunner(expected);
            var supervisor = new ExecutionSupervisor(
                new FakeAuthenticationManager(new FakeAuthenticationContext()),
                new FakeIntegratedSshAgentFactory(agent),
                runner);

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => supervisor.RunAsync(
                    CreateRequest(),
                    null,
                    CancellationToken.None));

            Assert.Same(expected, actual);
            Assert.True(agent.IsDisposed);
        }

        [Fact]
        public async Task AuthenticationIsDisposedWhenAgentStartupFails()
        {
            var authentication = new FakeAuthenticationContext();
            var expected = new InvalidOperationException("agent failed");
            var supervisor = new ExecutionSupervisor(
                new FakeAuthenticationManager(authentication),
                new FakeIntegratedSshAgentFactory(expected),
                new FakeForegroundProcessRunner(0));

            var actual = await Assert.ThrowsAsync<InvalidOperationException>(
                () => supervisor.RunAsync(
                    CreateRequest(),
                    null,
                    CancellationToken.None));

            Assert.Same(expected, actual);
            Assert.True(authentication.IsDisposed);
        }

        [Fact]
        public async Task LogAndCancellationTokenReachAgentFactoryAndChildRunner()
        {
            var agent = new FakeIntegratedSshAgent("/tmp/agent.sock");
            var factory = new FakeIntegratedSshAgentFactory(agent);
            var runner = new FakeForegroundProcessRunner(0);
            var authenticationManager = new FakeAuthenticationManager(
                new FakeAuthenticationContext());
            using var cancellation = new CancellationTokenSource();
            Action<string> log = _ => { };

            await new ExecutionSupervisor(authenticationManager, factory, runner).RunAsync(
                CreateRequest(),
                log,
                cancellation.Token);

            Assert.Same(log, factory.Log);
            Assert.Equal(cancellation.Token, authenticationManager.CancellationToken);
            Assert.Equal(cancellation.Token, factory.CancellationToken);
            Assert.Equal(cancellation.Token, runner.CancellationToken);
        }

        private static ForegroundProcessRequest CreateRequest(
            IReadOnlyDictionary<string, string?>? environmentVariables = null)
        {
            return new ForegroundProcessRequest(
                "child",
                new[] { "argument" },
                Environment.CurrentDirectory,
                environmentVariables);
        }

        private sealed class FakeIntegratedSshAgent : IIntegratedSshAgent
        {
            public FakeIntegratedSshAgent(string socketPath)
            {
                SocketPath = socketPath;
            }

            public string SocketPath { get; }

            public bool IsDisposed { get; private set; }

            public ValueTask DisposeAsync()
            {
                IsDisposed = true;
                return ValueTask.CompletedTask;
            }
        }

        private sealed class FakeIntegratedSshAgentFactory : IIntegratedSshAgentFactory
        {
            private readonly IIntegratedSshAgent? _agent;
            private readonly Exception? _exception;

            public FakeIntegratedSshAgentFactory(IIntegratedSshAgent agent)
            {
                _agent = agent;
            }

            public FakeIntegratedSshAgentFactory(Exception exception)
            {
                _exception = exception;
            }

            public int StartCount { get; private set; }

            public Action<string>? Log { get; private set; }

            public CancellationToken CancellationToken { get; private set; }

            public IReadOnlyDictionary<string, string?>? EnvironmentVariables { get; private set; }

            public Task<IIntegratedSshAgent> StartAsync(
                IReadOnlyDictionary<string, string?> environmentVariables,
                Action<string>? log,
                CancellationToken cancellationToken)
            {
                StartCount++;
                EnvironmentVariables = new Dictionary<string, string?>(environmentVariables);
                Log = log;
                CancellationToken = cancellationToken;

                if (_exception is not null)
                {
                    return Task.FromException<IIntegratedSshAgent>(_exception);
                }

                return Task.FromResult(_agent!);
            }
        }

        private sealed class FakeAuthenticationManager : IOnePasswordAuthenticationManager
        {
            private readonly IOnePasswordAuthenticationContext _authentication;

            public FakeAuthenticationManager(IOnePasswordAuthenticationContext authentication)
            {
                _authentication = authentication;
            }

            public CancellationToken CancellationToken { get; private set; }

            public Task<IOnePasswordAuthenticationContext> EstablishAsync(
                CancellationToken cancellationToken)
            {
                CancellationToken = cancellationToken;
                return Task.FromResult(_authentication);
            }
        }

        private sealed class FakeAuthenticationContext : IOnePasswordAuthenticationContext
        {
            public FakeAuthenticationContext(
                IReadOnlyDictionary<string, string?>? environmentVariables = null)
            {
                EnvironmentVariables = environmentVariables ??
                    new Dictionary<string, string?>();
            }

            public IReadOnlyDictionary<string, string?> EnvironmentVariables { get; }

            public bool IsDisposed { get; private set; }

            public void Dispose()
            {
                IsDisposed = true;
            }
        }

        private sealed class FakeForegroundProcessRunner : IForegroundProcessRunner
        {
            private readonly int _exitCode;
            private readonly Exception? _exception;

            public FakeForegroundProcessRunner(int exitCode)
            {
                _exitCode = exitCode;
            }

            public FakeForegroundProcessRunner(Exception exception)
            {
                _exception = exception;
            }

            public ForegroundProcessRequest? Request { get; private set; }

            public CancellationToken CancellationToken { get; private set; }

            public Task<int> RunAsync(
                ForegroundProcessRequest request,
                CancellationToken cancellationToken)
            {
                Request = request;
                CancellationToken = cancellationToken;

                if (_exception is not null)
                {
                    return Task.FromException<int>(_exception);
                }

                return Task.FromResult(_exitCode);
            }
        }
    }
}
