// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.DocumentDB
{
    public class DocumentDBItemValueBinderTests
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
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(out mockService, partitionKey);
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
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(out mockService);
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
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(out mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, null))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

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
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(out mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync(_expectedUri, null))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable));

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
            string id = null;

            // Act
            bool result = DocumentDBItemValueBinder<object>.TryGetId(token, out id);

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
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

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

            DocumentDBAttribute attribute = new DocumentDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id
            };

            var context = new DocumentDBContext
            {
                Service = mockService.Object,
                ResolvedAttribute = attribute
            };

            JObject clonedOrig = DocumentDBItemValueBinder<object>.CloneItem(original);

            // Act
            await DocumentDBItemValueBinder<Item>.SetValueInternalAsync(clonedOrig, updated, context);

            // Assert
            mockService.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_Poco_SkipsUpdate_IfSame()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

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

            DocumentDBAttribute attribute = new DocumentDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id
            };

            var context = new DocumentDBContext
            {
                Service = mockService.Object,
                ResolvedAttribute = attribute
            };

            JObject clonedOrig = DocumentDBItemValueBinder<object>.CloneItem(original);

            // Act
            await DocumentDBItemValueBinder<Item>.SetValueInternalAsync(clonedOrig, updated, context);

            // Assert

            // nothing on the client should be called
            mockService.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_Poco_Throws_IfIdChanges()
        {
            // Arrange
            var mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            Item original = new Item
            {
                Id = "abc123",
            };

            Item updated = new Item
            {
                Id = "def456",
            };

            DocumentDBAttribute attribute = new DocumentDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id
            };

            var context = new DocumentDBContext
            {
                Service = mockService.Object,
                ResolvedAttribute = attribute
            };

            var originalJson = JObject.FromObject(original);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DocumentDBItemValueBinder<Item>.SetValueInternalAsync(originalJson, updated, context));

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

        private async Task TestGetThenSet<T>() where T : class
        {
            // Arrange
            Document newDocument = new Document();
            newDocument.Id = Guid.NewGuid().ToString();
            newDocument.SetPropertyValue("text", "some text");

            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<T>(out mockService);
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

        private static DocumentDBItemValueBinder<T> CreateBinder<T>(out Mock<IDocumentDBService> mockService, string partitionKey = null) where T : class
        {
            mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            DocumentDBAttribute attribute = new DocumentDBAttribute(DatabaseName, CollectionName)
            {
                Id = Id,
                PartitionKey = partitionKey
            };

            var context = new DocumentDBContext
            {
                ResolvedAttribute = attribute,
                Service = mockService.Object
            };

            return new DocumentDBItemValueBinder<T>(context);
        }
    }
}