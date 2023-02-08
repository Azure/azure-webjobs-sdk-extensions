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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests.Trigger
{
    public class CosmosDBMetricsProviderTests
    {
        private static readonly string DatabaseName = "testDb";
        private static readonly string ContainerName = "testContainer";
        private static readonly string ProcessorName = "theProcessor";

        private readonly ILoggerFactory _loggerFactory;
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private readonly Mock<FeedIterator<ChangeFeedProcessorState>> _estimatorIterator;
        private readonly Mock<Container> _monitoredContainer;
        private readonly Mock<Container> _leasesContainer;
        private readonly CosmosDBMetricsProvider _cosmosDbMetricsProvider;

        public CosmosDBMetricsProviderTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);

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

            _cosmosDbMetricsProvider = new CosmosDBMetricsProvider(_loggerFactory.CreateLogger<CosmosDBMetricsProviderTests>(), _monitoredContainer.Object, _leasesContainer.Object, ProcessorName);
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

            var metrics = await _cosmosDbMetricsProvider.GetMetricsAsync();

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

            metrics = await _cosmosDbMetricsProvider.GetMetricsAsync();

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
            metrics = (CosmosDBTriggerMetrics)await _cosmosDbMetricsProvider.GetMetricsAsync();
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

            var metrics = (CosmosDBTriggerMetrics)await _cosmosDbMetricsProvider.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            var warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("Please check that the CosmosDB container and leases container exist and are listed correctly in Functions config files.", warning.FormattedMessage);
            _loggerProvider.ClearAllLogMessages();

            await Assert.ThrowsAsync<InvalidOperationException>(async () => await _cosmosDbMetricsProvider.GetMetricsAsync());

            warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("Unable to handle System.InvalidOperationException: Unknown", warning.FormattedMessage);
            _loggerProvider.ClearAllLogMessages();

            metrics = (CosmosDBTriggerMetrics)await _cosmosDbMetricsProvider.GetMetricsAsync();

            Assert.Equal(0, metrics.PartitionCount);
            Assert.Equal(0, metrics.RemainingWork);
            Assert.NotEqual(default(DateTime), metrics.Timestamp);

            warning = _loggerProvider.GetAllLogMessages().Single(p => p.Level == Microsoft.Extensions.Logging.LogLevel.Warning);
            Assert.Equal("CosmosDBTrigger Exception message: Uh oh again.", warning.FormattedMessage);
        }
    }
}
