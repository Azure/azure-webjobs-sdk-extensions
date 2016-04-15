// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBOutputBindingProviderTests
    {
        private const string DatabaseName = "TestDatabase";
        private const string CollectionName = "TestCollection";
        private readonly Uri databaseUri = UriFactory.CreateDatabaseUri(DatabaseName);

        [Theory]
        [InlineData(typeof(Item), true, true)]
        [InlineData(typeof(JObject), true, true)]
        [InlineData(typeof(Item[]), true, true)]
        [InlineData(typeof(JObject[]), true, true)]
        [InlineData(typeof(Item), false, false)]
        [InlineData(typeof(JObject), false, false)]
        [InlineData(typeof(Item[]), false, false)]
        [InlineData(typeof(object), true, true)]
        [InlineData(typeof(object), false, false)]
        [InlineData(typeof(ICollector<Document>), false, true)]
        [InlineData(typeof(IAsyncCollector<JObject>), false, true)]
        [InlineData(typeof(ICollector<Item>), true, true)]
        public void IsTypeValid_ValidatesCorrectly(Type parameterType, bool isOutParameter, bool expected)
        {
            // Arrange
            Type typeToTest = isOutParameter ? parameterType.MakeByRefType() : parameterType;

            // Act
            bool result = DocumentDBOutputBindingProvider.IsTypeValid(typeToTest);

            // Assert
            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task CreateIfNotExists_DoesNotCreate_IfFalse()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);
            var config = new DocumentDBConfiguration
            {
                ConnectionString = "AccountEndpoint=http://someuri;AccountKey=some_key",
                DocumentDBServiceFactory = new TestDocumentDBServiceFactory(mockService.Object)
            };
            var attribute = new DocumentDBAttribute { CreateIfNotExists = false };
            var provider = new DocumentDBAttributeBindingProvider(new JobHostConfiguration(), config, new TestTraceWriter());

            // Act
            await provider.TryCreateAsync(new BindingProviderContext(DocumentDBTestUtility.GetCreateIfNotExistsParameters().First(), null, CancellationToken.None));

            // Assert
            // Nothing to assert. Since the service was null, it was never called.
        }

        [Theory]
        [InlineData(null, 100)]
        [InlineData("partitionKeyPath", 1000)]
        public async Task CreateIfNotExists_Creates_IfTrue(string partitionKeyPath, int collectionThroughput)
        {
            // Arrange
            DocumentDBContext context = null;
            var mockService = InitializeMockService(partitionKeyPath, collectionThroughput, out context);

            var expectedPaths = new List<string>();
            if (!string.IsNullOrEmpty(partitionKeyPath))
            {
                expectedPaths.Add(partitionKeyPath);
            }

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri,
                    It.Is<DocumentCollection>(d => d.Id == CollectionName && Enumerable.SequenceEqual(d.PartitionKey.Paths, expectedPaths)),
                    It.Is<RequestOptions>(r => r.OfferThroughput == collectionThroughput)))
                .ReturnsAsync(new DocumentCollection());

            var provider = new DocumentDBOutputBindingProvider(context, null);

            // Act
            await provider.TryCreateAsync(new BindingProviderContext(DocumentDBTestUtility.GetCreateIfNotExistsParameters().Last(), null, CancellationToken.None));

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExists_DoesNotSetThroughput_IfZero()
        {
            // Arrange
            string partitionKeyPath = "partitionKey";
            DocumentDBContext context = null;
            var mockService = InitializeMockService(partitionKeyPath, 0, out context);

            var expectedPaths = new List<string>();
            if (!string.IsNullOrEmpty(partitionKeyPath))
            {
                expectedPaths.Add(partitionKeyPath);
            }

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri,
                    It.Is<DocumentCollection>(d => d.Id == CollectionName && Enumerable.SequenceEqual(d.PartitionKey.Paths, expectedPaths)),
                    null))
                .ReturnsAsync(new DocumentCollection());

            var provider = new DocumentDBOutputBindingProvider(context, null);

            // Act
            await provider.TryCreateAsync(new BindingProviderContext(DocumentDBTestUtility.GetCreateIfNotExistsParameters().Last(), null, CancellationToken.None));

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionDoNotExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ReturnsAsync(new Database());

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == CollectionName), null))
                .ReturnsAsync(new DocumentCollection());

            // Act
            await DocumentDBOutputBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName, null, 0);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfCollectionDoesNotExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict));

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == CollectionName), null))
                .ReturnsAsync(new DocumentCollection());

            // Act
            await DocumentDBOutputBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName, null, 0);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Succeeds_IfDbAndCollectionExist()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict));

            mockService
                .Setup(m => m.CreateDocumentCollectionAsync(databaseUri, It.Is<DocumentCollection>(d => d.Id == CollectionName), null))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.Conflict));

            // Act
            await DocumentDBOutputBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName, null, 0);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task CreateIfNotExist_Throws_IfExceptionIsNotConflict()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.BadRequest));

            // Act
            await Assert.ThrowsAsync<DocumentClientException>(
                () => DocumentDBOutputBindingProvider.CreateIfNotExistAsync(mockService.Object, DatabaseName, CollectionName, null, 0));

            // Assert            
            mockService.VerifyAll();
        }

        private Mock<IDocumentDBService> InitializeMockService(string partitionKey, int throughput, out DocumentDBContext context)
        {
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            context = new DocumentDBContext
            {
                ResolvedDatabaseName = DatabaseName,
                ResolvedCollectionName = CollectionName,
                Service = new TestDocumentDBServiceFactory(mockService.Object).CreateService("AccountEndpoint=http://someuri;AccountKey=some_key"),
                ResolvedPartitionKey = partitionKey,
                CreateIfNotExists = true,
                CollectionThroughput = throughput
            };

            mockService
                .Setup(m => m.CreateDatabaseAsync(It.Is<Database>(d => d.Id == DatabaseName)))
                .ReturnsAsync(new Database());

            return mockService;
        }
    }
}
