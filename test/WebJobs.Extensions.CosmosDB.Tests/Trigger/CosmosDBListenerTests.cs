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
    public class CosmosDBListenerTests
    {
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private ILoggerFactory _loggerFactory;
        private Mock<ITriggeredFunctionExecutor> _mockExecutor;
        private Mock<ICosmosDBService> _mockMonitoredService;
        private Mock<ICosmosDBService> _mockLeasesService;
        private DocumentCollectionInfo _monitoredInfo;
        private DocumentCollectionInfo _leasesInfo;
        private ChangeFeedProcessorOptions _processorOptions;
        private CosmosDBTriggerListener _listener;
        private Mock<IRemainingWorkEstimator> _mockWorkEstimator;
        private string _functionId;

        public CosmosDBListenerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);

            _mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            _functionId = "testfunctionid";

            _mockMonitoredService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            _mockMonitoredService.Setup(m => m.GetClient()).Returns(new DocumentClient(new Uri("http://someurl"), "c29tZV9rZXk="));
            _monitoredInfo = new DocumentCollectionInfo { Uri = new Uri("http://someurl"), MasterKey = "c29tZV9rZXk=", DatabaseName = "MonitoredDB", CollectionName = "MonitoredCollection" };

            _mockLeasesService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            _mockLeasesService.Setup(m => m.GetClient()).Returns(new DocumentClient(new Uri("http://someurl"), "c29tZV9rZXk="));
            _leasesInfo = new DocumentCollectionInfo { Uri = new Uri("http://someurl"), MasterKey = "c29tZV9rZXk=", DatabaseName = "LeasesDB", CollectionName = "LeasesCollection" };

            _processorOptions = new ChangeFeedProcessorOptions();

            // Mock the work estimator so this doesn't require a CosmosDB instance.
            _mockWorkEstimator = new Mock<IRemainingWorkEstimator>(MockBehavior.Strict);

            _listener = new CosmosDBTriggerListener(_mockExecutor.Object, _functionId, _monitoredInfo, _leasesInfo, _processorOptions, _mockMonitoredService.Object, _mockLeasesService.Object, _loggerFactory.CreateLogger<CosmosDBTriggerListener>(), _mockWorkEstimator.Object);
        }

        [Fact]
        public void ScaleMonitorDescriptor_ReturnsExpectedValue()
        {
            Assert.Equal($"{_functionId}-cosmosdbtrigger-{_monitoredInfo.DatabaseName}-{_monitoredInfo.CollectionName}".ToLower(), _listener.Descriptor.Id);
        }

        [Fact]
        public async Task GetMetrics_ReturnsExpectedResult()
        {
            _mockWorkEstimator
                .Setup(m => m.GetEstimatedRemainingWorkPerPartitionAsync())
                .Returns(Task.FromResult((IReadOnlyList<RemainingPartitionWork>)new List<RemainingPartitionWork>()));

            var metrics = await _listener.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            _mockWorkEstimator
                .Setup(m => m.GetEstimatedRemainingWorkPerPartitionAsync())
                .Returns(Task.FromResult((IReadOnlyList<RemainingPartitionWork>)new List<RemainingPartitionWork>()
                {
                    new RemainingPartitionWork("a", 5),
                    new RemainingPartitionWork("b", 5),
                    new RemainingPartitionWork("c", 5),
                    new RemainingPartitionWork("d", 5)
                }));

            metrics = await _listener.GetMetricsAsync();

            Assert.Equal(4, metrics.PartitionCount);
            Assert.Equal(20, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            // verify non-generic interface works as expected
            metrics = (CosmosDBTriggerMetrics)(await ((IScaleMonitor)_listener).GetMetricsAsync());
            Assert.Equal(4, metrics.PartitionCount);
            Assert.Equal(20, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);
        }

        [Fact]
        public async Task GetMetrics_HandlesExceptions()
        {
            // Can't test DocumentClientExceptions because they can't be constructed.

            // InvalidOperationExceptions
            _mockWorkEstimator
                .Setup(m => m.GetEstimatedRemainingWorkPerPartitionAsync())
                .Throws(new InvalidOperationException("Resource Not Found"));

            var metrics = await _listener.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            var warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("Please check that the CosmosDB collection and leases collection exist and are listed correctly in Functions config files.", warning.FormattedMessage);
            _loggerProvider.ClearAllLogMessages();

            // Unknown InvalidOperationExceptions
            _mockWorkEstimator
                .Setup(m => m.GetEstimatedRemainingWorkPerPartitionAsync())
                .Throws(new InvalidOperationException("Unknown"));

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _listener.GetMetricsAsync());

            warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("Unable to handle System.InvalidOperationException: Unknown", warning.FormattedMessage);
            _loggerProvider.ClearAllLogMessages();

            // HttpRequestExceptions
            _mockWorkEstimator
                .Setup(m => m.GetEstimatedRemainingWorkPerPartitionAsync())
                .Throws(new HttpRequestException("Uh oh", new System.Net.WebException("Uh oh again", WebExceptionStatus.NameResolutionFailure)));

            metrics = await _listener.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("CosmosDBTrigger Exception message: Uh oh again.", warning.FormattedMessage);
        }

        [Fact]
        public void GetScaleStatus_NoMetrics_ReturnsVote_None()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 1
            };

            var status = _listener.GetScaleStatus(context);
            Assert.Equal(ScaleVote.None, status.Vote);

            // verify the non-generic implementation works properly
            status = ((IScaleMonitor)_listener).GetScaleStatus(context);
            Assert.Equal(ScaleVote.None, status.Vote);
        }

        [Fact]
        public void GetScaleStatus_InstancesPerPartitionThresholdExceeded_ReturnsVote_ScaleIn()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 2
            };
            var timestamp = DateTime.UtcNow;
            context.Metrics = new List<CosmosDBTriggerMetrics>
            {
                new CosmosDBTriggerMetrics { RemainingWork = 2500, PartitionCount = 1, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2505, PartitionCount = 1, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2612, PartitionCount = 1, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2700, PartitionCount = 1, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2810, PartitionCount = 1, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2900, PartitionCount = 1, Timestamp = timestamp.AddSeconds(15) },
            };

            var status = _listener.GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleIn, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("WorkerCount (2) > PartitionCount (1).", log.FormattedMessage);
            log = logs[1];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("Number of instances (2) is too high relative to number of partitions for collection (MonitoredCollection, 1).", log.FormattedMessage);
        }

        [Fact]
        public void GetScaleStatus_MessagesPerWorkerThresholdExceeded_ReturnsVote_ScaleOut()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 1
            };
            var timestamp = DateTime.UtcNow;
            context.Metrics = new List<CosmosDBTriggerMetrics>
            {
                new CosmosDBTriggerMetrics { RemainingWork = 2500, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2505, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2612, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2700, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2810, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 2900, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
            };

            var status = _listener.GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleOut, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("RemainingWork (2900) > WorkerCount (1) * 1,000.", log.FormattedMessage);
            log = logs[1];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("Remaining work for collection (MonitoredCollection, 2900) is too high relative to the number of instances (1).", log.FormattedMessage);
        }

        [Fact]
        public void GetScaleStatus_ConsistentRemainingWork_ReturnsVote_ScaleOut()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 1
            };
            var timestamp = DateTime.UtcNow;
            context.Metrics = new List<CosmosDBTriggerMetrics>
            {
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 2, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 2, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 2, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 2, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 2, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 2, Timestamp = timestamp.AddSeconds(15) },
            };

            var status = _listener.GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleOut, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("CosmosDB collection 'MonitoredCollection' has documents waiting to be processed.", log.FormattedMessage);
            log = logs[1];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("There are 1 instances relative to 2 partitions.", log.FormattedMessage);
        }

        [Fact]
        public void GetScaleStatus_RemainingWorkIncreasing_ReturnsVote_ScaleOut()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 1
            };
            var timestamp = DateTime.UtcNow;
            context.Metrics = new List<CosmosDBTriggerMetrics>
            {
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 20, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 40, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 80, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 100, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 150, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
            };

            var status = _listener.GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleOut, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("Remaining work is increasing for 'MonitoredCollection'.", log.FormattedMessage);
        }

        [Fact]
        public void GetScaleStatus_RemainingWorkDecreasing_ReturnsVote_ScaleIn()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 1
            };
            var timestamp = DateTime.UtcNow;
            context.Metrics = new List<CosmosDBTriggerMetrics>
            {
                new CosmosDBTriggerMetrics { RemainingWork = 150, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 100, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 80, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 40, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 20, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
                new CosmosDBTriggerMetrics { RemainingWork = 10, PartitionCount = 0, Timestamp = timestamp.AddSeconds(15) },
            };

            var status = _listener.GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleIn, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("Remaining work is decreasing for 'MonitoredCollection'.", log.FormattedMessage);
        }

        [Fact]
        public async Task StartAsync_Retries()
        {
            var listener = new MockListener(_mockExecutor.Object, _functionId, _monitoredInfo, _leasesInfo, _processorOptions, _mockMonitoredService.Object, _mockLeasesService.Object, NullLogger.Instance);

            // Ensure that we can call StartAsync() multiple times to retry if there is an error.
            for (int i = 0; i < 3; i++)
            {
                var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => listener.StartAsync(CancellationToken.None));
                Assert.Equal("Failed to register!", ex.Message);
            }

            // This should succeed
            await listener.StartAsync(CancellationToken.None);
        }

        private class MockListener : CosmosDBTriggerListener
        {
            private int _retries = 0;

            public MockListener(ITriggeredFunctionExecutor executor, string functionId, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedProcessorOptions processorOptions, ICosmosDBService monitoredCosmosDBService, ICosmosDBService leasesCosmosDBService, ILogger logger)
                : base(executor, functionId, documentCollectionLocation, leaseCollectionLocation, processorOptions, monitoredCosmosDBService, leasesCosmosDBService, logger)
            {
            }

            internal override Task StartProcessorAsync()
            {
                if (++_retries <= 3)
                {
                    throw new InvalidOperationException("Failed to register!");
                }

                return Task.CompletedTask;
            }
        }
    }
}