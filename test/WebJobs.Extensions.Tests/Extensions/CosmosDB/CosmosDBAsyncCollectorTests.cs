// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB
{
    public class CosmosDBAsyncCollectorTests
    {
        private const string DatabaseName = "ItemDB";
        private const string CollectionName = "ItemCollection";
        private readonly Uri databaseUri = UriFactory.CreateDatabaseUri(DatabaseName);

        [Fact]
        public async Task AddAsync_CreatesDocument()
        {
            // Arrange
            var mockDocDBService = new Mock<ICosmosDBService>(MockBehavior.Strict);

            mockDocDBService
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .ReturnsAsync(new Document());

            var context = CreateContext(mockDocDBService.Object);
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

            var context = CreateContext(mockDocDBService.Object);
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
            var context = CreateContext(mockDocDBService.Object);
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
            var context = CreateContext(mockDocDBService.Object);
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
            CosmosDBContext context = CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath,
                throughput: collectionThroughput, createIfNotExists: true);
            var collector = new CosmosDBAsyncCollector<Item>(context);

            mockService
                    .SetupSequence(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .Throws(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound))
                    .Returns(Task.FromResult(new Document()));

            SetupDatabaseMock(mockService);
            SetupCollectionMock(mockService, partitionKeyPath, collectionThroughput);

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockService.VerifyAll();

            // Verify that we upsert again after creation.
            mockService.Verify(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()), Times.Exactly(2));
        }

        [Fact]
        public async Task CreateIfNotExists_DoesNotSetThroughput_IfZero()
        {
            // Arrange
            string partitionKeyPath = "partitionKey";
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            SetupCollectionMock(mockService, partitionKeyPath);

            // Act
            await CosmosDBAsyncCollector<Item>.CreateDocumentCollectionIfNotExistsAsync(mockService.Object, DatabaseName,
                CollectionName, partitionKeyPath, 0);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionDoNotExist()
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CreateContext(mockService.Object);
            SetupDatabaseMock(mockService);
            SetupCollectionMock(mockService);

            // Act
            await CosmosDBAsyncCollector<Item>.CreateIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfCollectionDoesNotExist()
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CreateContext(mockService.Object);

            SetupDatabaseMock(mockService, databaseExists: true);
            SetupCollectionMock(mockService);

            // Act
            await CosmosDBAsyncCollector<Item>.CreateIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionExist()
        {
            // Arrange            
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CreateContext(mockService.Object);
            SetupDatabaseMock(mockService, databaseExists: true);
            SetupCollectionMock(mockService, collectionExists: true);

            // Act
            await CosmosDBAsyncCollector<Item>.CreateIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Throws_IfExceptionIsNotConflict()
        {
            // Arrange
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);
            CosmosDBContext context = CreateContext(mockService.Object);
            SetupDatabaseMock(mockService);

            // overwrite the default setup with one that throws
            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(
                () => CosmosDBAsyncCollector<Item>.CreateIfNotExistAsync(context));

            // Assert            
            mockService.VerifyAll();
        }
        private void SetupCollectionMock(Mock<ICosmosDBService> mockService, string partitionKeyPath = null,
            int throughput = 0, bool collectionExists = true)
        {
            var expectedPaths = new List<string>();
            if (!string.IsNullOrEmpty(partitionKeyPath))
            {
                expectedPaths.Add(partitionKeyPath);
            }

            var collections = new List<DocumentCollection>();
            if (collectionExists)
            {
                collections.Add(new DocumentCollection { Id = CollectionName });
            }

            mockService
               .Setup(m => m.CreateDocumentCollectionQuery(databaseUri))
               .Returns(collections.AsOrderedQueryable(c => c.Id));

            if (collectionExists)
            {
                return;
            }

            if (throughput == 0)
            {
                mockService
                    .Setup(m => m.CreateDocumentCollectionAsync(databaseUri,
                        It.Is<DocumentCollection>(d => d.Id == CollectionName && Enumerable.SequenceEqual(d.PartitionKey.Paths, expectedPaths)),
                        null))
                    .ReturnsAsync(new DocumentCollection());
            }
            else
            {
                mockService
                    .Setup(m => m.CreateDocumentCollectionAsync(databaseUri,
                        It.Is<DocumentCollection>(d => d.Id == CollectionName && Enumerable.SequenceEqual(d.PartitionKey.Paths, expectedPaths)),
                        It.Is<RequestOptions>(r => r.OfferThroughput == throughput)))
                    .ReturnsAsync(new DocumentCollection());
            }
        }

        private void SetupDatabaseMock(Mock<ICosmosDBService> mockService, bool databaseExists = false)
        {
            var databases = new List<Database>();
            if (databaseExists)
            {
                databases.Add(new Database { Id = DatabaseName });
            }

            mockService
               .Setup(m => m.CreateDatabaseQuery())
               .Returns(databases.AsOrderedQueryable(d => d.Id));

            if (!databaseExists)
            {
                mockService
                    .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                    .ReturnsAsync(new Database());
            }
        }

        private static CosmosDBContext CreateContext(ICosmosDBService service, bool createIfNotExists = false,
            string partitionKeyPath = null, int throughput = 0)
        {
            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                CreateIfNotExists = createIfNotExists,
                PartitionKey = partitionKeyPath,
                CollectionThroughput = throughput
            };

            return new CosmosDBContext
            {
                Service = service,
                ResolvedAttribute = attribute
            };
        }
    }
}
