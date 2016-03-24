// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
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

        public DocumentDBItemValueBinderTests()
        {
        }

        [Fact]
        public void GetValue_JObject_QueriesItem()
        {
            // Arrange                        
            var parameter = DocumentDBTestUtility.GetInputParameter<Item>();
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(parameter, out mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync<Item>(_expectedUri))
                .ReturnsAsync(new Item());

            // Act
            var value = binder.GetValue();

            // Assert
            mockService.VerifyAll();
            Assert.NotNull(value);
        }

        [Fact]
        public void GetValue_DoesNotThrow_WhenResponseIsNotFound()
        {
            // Arrange
            var parameter = DocumentDBTestUtility.GetInputParameter<Item>();
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(parameter, out mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync<Item>(_expectedUri))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.NotFound));

            // Act
            var value = binder.GetValue();

            // Assert
            Assert.Null(value);
            mockService.VerifyAll();
        }

        [Fact]
        public void GetValue_Throws_WhenErrorResponse()
        {
            // Arrange
            var parameter = DocumentDBTestUtility.GetInputParameter<Item>();
            Mock<IDocumentDBService> mockService;
            IValueBinder binder = CreateBinder<Item>(parameter, out mockService);
            mockService
                .Setup(m => m.ReadDocumentAsync<Item>(_expectedUri))
                .ThrowsAsync(DocumentDBTestUtility.CreateDocumentClientException(HttpStatusCode.ServiceUnavailable));

            // Act
            // TODO: Fix this up so it exposes the real exception
            var ex = Assert.Throws<AggregateException>(() => binder.GetValue());

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ((DocumentClientException)ex.InnerException).StatusCode);
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

            var context = new DocumentDBContext
            {
                Service = mockService.Object,
                ResolvedDatabaseName = DatabaseName,
                ResolvedCollectionName = CollectionName,
                ResolvedId = Id
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

            var context = new DocumentDBContext
            {
                Service = mockService.Object,
                ResolvedDatabaseName = DatabaseName,
                ResolvedCollectionName = CollectionName,
                ResolvedId = Id
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

            var context = new DocumentDBContext
            {
                Service = mockService.Object,
                ResolvedDatabaseName = DatabaseName,
                ResolvedCollectionName = CollectionName,
                ResolvedId = Id
            };

            var originalJson = JObject.FromObject(original);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => DocumentDBItemValueBinder<Item>.SetValueInternalAsync(originalJson, updated, context));

            // Assert
            Assert.Equal("Cannot update the 'Id' property.", ex.Message);
            mockService.Verify();
        }

        private static DocumentDBItemValueBinder<T> CreateBinder<T>(ParameterInfo parameter, out Mock<IDocumentDBService> mockService) where T : class
        {
            mockService = new Mock<IDocumentDBService>(MockBehavior.Strict);

            var context = new DocumentDBContext
            {
                ResolvedDatabaseName = DatabaseName,
                ResolvedCollectionName = CollectionName,
                ResolvedId = Id,
                Service = mockService.Object
            };

            return new DocumentDBItemValueBinder<T>(parameter, context, "abc123");
        }
    }
}