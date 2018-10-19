// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents.ChangeFeedProcessor;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Host.Executors;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests.Trigger
{
    public class CosmosDBListenerTests
    {
        [Fact]
        public async Task StartAsync_Retries()
        {
            var mockExecutor = new Mock<ITriggeredFunctionExecutor>();

            var monitoredCosmosDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);

            monitoredCosmosDBService
                .Setup(m => m.GetClient())
                .Returns(new DocumentClient(new Uri("http://fakeaccount"), "c29tZV9rZXk="));

            var leasesCosmosDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);

            leasesCosmosDBService
                .Setup(m => m.GetClient())
                .Returns(new DocumentClient(new Uri("http://fakeaccount"), "c29tZV9rZXk="));

            var collInfo = new DocumentCollectionInfo { Uri = new Uri("http://fakeaccount"), MasterKey = "c29tZV9rZXk=", DatabaseName = "FakeDb", CollectionName = "FakeColl" };
            var leaseInfo = new DocumentCollectionInfo { Uri = new Uri("http://fakeaccount"), MasterKey = "c29tZV9rZXk=", DatabaseName = "FakeDb", CollectionName = "leases" };
            var processorOptions = new ChangeFeedProcessorOptions();

            var listener = new MockListener(mockExecutor.Object, collInfo, leaseInfo, processorOptions, monitoredCosmosDBService.Object, leasesCosmosDBService.Object, NullLogger.Instance);

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

            public MockListener(ITriggeredFunctionExecutor executor, DocumentCollectionInfo documentCollectionLocation, DocumentCollectionInfo leaseCollectionLocation, ChangeFeedProcessorOptions processorOptions, ICosmosDBService monitoredCosmosDBService, ICosmosDBService leasesCosmosDBService, ILogger logger)
                : base(executor, documentCollectionLocation, leaseCollectionLocation, processorOptions, monitoredCosmosDBService, leasesCosmosDBService, logger)
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