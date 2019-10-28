// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBAsyncCollectorTests
    {
        [Fact]
        public async Task AddAsync_CreatesDocument()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == CosmosDBTestUtility.DatabaseName), It.Is<string>(c => c == CosmosDBTestUtility.ContainerName)))
                .Returns(mockContainer.Object);

            var mockResponse = new Mock<ItemResponse<Item>>(MockBehavior.Strict);
            mockContainer
                .Setup(m => m.UpsertItemAsync<Item>(It.IsAny<Item>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            var context = CosmosDBTestUtility.CreateContext(mockService.Object);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_ThrowsWithCustomMessage_IfNotFound()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == CosmosDBTestUtility.DatabaseName), It.Is<string>(c => c == CosmosDBTestUtility.ContainerName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(m => m.UpsertItemAsync<Item>(It.IsAny<Item>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            var context = CosmosDBTestUtility.CreateContext(mockService.Object, createIfNotExists: false);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => collector.AddAsync(new Item { Text = "hello!" }));

            // Assert
            Assert.Contains(CosmosDBTestUtility.ContainerName, ex.Message);
            Assert.Contains(CosmosDBTestUtility.DatabaseName, ex.Message);
            mockService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_DoesNotCreate_IfUpsertSucceeds()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockService.Object);
            context.ResolvedAttribute.CreateIfNotExists = true;
            var collector = new CosmosDBAsyncCollector<Item>(context);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == CosmosDBTestUtility.DatabaseName), It.Is<string>(c => c == CosmosDBTestUtility.ContainerName)))
                .Returns(mockContainer.Object);

            var mockResponse = new Mock<ItemResponse<Item>>(MockBehavior.Strict);
            mockContainer
                    .Setup(m => m.UpsertItemAsync<Item>(It.IsAny<Item>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .ReturnsAsync(mockResponse.Object);

            //// Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockService.VerifyAll();
        }

        [Theory]
        [InlineData(null, 100)]
        [InlineData("partitionKeyPath", 1000)]
        public async Task AddAsync_Creates_IfTrue_AndNotFound(string partitionKeyPath, int collectionThroughput)
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            CosmosDBContext context = CosmosDBTestUtility.CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath,
                throughput: collectionThroughput, createIfNotExists: true);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            Mock<Database> dbMock = CosmosDBTestUtility.SetupDatabaseMock(mockService);
            Mock<Container> mockContainer = CosmosDBTestUtility.SetupCollectionMock(mockService, dbMock, partitionKeyPath, collectionThroughput);
            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == CosmosDBTestUtility.DatabaseName), It.Is<string>(c => c == CosmosDBTestUtility.ContainerName)))
                .Returns(mockContainer.Object);
            var mockResponse = new Mock<ItemResponse<Item>>(MockBehavior.Strict);
            mockContainer
                    .SetupSequence(m => m.UpsertItemAsync<Item>(It.IsAny<Item>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                    .Throws(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound))
                    .ReturnsAsync(mockResponse.Object);

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockService.VerifyAll();

            // Verify that we upsert again after creation.
            mockContainer.Verify(m => m.UpsertItemAsync<Item>(It.IsAny<Item>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
