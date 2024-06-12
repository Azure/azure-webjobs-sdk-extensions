// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBTriggerHealthMonitorTests
    {
        [Fact]
        public async Task LogsAcquire()
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();

            await cosmosDBTriggerHealthMonitor.OnLeaseAcquireAsync(leaseToken);

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Debug, loggedEvent.LogLevel);
            Assert.Null(loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken));
        }

        [Fact]
        public async Task LogsRelease()
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();

            await cosmosDBTriggerHealthMonitor.OnLeaseReleaseAsync(leaseToken);

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Debug, loggedEvent.LogLevel);
            Assert.Null(loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken));
        }

        [Fact]
        public void LogsOnChangesDelivered()
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();
            string diagnosticsString = Guid.NewGuid().ToString();
            Mock<CosmosDiagnostics> diagnostics = new Mock<CosmosDiagnostics>();
            diagnostics.Setup(m => m.ToString()).Returns(diagnosticsString);
            Headers headers = new Headers();
            string continuationValue = Guid.NewGuid().ToString();
            headers["x-ms-continuation"] = continuationValue;
            Mock<ChangeFeedProcessorContext> context = new Mock<ChangeFeedProcessorContext>();
            context.Setup(m => m.LeaseToken).Returns(leaseToken);
            context.Setup(m => m.Diagnostics).Returns(diagnostics.Object);
            context.Setup(m => m.Headers).Returns(headers);
            cosmosDBTriggerHealthMonitor.OnChangesDelivered(context.Object);

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Debug, loggedEvent.LogLevel);
            Assert.Null(loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken) && loggedEvent.Message.Contains(diagnosticsString) && loggedEvent.Message.Contains(continuationValue));
        }

        [Theory]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task LogsTransientConnectivity(HttpStatusCode statusCode)
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();
            string diagnosticsString = Guid.NewGuid().ToString();
            Mock<CosmosDiagnostics> diagnostics = new Mock<CosmosDiagnostics>();
            diagnostics.Setup(m => m.ToString()).Returns(diagnosticsString);
            MockedException cosmosException = new MockedException(statusCode, diagnostics.Object);

            await cosmosDBTriggerHealthMonitor.OnErrorAsync(leaseToken, cosmosException);

            Assert.Equal(2, mockedLogger.Events.Count);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Warning, loggedEvent.LogLevel);
            Assert.Equal(cosmosException, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken));

            loggedEvent = mockedLogger.Events[1];
            Assert.Equal(LogLevel.Debug, loggedEvent.LogLevel);
            Assert.Null(loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken) && loggedEvent.Message.Contains(diagnosticsString));
        }

        [Fact]
        public async Task LogsLeaseLost412()
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();
            string diagnosticsString = Guid.NewGuid().ToString();
            Mock<CosmosDiagnostics> diagnostics = new Mock<CosmosDiagnostics>();
            diagnostics.Setup(m => m.ToString()).Returns(diagnosticsString);
            MockedException cosmosException = new MockedException(HttpStatusCode.PreconditionFailed, diagnostics.Object);

            await cosmosDBTriggerHealthMonitor.OnErrorAsync(leaseToken, cosmosException);

            Assert.Equal(2, mockedLogger.Events.Count);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Information, loggedEvent.LogLevel);
            Assert.Equal(cosmosException, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken));

            loggedEvent = mockedLogger.Events[1];
            Assert.Equal(LogLevel.Debug, loggedEvent.LogLevel);
            Assert.Null(loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken) && loggedEvent.Message.Contains(diagnosticsString));
        }

        [Fact]
        public async Task LogsOnUserException()
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();
            string diagnosticsString = Guid.NewGuid().ToString();
            Mock<CosmosDiagnostics> diagnostics = new Mock<CosmosDiagnostics>();
            diagnostics.Setup(m => m.ToString()).Returns(diagnosticsString);
            Exception exception = new Exception();
            Mock<ChangeFeedProcessorContext> context = new Mock<ChangeFeedProcessorContext>();
            context.Setup(m => m.LeaseToken).Returns(leaseToken);
            context.Setup(m => m.Diagnostics).Returns(diagnostics.Object);
            ChangeFeedProcessorUserException userException = new ChangeFeedProcessorUserException(exception, context.Object);

            await cosmosDBTriggerHealthMonitor.OnErrorAsync(leaseToken, userException);

            Assert.Equal(2, mockedLogger.Events.Count);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Warning, loggedEvent.LogLevel);
            Assert.Equal(exception, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken));

            loggedEvent = mockedLogger.Events[1];
            Assert.Equal(LogLevel.Debug, loggedEvent.LogLevel);
            Assert.Null(loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken) && loggedEvent.Message.Contains(diagnosticsString));
        }

        [Fact]
        public async Task LogsOtherException()
        {
            MockedLogger mockedLogger = new MockedLogger();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);
            string leaseToken = Guid.NewGuid().ToString();
            Exception otherException = new Exception();

            await cosmosDBTriggerHealthMonitor.OnErrorAsync(leaseToken, otherException);

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Error, loggedEvent.LogLevel);
            Assert.Equal(otherException, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(leaseToken));
        }

        private class MockedException : CosmosException
        {
            private readonly CosmosDiagnostics diagnostics;

            public MockedException(HttpStatusCode httpStatusCode, CosmosDiagnostics diagnostics) : base("Exception!", httpStatusCode, 0, string.Empty, 0)
            {
                this.diagnostics = diagnostics;
            }

            public override CosmosDiagnostics Diagnostics => this.diagnostics;
        }

        private class MockedLogger : ILogger
        {
            public List<LogEvent> Events { get; private set; } = new List<LogEvent>();

            public IDisposable BeginScope<TState>(TState state)
            {
                throw new NotImplementedException();
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                throw new NotImplementedException();
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            {
                Events.Add(new LogEvent() { LogLevel = logLevel, Exception = exception, Message = state.ToString() });
            }
        }

        private class LogEvent
        {
            public LogLevel LogLevel { get; set; }

            public Exception Exception { get; set; }

            public string Message { get; set; }
        }
    }
}
