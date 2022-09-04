// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBItemValueBinderTests
    {
        private const string DatabaseName = "ItemDb";
        private const string CollectionName = "ItemCollection";
        private const string Id = "abc123";

        [Fact]
        public async Task GetValueAsync_JObject_QueriesItem_WithPartitionKey()
        {
            // Arrange           
            string partitionKey = "partitionKey";
            string partitionKeyValue = string.Format("[\"{0}\"]", partitionKey);
            IValueBinder binder = CreateBinder<Item>(out Mock<CosmosClient> mockService, partitionKey);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
            mockResponse
                .Setup(m => m.Resource)
                .Returns(new Item());

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(i => i == Id), It.Is<PartitionKey>(r => r.ToString() == partitionKeyValue), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Act
            var value = (await binder.GetValueAsync()) as Item;

            // Assert
            mockService.VerifyAll();
            Assert.NotNull(value);
        }

        [Fact]
        public async Task GetValueAsync_JObject_QueriesItem()
        {
            // Arrange            
            IValueBinder binder = CreateBinder<Item>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
            mockResponse
                .Setup(m => m.Resource)
                .Returns(new Item());

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(i => i == Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Act
            var value = (await binder.GetValueAsync()) as Item;

            // Assert
            mockService.VerifyAll();
            Assert.NotNull(value);
        }

        [Fact]
        public async Task GetValueAsync_DoesNotThrow_WhenResponseIsNotFound()
        {
            // Arrange
            IValueBinder binder = CreateBinder<Item>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(i => i == Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            // Act
            var value = await binder.GetValueAsync();

            // Assert
            Assert.Null(value);
            mockService.VerifyAll();
        }

        [Fact]
        public async Task GetValueAsync_Throws_WhenErrorResponse()
        {
            // Arrange
            IValueBinder binder = CreateBinder<Item>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(i => i == Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable));

            // Act
            // TODO: Fix this up so it exposes the real exception
            var ex = await Assert.ThrowsAsync<CosmosException>(() => binder.GetValueAsync());

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.StatusCode);
        }

        [Theory]
        [InlineData("ID", false)]
        [InlineData("Id", false)]
        [InlineData("iD", false)]
        [InlineData("id", true)]
        public void TryGetId_CaseSensitive(string idKey, bool expected)
        {
            // Arrange
            var token = JObject.Parse(string.Format("{{{0}:'abc123'}}", idKey));

            // Act
            bool result = CosmosDBItemValueBinder<object>.TryGetId(token, out string id);

            // Assert
            Assert.Equal(expected, result);
            if (result)
            {
                Assert.Equal("abc123", id);
            }
            else
            {
                Assert.Null(id);
            }
        }

        [Fact]
        public async Task SetAsync_Updates_IfPropertyChanges()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);

            Item original = new Item
            {
                Id = "abc123",
                Text = "hello"
            };

            Item updated = new Item
            {
                Id = "abc123",
                Text = "goodbye"
            };

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
            mockResponse
                .Setup(m => m.Resource)
                .Returns(new Item());

            mockContainer
                .Setup(m => m.ReplaceItemAsync<Item>(It.Is<Item>(it => it.Text == updated.Text), It.Is<string>(i => i == updated.Id), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);


            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id
            };

            var context = new CosmosDBContext
            {
                Service = mockService.Object,
                ResolvedAttribute = attribute
            };

            JObject clonedOrig = CosmosDBItemValueBinder<object>.CloneItem(original);

            // Act
            await CosmosDBItemValueBinder<Item>.SetValueInternalAsync(clonedOrig, updated, context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_Poco_SkipsUpdate_IfSame()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);

            Item original = new Item
            {
                Id = "abc123",
                Text = "hello"
            };

            Item updated = new Item
            {
                Id = "abc123",
                Text = "hello"
            };

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id
            };

            var context = new CosmosDBContext
            {
                Service = mockService.Object,
                ResolvedAttribute = attribute
            };

            JObject clonedOrig = CosmosDBItemValueBinder<object>.CloneItem(original);

            // Act
            await CosmosDBItemValueBinder<Item>.SetValueInternalAsync(clonedOrig, updated, context);

            // Assert

            // nothing on the client should be called
            mockService.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_Poco_Throws_IfIdChanges()
        {
            // Arrange
            var mockService = new Mock<CosmosClient>(MockBehavior.Strict);

            Item original = new Item
            {
                Id = "abc123",
            };

            Item updated = new Item
            {
                Id = "def456",
            };

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id
            };

            var context = new CosmosDBContext
            {
                Service = mockService.Object,
                ResolvedAttribute = attribute
            };

            var originalJson = JObject.FromObject(original);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => CosmosDBItemValueBinder<Item>.SetValueInternalAsync(originalJson, updated, context));

            // Assert
            Assert.Equal("Cannot update the 'id' property.", ex.Message);
            mockService.Verify();
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_Poco()
        {
            Item newDocument = new Item
            {
                Id = Guid.NewGuid().ToString()
            };
            newDocument.Text = "some text";

            await TestGetThenSet<Item, Item>(newDocument);
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_JObject()
        {
            JObject newDocument = new JObject();
            newDocument["id"] = Guid.NewGuid().ToString();
            newDocument["Text"] = "some text";

            await TestGetThenSet<JObject, JObject>(newDocument);
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_String()
        {
            JObject newDocument = new JObject();
            newDocument["id"] = Guid.NewGuid().ToString();
            newDocument["Text"] = "some text";
            await TestGetThenSet<string, JObject>(newDocument);
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_Dynamic()
        {
            await TestGetThenSet<dynamic, dynamic>(new { id = Guid.NewGuid().ToString(), Text = "some text" });
        }

        private async Task TestGetThenSet<T, TToRead>(TToRead newDocument)
            where T : class
        {
            // Arrange
            IValueBinder binder = CreateBinder<T>(out Mock<CosmosClient> mockService);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            mockService
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            Mock<ItemResponse<TToRead>> mockResponse = new Mock<ItemResponse<TToRead>>();
            mockResponse
                .Setup(m => m.Resource)
                .Returns(newDocument);

            mockContainer
                .Setup(m => m.ReadItemAsync<TToRead>(It.Is<string>(i => i == Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockResponse.Object);

            // Act
            // get, then immediately set with no changes
            var value = await binder.GetValueAsync() as T;
            await binder.SetValueAsync(value, CancellationToken.None);

            // Assert
            // There should be no call to ReplaceDocumentAsync
            mockService.Verify();
        }

        private static CosmosDBItemValueBinder<T> CreateBinder<T>(out Mock<CosmosClient> mockService, string partitionKey = null)
            where T : class
        {
            mockService = new Mock<CosmosClient>(MockBehavior.Strict);

            CosmosDBAttribute attribute = new CosmosDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id,
                PartitionKey = partitionKey
            };

            var context = new CosmosDBContext
            {
                ResolvedAttribute = attribute,
                Service = mockService.Object
            };

            return new CosmosDBItemValueBinder<T>(context);
        }
    }
}