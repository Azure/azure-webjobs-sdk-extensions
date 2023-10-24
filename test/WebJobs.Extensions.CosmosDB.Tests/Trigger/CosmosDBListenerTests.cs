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
using Microsoft.Azure.WebJobs.Host;
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
        private readonly string _logDetails;

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

            _listener = new CosmosDBTriggerListener<dynamic>(_mockExecutor.Object, _functionId, ProcessorName, _monitoredContainer.Object, _leasesContainer.Object, attribute, Mock.Of<IDrainModeManager>(), _loggerFactory.CreateLogger<CosmosDBTriggerListener<dynamic>>());

            _logDetails = $"prefix='{ProcessorName}', monitoredContainer='{ContainerName}', monitoredDatabase='{DatabaseName}', " +
                $"leaseContainer='{ContainerName}', leaseDatabase='{DatabaseName}', functionId='{this._functionId}'";
        }

        [Fact]
        public async Task StartAsync_Retries()
        {
            var attribute = new CosmosDBTriggerAttribute("test", "test") { LeaseContainerPrefix = ProcessorName };
           
            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();

            var listener = new MockListener<dynamic>(mockExecutor.Object, _functionId, ProcessorName, _monitoredContainer.Object, _leasesContainer.Object, attribute, Mock.Of<IDrainModeManager>(), _loggerFactory.CreateLogger<CosmosDBTriggerListener<dynamic>>());

            // Ensure that we can call StartAsync() multiple times to retry if there is an error.
            for (int i = 0; i < 3; i++)
            {
                var ex = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => listener.StartAsync(CancellationToken.None));
                Assert.Equal("Failed to register!", ex.Message);
            }

            // This should succeed
            await listener.StartAsync(CancellationToken.None);

            var logs = _loggerProvider.GetAllLogMessages().ToArray();
            Assert.Equal(LogLevel.Error, logs[0].Level);
            Assert.Equal(Events.OnListenerStartError, logs[0].EventId);
            Assert.Contains(_logDetails, logs[0].FormattedMessage);
            Assert.Equal(LogLevel.Error, logs[1].Level);
            Assert.Equal(Events.OnListenerStartError, logs[1].EventId);
            Assert.Contains(_logDetails, logs[1].FormattedMessage);
            Assert.Equal(LogLevel.Error, logs[2].Level);
            Assert.Equal(Events.OnListenerStartError, logs[2].EventId);
            Assert.Contains(_logDetails, logs[2].FormattedMessage);
            Assert.Equal(LogLevel.Debug, logs[3].Level);
            Assert.Equal(Events.OnListenerStarted, logs[3].EventId);
            Assert.Contains(_logDetails, logs[3].FormattedMessage);
        }

        private class MockListener<T> : CosmosDBTriggerListener<T>
        {
            private int _retries = 0;

            public MockListener(ITriggeredFunctionExecutor executor,
                string functionId,
                string processorName,
                Container monitoredContainer,
                Container leaseContainer,
                CosmosDBTriggerAttribute cosmosDBAttribute,
                IDrainModeManager drainModeManager,
                ILogger logger)
                : base(executor, functionId, processorName, monitoredContainer, leaseContainer, cosmosDBAttribute, drainModeManager, logger)
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