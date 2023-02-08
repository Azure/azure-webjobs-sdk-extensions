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
    public class CosmosDBScaleMonitorTests
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
        private readonly string _functionId;
        private readonly string _logDetails;
        private readonly IScaleMonitor _scaleMonitor;

        public CosmosDBScaleMonitorTests()
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

            _logDetails = $"prefix='{ProcessorName}', monitoredContainer='{ContainerName}', monitoredDatabase='{DatabaseName}', " +
                $"leaseContainer='{ContainerName}', leaseDatabase='{DatabaseName}', functionId='{this._functionId}'";
            _scaleMonitor = new CosmosDBScaleMonitor(_functionId, _monitoredContainer.Object, _leasesContainer.Object, ProcessorName, _loggerFactory.CreateLogger<CosmosDBScaleMonitorTests>());
        }

        [Fact]
        public void ScaleMonitorDescriptor_ReturnsExpectedValue()
        {
            Assert.Equal($"{_functionId}-cosmosdbtrigger-{DatabaseName}-{ContainerName}".ToLower(), _scaleMonitor.Descriptor.Id);
        }

        [Fact]
        public void GetScaleStatus_NoMetrics_ReturnsVote_None()
        {
            var context = new ScaleStatusContext<CosmosDBTriggerMetrics>
            {
                WorkerCount = 1
            };

            var status = _scaleMonitor.GetScaleStatus(context);
            Assert.Equal(ScaleVote.None, status.Vote);

            // verify the non-generic implementation works properly
            status = ((CosmosDBScaleMonitor)_scaleMonitor).GetScaleStatus(context);
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

            var status = ((CosmosDBScaleMonitor)_scaleMonitor).GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleIn, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("WorkerCount (2) > PartitionCount (1).", log.FormattedMessage);
            log = logs[1];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal($"Number of instances (2) is too high relative to number of partitions for container ({ContainerName}, 1).", log.FormattedMessage);
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

            var status = ((CosmosDBScaleMonitor)_scaleMonitor).GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleOut, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal("RemainingWork (2900) > WorkerCount (1) * 1,000.", log.FormattedMessage);
            log = logs[1];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal($"Remaining work for container ({ContainerName}, 2900) is too high relative to the number of instances (1).", log.FormattedMessage);
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

            var status = ((CosmosDBScaleMonitor)_scaleMonitor).GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleOut, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal($"CosmosDB container '{ContainerName}' has documents waiting to be processed.", log.FormattedMessage);
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

            var status = ((CosmosDBScaleMonitor)_scaleMonitor).GetScaleStatus(context);
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

            var status = ((CosmosDBScaleMonitor)_scaleMonitor).GetScaleStatus(context);
            Assert.Equal(ScaleVote.ScaleIn, status.Vote);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            var log = logs[0];
            Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Information, log.Level);
            Assert.Equal($"Remaining work is decreasing for '{ContainerName}'.", log.FormattedMessage);
        }
    }
}
