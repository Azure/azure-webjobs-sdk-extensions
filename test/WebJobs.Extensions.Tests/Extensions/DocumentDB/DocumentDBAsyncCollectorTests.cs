// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBAsyncCollectorTests
    {
        private const string DatabaseName = "ItemDB";
        private const string CollectionName = "ItemCollection";
        private readonly Uri databaseUri = UriFactory.CreateDatabaseUri(DatabaseName);

        [Fact]
        public async Task AddAsync_CreatesDocument()
        {
            // Arrange
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockDocDBService
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .ReturnsAsync(new Document());

            var context = CreateContext(mockDocDBService.Object);
            var collector = new DocumentDBAsyncCollector<Item>(context);

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockDocDBService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_Throws_IfExceptionIsNotTooManyRequests()
        {
            // Arrange
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockDocDBService
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            var context = CreateContext(mockDocDBService.Object);
            var collector = new DocumentDBAsyncCollector<Item>(context);

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(() => collector.AddAsync(new Item { Text = "hello!" }));

            // Assert
            mockDocDBService.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_Retries_IfThrottled()
        {
            // Arrange
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var context = CreateContext(mockDocDBService.Object);
            var collector = new DocumentDBAsyncCollector<Item>(context);

            mockDocDBService
                    .SetupSequence(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429))
                    .Returns(Task.FromResult(new Document()));

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockDocDBService.Verify(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()), Times.Exactly(4));
        }

        [Fact]
        public async Task AddAsync_DoesNotCreate_IfUpsertSucceeds()
        {
            // Arrange
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var context = CreateContext(mockDocDBService.Object);
            context.ResolvedAttribute.CreateIfNotExists = true;
            var collector = new DocumentDBAsyncCollector<Item>(context);

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
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var context = CreateContext(mockDocDBService.Object);
            context.ResolvedAttribute.CreateIfNotExists = false;
            var collector = new DocumentDBAsyncCollector<Item>(context);

            mockDocDBService
                    .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

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
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = CreateContext(mockService.Object, partitionKeyPath: partitionKeyPath,
                throughput: collectionThroughput, createIfNotExists: true);
            var collector = new DocumentDBAsyncCollector<Item>(context);

            mockService
                    .SetupSequence(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound))
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
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            SetupCollectionMock(mockService, partitionKeyPath);

            // Act
            await DocumentDBAsyncCollector<Item>.CreateDocumentCollectionIfNotExistsAsync(mockService.Object, DatabaseName,
                CollectionName, partitionKeyPath, 0);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionDoNotExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = CreateContext(mockService.Object);
            SetupDatabaseMock(mockService);
            SetupCollectionMock(mockService);

            // Act
            await DocumentDBAsyncCollector<Item>.CreateIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfCollectionDoesNotExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = CreateContext(mockService.Object);

            SetupDatabaseMock(mockService, databaseExists: true);
            SetupCollectionMock(mockService);

            // Act
            await DocumentDBAsyncCollector<Item>.CreateIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionExist()
        {
            // Arrange            
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = CreateContext(mockService.Object);
            SetupDatabaseMock(mockService, databaseExists: true);
            SetupCollectionMock(mockService, collectionExists: true);

            // Act
            await DocumentDBAsyncCollector<Item>.CreateIfNotExistAsync(context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Throws_IfExceptionIsNotConflict()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            DocumentDBContext context = CreateContext(mockService.Object);
            SetupDatabaseMock(mockService);

            // overwrite the default setup with one that throws
            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(
                () => DocumentDBAsyncCollector<Item>.CreateIfNotExistAsync(context));

            // Assert            
            mockService.VerifyAll();
        }
        private void SetupCollectionMock(Mock<IDocumentDBService> mockService, string partitionKeyPath = null,
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

        private void SetupDatabaseMock(Mock<IDocumentDBService> mockService, bool databaseExists = false)
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

        private static DocumentDBContext CreateContext(IDocumentDBService service, bool createIfNotExists = false,
            string partitionKeyPath = null, int throughput = 0)
        {
            DocumentDBAttribute attribute = new DocumentDBAttribute(DatabaseName, CollectionName)
            {
                CreateIfNotExists = createIfNotExists,
                PartitionKey = partitionKeyPath,
                CollectionThroughput = throughput
            };

            return new DocumentDBContext
            {
                Service = service,
                ResolvedAttribute = attribute
            };
        }
    }
}
