// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Exceptions;
using Microsoft.Azure.Documents.ChangeFeedProcessor.Monitoring;
using Microsoft.Azure.Documents.ChangeFeedProcessor.PartitionManagement;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests.Trigger
{
    public class CosmosDBTriggerHealthMonitorTests
    {
        [Fact]
        public async Task LogsCritical()
        {
            MockedLogger mockedLogger = new MockedLogger();
            Exception exception = new Exception();
            MockedLease lease = new MockedLease();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);

            await cosmosDBTriggerHealthMonitor.InspectAsync(new HealthMonitoringRecord(HealthSeverity.Critical, MonitoredOperation.AcquireLease, lease, exception));

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Critical, loggedEvent.LogLevel);
            Assert.Equal(exception, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(lease.ToString()) && loggedEvent.Message.Contains(MonitoredOperation.AcquireLease.ToString()));
        }

        [Fact]
        public async Task LogsError()
        {
            MockedLogger mockedLogger = new MockedLogger();
            Exception exception = new Exception();
            MockedLease lease = new MockedLease();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);

            await cosmosDBTriggerHealthMonitor.InspectAsync(new HealthMonitoringRecord(HealthSeverity.Error, MonitoredOperation.AcquireLease, lease, exception));

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Error, loggedEvent.LogLevel);
            Assert.Equal(exception, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(lease.ToString()) && loggedEvent.Message.Contains(MonitoredOperation.AcquireLease.ToString()));
            Assert.True(loggedEvent.Message.Contains("encountered an error"));
        }

        [Fact]
        public async Task LogsLeaseLost()
        {
            MockedLogger mockedLogger = new MockedLogger();
            LeaseLostException exception = new LeaseLostException();
            MockedLease lease = new MockedLease();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);

            await cosmosDBTriggerHealthMonitor.InspectAsync(new HealthMonitoringRecord(HealthSeverity.Error, MonitoredOperation.AcquireLease, lease, exception));

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Warning, loggedEvent.LogLevel);
            Assert.Equal(exception, loggedEvent.Exception);
            Assert.True(loggedEvent.Message.Contains(lease.ToString()) && loggedEvent.Message.Contains(MonitoredOperation.AcquireLease.ToString()));
            Assert.True(loggedEvent.Message.Contains("This is expected during scaling and briefly"));
        }

        [Fact]
        public async Task LogsTrace()
        {
            MockedLogger mockedLogger = new MockedLogger();
            MockedLease lease = new MockedLease();
            CosmosDBTriggerHealthMonitor cosmosDBTriggerHealthMonitor = new CosmosDBTriggerHealthMonitor(mockedLogger);

            await cosmosDBTriggerHealthMonitor.InspectAsync(new HealthMonitoringRecord(HealthSeverity.Informational, MonitoredOperation.AcquireLease, lease, null));

            Assert.Single(mockedLogger.Events);

            LogEvent loggedEvent = mockedLogger.Events[0];
            Assert.Equal(LogLevel.Trace, loggedEvent.LogLevel);
            Assert.True(loggedEvent.Message.Contains(lease.ToString()) && loggedEvent.Message.Contains(MonitoredOperation.AcquireLease.ToString()));
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

        private class MockedLease : ILease
        {
            public string PartitionId => throw new NotImplementedException();

            public string Owner { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public DateTime Timestamp { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string ContinuationToken { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public string Id => throw new NotImplementedException();

            public string ConcurrencyToken => throw new NotImplementedException();

            public Dictionary<string, string> Properties { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

            public override string ToString()
            {
                return "Mocked Lease";
            }
        }
    }
}
