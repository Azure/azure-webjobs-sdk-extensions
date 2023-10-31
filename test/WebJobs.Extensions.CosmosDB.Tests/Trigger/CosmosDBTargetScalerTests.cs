// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests.Trigger
{
    public class CosmosDBTargetScalerTests
    {
        private static readonly string DatabaseName = "testDb";
        private static readonly string ContainerName = "testContainer";
        private static readonly string ProcessorName = "theProcessor";

        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();
        private readonly ILoggerFactory _loggerFactory;
        private readonly Mock<Container> _monitoredContainer;
        private readonly Mock<Container> _leasesContainer;
        private readonly Mock<FeedIterator<ChangeFeedProcessorState>> _estimatorIterator;
        private readonly CosmosDBTriggerListener<dynamic> _listener;
        private readonly string _functionId;
        private readonly string _logDetails;
        private readonly CosmosDBTargetScaler _targetScaler;

        private CosmosDBTriggerAttribute _attribute;

        public CosmosDBTargetScalerTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(_loggerProvider);

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

            _attribute = new CosmosDBTriggerAttribute(DatabaseName, ContainerName);
            _targetScaler = new CosmosDBTargetScaler(_functionId, _attribute.MaxItemsPerInvocation, _monitoredContainer.Object, _leasesContainer.Object, ProcessorName, _loggerFactory.CreateLogger<CosmosDBTriggerListener<dynamic>>());
        }

        [Theory]
        [InlineData(null, 200, 2, 2)]
        [InlineData(null, 300, 2, 2)]
        [InlineData(null, 300, 0, 3)]
        [InlineData(null, 0, 3, 0)]
        [InlineData(50, 200, 0, 4)]
        [InlineData(-50, 200, 0, 2)]
        [InlineData(1, 2147483650, 1, 1)]
        [InlineData(1, 2147483650, 2147483647, 2147483647)]
        [InlineData(2, 2147483650, 1073741825, 1073741825)]
        public void GetScaleResultInternal(int? concurrency, long remainingWork, int partitionCount, int expectedTargetWorkerCount)
        {
            TargetScalerContext targetScalerContext = new TargetScalerContext
            {
                InstanceConcurrency = concurrency,
            };

            TargetScalerResult result = _targetScaler.GetScaleResultInternal(targetScalerContext, remainingWork, partitionCount);

            Assert.Equal(expectedTargetWorkerCount, result.TargetWorkerCount);
        }

        [Fact]
        public async Task GetScaleResultAsync()
        {
            TargetScalerContext targetScalerContext = new TargetScalerContext { };

            _estimatorIterator
                            .SetupSequence(m => m.HasMoreResults)
                            .Returns(true)
                            .Returns(false);

            Mock<FeedResponse<ChangeFeedProcessorState>> response = new Mock<FeedResponse<ChangeFeedProcessorState>>();
            response
                .Setup(m => m.GetEnumerator())
                .Returns(new List<ChangeFeedProcessorState>()
                {
                    new ChangeFeedProcessorState("a", 100, string.Empty),
                    new ChangeFeedProcessorState("b", 100, string.Empty),
                    new ChangeFeedProcessorState("c", 50, string.Empty),
                    new ChangeFeedProcessorState("d", 100, string.Empty)
                }.GetEnumerator());

            _estimatorIterator
                .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response.Object));

            TargetScalerResult result = await _targetScaler.GetScaleResultAsync(targetScalerContext);
            Assert.Equal(4, result.TargetWorkerCount);
        }
    }
}
