// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
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
        private static readonly string DatabaseName = "testDb";
        private static readonly string ContainerName = "testContainer";
        private static readonly string ProcessorName = "theProcessor";

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private readonly ILoggerFactory _loggerFactory;
        private readonly Mock<ITriggeredFunctionExecutor> _mockExecutor;
        private readonly Mock<Container> _monitoredContainer;
        private readonly Mock<Container> _leasesContainer;
        private readonly Mock<FeedIterator<ChangeFeedProcessorState>> _estimatorIterator;
        private readonly CosmosDBTriggerListener<dynamic> _listener;
        private readonly string _functionId;

        public CosmosDBListenerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);

            _mockExecutor = new Mock<ITriggeredFunctionExecutor>();
            _functionId = "testfunctionid";

            var database = new Mock<Database>(MockBehavior.Strict);
            database.Setup(d => d.Id).Returns(DatabaseName);

            _monitoredContainer = new Mock<Container>(MockBehavior.Strict);
            _monitoredContainer.Setup(m => m.Id).Returns(ContainerName);
            _monitoredContainer.Setup(m => m.Database).Returns(database.Object);

            _estimatorIterator = new Mock<FeedIterator<ChangeFeedProcessorState>>();

            Mock<ChangeFeedEstimator> estimator = new Mock<ChangeFeedEstimator>();
            estimator.Setup(m => m.GetCurrentStateIterator(It.IsAny<ChangeFeedEstimatorRequestOptions>()))
                .Returns(_estimatorIterator.Object);

            _leasesContainer = new Mock<Container>(MockBehavior.Strict);
            _leasesContainer.Setup(m => m.Id).Returns(ContainerName);
            _leasesContainer.Setup(m => m.Database).Returns(database.Object);

            _monitoredContainer
                .Setup(m => m.GetChangeFeedEstimator(It.Is<string>(s => s == ProcessorName), It.Is<Container>(c => c == _leasesContainer.Object)))
                .Returns(estimator.Object);

            var attribute = new CosmosDBTriggerAttribute(DatabaseName, ContainerName);

            _listener = new CosmosDBTriggerListener<dynamic>(_mockExecutor.Object, _functionId, ProcessorName, _monitoredContainer.Object, _leasesContainer.Object, attribute, _loggerFactory.CreateLogger<CosmosDBTriggerListener<dynamic>>());
        }

        [Fact]
        public void ScaleMonitorDescriptor_ReturnsExpectedValue()
        {
            Assert.Equal($"{_functionId}-cosmosdbtrigger-{DatabaseName}-{ContainerName}".ToLower(), _listener.Descriptor.Id);
        }

        [Fact]
        public async Task GetMetrics_ReturnsExpectedResult()
        {
            _estimatorIterator
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            Mock<FeedResponse<ChangeFeedProcessorState>> response = new Mock<FeedResponse<ChangeFeedProcessorState>>();
            response
                .Setup(m => m.GetEnumerator())
                .Returns(new List<ChangeFeedProcessorState>().GetEnumerator());

            _estimatorIterator
                .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response.Object));

            var metrics = await _listener.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            _estimatorIterator
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            response
                .Setup(m => m.GetEnumerator())
                .Returns(new List<ChangeFeedProcessorState>()
                {
                    new ChangeFeedProcessorState("a", 5, string.Empty),
                    new ChangeFeedProcessorState("b", 5, string.Empty),
                    new ChangeFeedProcessorState("c", 5, string.Empty),
                    new ChangeFeedProcessorState("d", 5, string.Empty)
                }.GetEnumerator());

            metrics = await _listener.GetMetricsAsync();

            Assert.Equal(4, metrics.PartitionCount);
            Assert.Equal(20, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            _estimatorIterator
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            response
                .Setup(m => m.GetEnumerator())
                .Returns(new List<ChangeFeedProcessorState>()
                {
                                new ChangeFeedProcessorState("a", 5, string.Empty),
                                new ChangeFeedProcessorState("b", 5, string.Empty),
                                new ChangeFeedProcessorState("c", 5, string.Empty),
                                new ChangeFeedProcessorState("d", 5, string.Empty)
                }.GetEnumerator());

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
            _estimatorIterator
                .Setup(m => m.HasMoreResults).Returns(true);

            _estimatorIterator
                .SetupSequence(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new CosmosException("Resource not found", HttpStatusCode.NotFound, 0, string.Empty, 0))
                .ThrowsAsync(new InvalidOperationException("Unknown"))
                .ThrowsAsync(new HttpRequestException("Uh oh", new System.Net.WebException("Uh oh again", WebExceptionStatus.NameResolutionFailure)));

            var metrics = await _listener.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            var warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("Please check that the CosmosDB collection and leases collection exist and are listed correctly in Functions config files.", warning.FormattedMessage);
            _loggerProvider.ClearAllLogMessages();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _listener.GetMetricsAsync());

            warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("Unable to handle System.InvalidOperationException: Unknown", warning.FormattedMessage);
            _loggerProvider.ClearAllLogMessages();

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
            Assert.Equal($"Number of instances (2) is too high relative to number of partitions for collection ({ContainerName}, 1).", log.FormattedMessage);
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
            Assert.Equal($"Remaining work for collection ({ContainerName}, 2900) is too high relative to the number of instances (1).", log.FormattedMessage);
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
            Assert.Equal($"CosmosDB collection '{ContainerName}' has documents waiting to be processed.", log.FormattedMessage);
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
            Assert.Equal($"Remaining work is increasing for '{ContainerName}'.", log.FormattedMessage);
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
            Assert.Equal($"Remaining work is decreasing for '{ContainerName}'.", log.FormattedMessage);
        }

        [Fact]
        public async Task StartAsync_Retries()
        {
            var attribute = new CosmosDBTriggerAttribute("test", "test") { LeaseCollectionPrefix = Guid.NewGuid().ToString() };
           
            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();

            var listener = new MockListener<dynamic>(mockExecutor.Object, _monitoredContainer.Object, _leasesContainer.Object, attribute, NullLogger.Instance);

            // Ensure that we can call StartAsync() multiple times to retry if there is an error.
            for (int i = 0; i < 3; i++)
            {
                var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => listener.StartAsync(CancellationToken.None));
                Assert.Equal("Failed to register!", ex.Message);
            }

            // This should succeed
            await listener.StartAsync(CancellationToken.None);
        }

        private class MockListener<T> : CosmosDBTriggerListener<T>
        {
            private int _retries = 0;

            public MockListener(ITriggeredFunctionExecutor executor,
                Container monitoredContainer,
                Container leaseContainer,
                CosmosDBTriggerAttribute cosmosDBAttribute,
                ILogger logger)
                : base(executor, Guid.NewGuid().ToString(), string.Empty, monitoredContainer, leaseContainer, cosmosDBAttribute, logger)
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

            internal override void InitializeBuilder()
            {
            }
        }
    }
}