// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
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
        private readonly Uri _expectedUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, Id);

        [Fact]
        public async Task GetValueAsync_JObject_QueriesItem_WithPartitionKey()
        {
            // Arrange           
            string partitionKey = "partitionKey";
            string partitionKeyValue = string.Format("[\"{0}\"]", partitionKey);
            IValueBinder binder = CreateBinder<Item>(out Mock<ICosmosDBService> mockService, partitionKey);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == partitionKeyValue)))
                .ReturnsAsync(new Document());

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
            IValueBinder binder = CreateBinder<Item>(out Mock<ICosmosDBService> mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, null))
                .ReturnsAsync(new Document());

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
            IValueBinder binder = CreateBinder<Item>(out Mock<ICosmosDBService> mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, null))
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
            IValueBinder binder = CreateBinder<Item>(out Mock<ICosmosDBService> mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, null))
                .ThrowsAsync(CosmosDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable));

            // Act
            // TODO: Fix this up so it exposes the real exception
            var ex = await Assert.ThrowsAsync<DocumentClientException>(() => binder.GetValueAsync());

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
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);

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

            mockService
               .Setup(m => m.ReplaceDocumentAsync(_expectedUri, updated))
               .ReturnsAsync(new Document());

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
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);

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
            var mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);

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
            Assert.Equal("Cannot update the 'Id' property.", ex.Message);
            mockService.Verify();
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_Poco()
        {
            await TestGetThenSet<Item>();
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_JObject()
        {
            await TestGetThenSet<JObject>();
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_Document()
        {
            await TestGetThenSet<Document>();
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_String()
        {
            await TestGetThenSet<string>();
        }

        [Fact]
        public async Task GetAsync_SetAsync_DoesNotUpdate_IfUnchanged_Dynamic()
        {
            await TestGetThenSet<dynamic>();
        }

        private async Task TestGetThenSet<T>()
            where T : class
        {
            // Arrange
            Document newDocument = new Document
            {
                Id = Guid.NewGuid().ToString()
            };
            newDocument.SetPropertyValue("text", "some text");

            IValueBinder binder = CreateBinder<T>(out Mock<ICosmosDBService> mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, null))
                .ReturnsAsync(newDocument);

            // Act
            // get, then immediately set with no changes
            var value = await binder.GetValueAsync() as T;
            await binder.SetValueAsync(value, CancellationToken.None);

            // Assert
            // There should be no call to ReplaceDocumentAsync
            mockService.Verify();
        }

        private static CosmosDBItemValueBinder<T> CreateBinder<T>(out Mock<ICosmosDBService> mockService, string partitionKey = null)
            where T : class
        {
            mockService = new Mock<ICosmosDBService>(MockBehavior.Strict);

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