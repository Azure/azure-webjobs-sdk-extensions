// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBEnumerableBuilderTests
    {
        private const string DatabaseName = "ItemDb";
        private const string CollectionName = "ItemCollection";
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

        [Fact]
        public async Task ConvertAsync_Succeeds_NoContinuation()
        {
            var builder = CreateBuilder<Item>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<FeedIterator<Item>> mockIterator = new Mock<FeedIterator<Item>>();
            mockContainer
                .Setup(m => m.GetItemQueryIterator<Item>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
                .Returns(mockIterator.Object);

            mockIterator
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            Mock<FeedResponse<Item>> mockResponse = new Mock<FeedResponse<Item>>();

            mockIterator
                .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            mockResponse
                .Setup(m => m.Resource)
                .Returns(GetDocumentCollection(5));

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                SqlQuery = string.Empty
            };

            var results = await builder.ConvertAsync(attribute, CancellationToken.None);
            Assert.Equal(5, results.Count());
        }

        [Fact]
        public async Task ConvertAsync_Succeeds_NoContinuation_WithPartitionKey()
        {
            var partitionKey = Guid.NewGuid().ToString();
            var builder = CreateBuilder<Item>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<FeedIterator<Item>> mockIterator = new Mock<FeedIterator<Item>>();
            mockContainer
                .Setup(m => m.GetItemQueryIterator<Item>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.Is<QueryRequestOptions>(ro => ro.PartitionKey == new PartitionKey(partitionKey))))
                .Returns(mockIterator.Object);

            mockIterator
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(false);

            Mock<FeedResponse<Item>> mockResponse = new Mock<FeedResponse<Item>>();

            mockIterator
                .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            mockResponse
                .Setup(m => m.Resource)
                .Returns(GetDocumentCollection(5));

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                SqlQuery = string.Empty,
                PartitionKey = partitionKey
            };

            var results = await builder.ConvertAsync(attribute, CancellationToken.None);
            Assert.Equal(5, results.Count());
        }

        [Fact]
        public async Task ConvertAsync_Succeeds_WithContinuation()
        {
            var builder = CreateBuilder<Item>(out Mock<CosmosClient> mockService);
            var docCollection = GetDocumentCollection(17);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<FeedIterator<Item>> mockIterator = new Mock<FeedIterator<Item>>();
            mockContainer
                .Setup(m => m.GetItemQueryIterator<Item>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
                .Returns(mockIterator.Object);

            mockIterator
                .SetupSequence(m => m.HasMoreResults)
                .Returns(true)
                .Returns(true)
                .Returns(true)
                .Returns(true)
                .Returns(false);

            Mock<FeedResponse<Item>> mockResponse = new Mock<FeedResponse<Item>>();

            mockIterator
                .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            mockResponse
                .SetupSequence(m => m.Resource)
                .Returns(docCollection.Take(5))
                .Returns(docCollection.Skip(5).Take(5))
                .Returns(docCollection.Skip(10).Take(5))
                .Returns(docCollection.Skip(15).Take(2));

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                SqlQuery = "SELECT * FROM c"
            };

            var results = await builder.ConvertAsync(attribute, CancellationToken.None);
            Assert.Equal(17, results.Count());

            mockIterator.Verify(m => m.ReadNextAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
        }

        [Fact]
        public async Task ConvertAsync_RethrowsException_IfNotFound()
        {
            var builder = CreateBuilder<Item>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<FeedIterator<Item>> mockIterator = new Mock<FeedIterator<Item>>();
            mockContainer
                .Setup(m => m.GetItemQueryIterator<Item>(It.IsAny<QueryDefinition>(), It.IsAny<string>(), It.IsAny<QueryRequestOptions>()))
                .Returns(mockIterator.Object);

            mockIterator
               .Setup(m => m.HasMoreResults)
               .Returns(true);

            Mock<FeedResponse<Item>> mockResponse = new Mock<FeedResponse<Item>>();

            mockIterator
                .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                SqlQuery = string.Empty
            };

            var ex = await Assert.ThrowsAsync<CosmosException>(() => builder.ConvertAsync(attribute, CancellationToken.None));
            Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);

            mockIterator.Verify(m => m.ReadNextAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        private static IEnumerable<Item> GetDocumentCollection(int count)
        {
            List<Item> items = new List<Item>();
            for (int i = 0; i < count; i++)
            {
                var doc = new Item { Id = i.ToString() };
                doc.Text = $"Item {i}";

                items.Add(doc);
            }
            return items;
        }

        private static CosmosDBEnumerableBuilder<T> CreateBuilder<T>(out Mock<CosmosClient> mockService)
            where T : class
        {
            mockService = new Mock<CosmosClient>(MockBehavior.Strict);
            Mock<ICosmosDBServiceFactory> mockServiceFactory = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);

            mockServiceFactory
                .Setup(m => m.CreateService(It.IsAny<string>(), It.IsAny<CosmosClientOptions>()))
                .Returns(mockService.Object);

            var options = new OptionsWrapper<CosmosDBOptions>(new CosmosDBOptions
            {
                ConnectionString = "AccountEndpoint=https://someuri;AccountKey=c29tZV9rZXk=;"
            });
            var configProvider = new CosmosDBExtensionConfigProvider(options, mockServiceFactory.Object, new DefaultCosmosDBSerializerFactory(), _emptyConfig, new TestNameResolver(), NullLoggerFactory.Instance);

            return new CosmosDBEnumerableBuilder<T>(configProvider);
        }
    }
}