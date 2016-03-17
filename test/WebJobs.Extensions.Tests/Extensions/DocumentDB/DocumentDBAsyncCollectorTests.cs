// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

// We are using this alias because the test assembly references both EasyTables and DocumentDB, which share
// the TypeUtility. This results in a type name collision. This is only required for the test assembly.
extern alias DocumentDB;

using System;
using System.Net;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBAsyncCollectorTests
    {
        private const string DatabaseName = "ItemDB";
        private const string CollectionName = "ItemCollection";

        [Fact]
        public async Task AddAsync_CreatesDocument()
        {
            // Arrange
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockDocDBService
                .Setup(m => m.CreateDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
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
                .Setup(m => m.CreateDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
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
            var databases = new Database[] { new Database { Id = DatabaseName } };
            var collections = new DocumentCollection[] { new DocumentCollection { Id = CollectionName } };
            var mockDocDBService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var context = CreateContext(mockDocDBService.Object);
            var collector = new DocumentDBAsyncCollector<Item>(context);

            mockDocDBService
                    .SetupSequence(m => m.CreateDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429))
                    .Throws(DocumentDBTestUtility.CreateDocumentClientException((HttpStatusCode)429))
                    .Returns(Task.FromResult(new Document()));

            // Act
            await collector.AddAsync(new Item { Text = "hello!" });

            // Assert
            mockDocDBService.VerifyAll();
        }

        private static DocumentDBContext CreateContext(IDocumentDBService service)
        {
            return new DocumentDBContext
            {
                Service = service,
                ResolvedDatabaseName = DatabaseName,
                ResolvedCollectionName = CollectionName
            };
        }
    }
}
