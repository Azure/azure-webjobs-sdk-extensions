// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB
{
    public class CosmosDBAsyncCollectorTests
    {
        [Fact]
        public async Task AddAsync_CreatesDocument()
        {
            // Arrange
            var mockDocDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);

            mockDocDBService
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .ReturnsAsync(new Document());

            var context = CosmosDBTestUtility.CreateContext(mockDocDBService.Object);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockDocDBService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_Throws_IfExceptionIsNotTooManyRequests()
        {
            // Arrange
            var mockDocDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);

            mockDocDBService
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            var context = CosmosDBTestUtility.CreateContext(mockDocDBService.Object);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(() => collector.AddAsync(new Item { Text = "hello!" }));

            // Assert
            mockDocDBService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_DoesNotCreate_IfUpsertSucceeds()
        {
            // Arrange
            var mockDocDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockDocDBService.Object);
            context.ResolvedAttribute.CreateIfNotExists = true;
            var collector = new CosmosDBAsyncCollector<Item>(context);

            mockDocDBService
                    .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .Returns(Task.FromResult(new Document()));

            //// Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockDocDBService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_DoesNotCreate_IfFalse()
        {
            // Arrange
            var mockDocDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            var context = CosmosDBTestUtility.CreateContext(mockDocDBService.Object);
            context.ResolvedAttribute.CreateIfNotExists = false;
            var collector = new CosmosDBAsyncCollector<Item>(context);

            mockDocDBService
                    .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            //// Act
            await Assert.ThrowsAsync<DocumentClientException>(() => collector.AddAsync(new Item { Text = "hello!" }));

            // Assert
            mockDocDBService.VerifyAll();
        }

        [Theory]
        [InlineData(null, 100)]
        [InlineData("partitionKeyPath", 1000)]
        public async Task AddAsync_Creates_IfTrue_AndNotFound(string partitionKeyPath, int collectionThroughput)
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CosmosDBTestUtility.CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath,
                throughput: collectionThroughput, createIfNotExists: true);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            mockService
                    .SetupSequence(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .Throws(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound))
                    .Returns(Task.FromResult(new Document()));

            CosmosDBTestUtility.SetupDatabaseMock(mockService);
            CosmosDBTestUtility.SetupCollectionMock(mockService, partitionKeyPath, collectionThroughput);

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockService.VerifyAll();

            // Verify that we upsert again after creation.
            mockService.Verify(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()), Times.Exactly(2));
        }
    }
}
