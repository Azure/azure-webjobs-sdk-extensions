// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileTableItemValueBinderTests
    {
        [Fact]
        public async Task GetValueAsync_JObject_QueriesItem()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetInputParameter<JObject>();
            Mock<IMobileServiceTable> mockTable;
            Mock<IMobileServiceClient> mockClient;
            IValueBinder binder = CreateJObjectBinder(parameter, out mockClient, out mockTable);
            JToken response = new JObject() as JToken;
            mockTable
                .Setup(m => m.LookupAsync("abc123"))
                .Returns(Task.FromResult(response));

            // Act
            var value = await binder.GetValueAsync();

            // Assert
            Assert.Same(response, value);
            mockTable.VerifyAll();
            mockClient.VerifyAll();
        }

        [Fact]
        public async Task GetValueAsync_Poco_QueriesItem()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetInputParameter<TodoItem>();
            Mock<IMobileServiceTable<TodoItem>> mockTable;
            Mock<IMobileServiceClient> mockClient;
            IValueBinder binder = CreatePocoBinder(parameter, out mockClient, out mockTable);
            var response = new TodoItem();
            mockTable
                .Setup(m => m.LookupAsync("abc123"))
                .Returns(Task.FromResult(response));

            // Act
            var value = await binder.GetValueAsync();

            // Assert
            Assert.Same(response, value);
            mockTable.VerifyAll();
            mockClient.VerifyAll();
        }

        [Fact]
        public async Task GetValueAsync_Poco_QueriesCorrectTable()
        {
            // Arrange                        
            var handler = new TestHandler("{}");
            var attribute = new MobileTableAttribute
            {
                Id = "abc123"
            };
            var context = new MobileTableContext
            {
                Client = new MobileServiceClient("https://someuri", handler),
                ResolvedAttribute = attribute
            };
            var binder = new MobileTableItemValueBinder<TodoItem>(context);

            // Act
            var value = await binder.GetValueAsync();

            // Assert
            Assert.NotNull(value);
            Assert.Equal("https://someuri/tables/TodoItem/abc123", handler.IssuedRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task GetValueAsync_PocoWithTable_QueriesCorrectTable()
        {
            // Arrange                        
            var handler = new TestHandler("{}");

            var attribute = new MobileTableAttribute
            {
                TableName = "SomeOtherTable",
                Id = "abc123"
            };

            var context = new MobileTableContext
            {
                ResolvedAttribute = attribute,
                Client = new MobileServiceClient("https://someuri", handler)
            };

            var binder = new MobileTableItemValueBinder<TodoItem>(context);

            // Act
            var value = await binder.GetValueAsync();

            // Assert          
            Assert.NotNull(value);
            Assert.Equal("https://someuri/tables/SomeOtherTable/abc123", handler.IssuedRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task GetValueAsync_JObject_DoesNotThrow_WhenResponseIsNotFound()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetInputParameter<JObject>();
            Mock<IMobileServiceTable> mockTable;
            Mock<IMobileServiceClient> mockClient;
            IValueBinder binder = CreateJObjectBinder(parameter, out mockClient, out mockTable);
            mockTable
                .Setup(m => m.LookupAsync("abc123"))
                .ThrowsAsync(new MobileServiceInvalidOperationException(string.Empty, null, new HttpResponseMessage(HttpStatusCode.NotFound)));

            // Act
            var value = await binder.GetValueAsync();

            // Assert
            Assert.Null(value);
            mockTable.VerifyAll();
            mockClient.VerifyAll();
        }

        [Fact]
        public async Task GetValueAsync_Poco_DoesNotThrow_WhenResponseIsNotFound()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetInputParameter<TodoItem>();
            Mock<IMobileServiceTable<TodoItem>> mockTable;
            Mock<IMobileServiceClient> mockClient;
            IValueBinder binder = CreatePocoBinder(parameter, out mockClient, out mockTable);
            mockTable
                .Setup(m => m.LookupAsync("abc123"))
                .ThrowsAsync(new MobileServiceInvalidOperationException(string.Empty, null, new HttpResponseMessage(HttpStatusCode.NotFound)));

            // Act
            var value = await binder.GetValueAsync();

            // Assert
            Assert.Null(value);
            mockTable.VerifyAll();
            mockClient.VerifyAll();
        }

        [Fact]
        public async Task GetValueAsync_Poco_Throws_WhenErrorResponse()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetInputParameter<TodoItem>();
            Mock<IMobileServiceTable<TodoItem>> mockTable;
            Mock<IMobileServiceClient> mockClient;
            IValueBinder binder = CreatePocoBinder(parameter, out mockClient, out mockTable);
            mockTable
                .Setup(m => m.LookupAsync("abc123"))
                .Throws(new MobileServiceInvalidOperationException(string.Empty, null, new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

            // Act
            var ex = await Assert.ThrowsAsync<MobileServiceInvalidOperationException>(() => binder.GetValueAsync());

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.Response.StatusCode);
        }

        [Fact]
        public async Task GetValueAsync_JObject_Throws_WhenErrorResponse()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetInputParameter<JObject>();
            Mock<IMobileServiceTable> mockTable;
            Mock<IMobileServiceClient> mockClient;
            IValueBinder binder = CreateJObjectBinder(parameter, out mockClient, out mockTable);
            mockTable
                .Setup(m => m.LookupAsync("abc123"))
                .Throws(new MobileServiceInvalidOperationException(string.Empty, null, new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));

            // Act
            var ex = await Assert.ThrowsAsync<MobileServiceInvalidOperationException>(() => binder.GetValueAsync());

            // Assert
            Assert.Equal(HttpStatusCode.ServiceUnavailable, ex.Response.StatusCode);
        }

        [Theory]
        [InlineData("ID")]
        [InlineData("Id")]
        [InlineData("iD")]
        [InlineData("id")]
        public void GetId_CaseInsensitive(string idKey)
        {
            // Arrange
            var token = JObject.Parse(string.Format("{{{0}:'abc123'}}", idKey));

            // Act
            var id = MobileTableItemValueBinder<object>.GetId(token);

            // Assert
            Assert.Equal("abc123", id);
        }

        [Fact]
        public void CloneItem_JObject_CorrectlySerializes()
        {
            // Arrange
            var jObject = new JObject();
            jObject["id"] = "abc123";
            jObject["text"] = "hello";
            jObject["complete"] = false;
            jObject["completedDate"] = DateTimeOffset.Now;

            // Act
            JObject cloned = MobileTableItemValueBinder<object>.CloneItem(jObject);

            // Assert
            Assert.NotSame(jObject, cloned);
            Assert.True(JToken.DeepEquals(jObject, cloned));
        }

        [Fact]
        public void CloneItem_Poco_CorrectlySerializes()
        {
            // Arrange
            TodoItem todoItem = new TodoItem
            {
                Id = "abc123",
                Text = "hello",
                Complete = false,
                CompletedDate = DateTimeOffset.Now
            };

            // Act
            JObject cloned = MobileTableItemValueBinder<object>.CloneItem(todoItem);
            TodoItem deserialized = cloned.ToObject<TodoItem>();

            // Assert
            Assert.Equal(todoItem.Id, deserialized.Id);
            Assert.Equal(todoItem.Text, deserialized.Text);
            Assert.Equal(todoItem.Complete, deserialized.Complete);
            Assert.Equal(todoItem.CompletedDate, deserialized.CompletedDate);
        }

        [Fact]
        public async Task SetAsync_JObject_Updates_IfPropertyChanges()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var mockTable = new Mock<IMobileServiceTable>(MockBehavior.Strict);

            JObject original = new JObject();
            original["id"] = "abc123";
            original["data"] = "hello";

            JObject updated = original.DeepClone() as JObject;
            original["data"] = "goodbye";

            mockTable
                .Setup(m => m.UpdateAsync(updated))
                .Returns(Task.FromResult<JToken>(null));

            mockClient
               .Setup(m => m.GetTable("TodoItem"))
               .Returns(mockTable.Object);

            var attribute = new MobileTableAttribute
            {
                TableName = "TodoItem"
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
                ResolvedAttribute = attribute
            };

            // Act
            await MobileTableItemValueBinder<JObject>.SetValueInternalAsync(original, updated, context);

            // Assert
            mockClient.VerifyAll();
            mockTable.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_Poco_Updates_IfPropertyChanges()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var mockTable = new Mock<IMobileServiceTable<TodoItem>>(MockBehavior.Strict);

            TodoItem original = new TodoItem
            {
                Id = "abc123",
                Text = "hello"
            };

            TodoItem updated = new TodoItem
            {
                Id = "abc123",
                Text = "goodbye"
            };

            mockTable
                .Setup(m => m.UpdateAsync(updated))
                .Returns(Task.FromResult<TodoItem>(null));

            mockClient
               .Setup(m => m.GetTable<TodoItem>())
               .Returns(mockTable.Object);

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
            };

            JObject clonedOrig = MobileTableItemValueBinder<object>.CloneItem(original);

            // Act
            await MobileTableItemValueBinder<TodoItem>.SetValueInternalAsync(clonedOrig, updated, context);

            // Assert
            mockClient.VerifyAll();
            mockTable.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_Poco_SkipsUpdate_IfSame()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);

            TodoItem original = new TodoItem
            {
                Id = "abc123",
                Text = "hello"
            };

            TodoItem updated = new TodoItem
            {
                Id = "abc123",
                Text = "hello"
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
            };

            JObject clonedOrig = MobileTableItemValueBinder<object>.CloneItem(original);

            // Act
            await MobileTableItemValueBinder<TodoItem>.SetValueInternalAsync(clonedOrig, updated, context);

            // Assert

            // nothing on the client should be called
            mockClient.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_JObject_SkipsUpdate_IfSame()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);

            JObject original = new JObject();
            original["id"] = "abc123";
            original["data"] = "hello";

            JObject updated = original.DeepClone() as JObject;

            var attribute = new MobileTableAttribute
            {
                TableName = "TodoItem"
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
                ResolvedAttribute = attribute
            };

            // Act
            await MobileTableItemValueBinder<JObject>.SetValueInternalAsync(original, updated, context);

            // Assert
            mockClient.VerifyAll();
        }

        [Fact]
        public async Task SetAsync_JObject_Throws_IfIdChanges()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);

            JObject original = new JObject();
            original["id"] = "abc123";
            original["data"] = "hello";

            JObject updated = original.DeepClone() as JObject;
            original["id"] = "def456";

            var attribute = new MobileTableAttribute
            {
                TableName = "TodoItem"
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
                ResolvedAttribute = attribute
            };

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => MobileTableItemValueBinder<JObject>.SetValueInternalAsync(original, updated, context));

            // Assert
            Assert.Equal("Cannot update the 'Id' property.", ex.Message);
            mockClient.Verify();
        }

        [Fact]
        public async Task SetAsync_Poco_Throws_IfIdChanges()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);

            TodoItem original = new TodoItem
            {
                Id = "abc123",
            };

            TodoItem updated = new TodoItem
            {
                Id = "def456",
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
            };

            var originalJson = JObject.FromObject(original);

            // Act
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => MobileTableItemValueBinder<TodoItem>.SetValueInternalAsync(originalJson, updated, context));

            // Assert
            Assert.Equal("Cannot update the 'Id' property.", ex.Message);
            mockClient.Verify();
        }

        [Fact]
        public async Task SetValue_Poco_UpdatesCorrectTable()
        {
            // Arrange                        
            var handler = new TestHandler("{}");
            var context = new MobileTableContext
            {
                Client = new MobileServiceClient("https://someuri", handler)
            };
            var original = JObject.Parse("{'Id':'abc123'}");
            var updated = new TodoItem { Id = "abc123", Text = "updated" };

            // Act
            await MobileTableItemValueBinder<TodoItem>.SetValueInternalAsync(original, updated, context);

            // Assert            
            Assert.Equal("https://someuri/tables/TodoItem/abc123", handler.IssuedRequest.RequestUri.ToString());
        }

        [Fact]
        public async Task SetValue_PocoWithTable_UpdatesCorrectTable()
        {
            // Arrange                        
            var handler = new TestHandler("{}");

            var attribute = new MobileTableAttribute
            {
                TableName = "SomeOtherTable"
            };

            var context = new MobileTableContext
            {
                ResolvedAttribute = attribute,
                Client = new MobileServiceClient("https://someuri", handler)
            };

            var original = JObject.Parse("{'Id':'abc123'}");
            var updated = new TodoItem { Id = "abc123", Text = "updated" };

            // Act
            await MobileTableItemValueBinder<TodoItem>.SetValueInternalAsync(original, updated, context);

            // Assert            
            Assert.Equal("https://someuri/tables/SomeOtherTable/abc123", handler.IssuedRequest.RequestUri.ToString());
        }

        private static IValueBinder CreateJObjectBinder(ParameterInfo parameter, out Mock<IMobileServiceClient> mockClient, out Mock<IMobileServiceTable> mockTable)
        {
            mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            mockTable = new Mock<IMobileServiceTable>(MockBehavior.Strict);

            mockClient
                .Setup(m => m.GetTable(It.IsAny<string>()))
                .Returns(mockTable.Object);

            var attribute = new MobileTableAttribute
            {
                TableName = "TodoItem",
                Id = "abc123"
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
                ResolvedAttribute = attribute
            };

            return new MobileTableItemValueBinder<JObject>(context);
        }

        private static IValueBinder CreatePocoBinder(ParameterInfo parameter, out Mock<IMobileServiceClient> mockClient, out Mock<IMobileServiceTable<TodoItem>> mockTable)
        {
            mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            mockTable = new Mock<IMobileServiceTable<TodoItem>>(MockBehavior.Strict);

            mockClient
                .Setup(m => m.GetTable<TodoItem>())
                .Returns(mockTable.Object);

            var attribute = new MobileTableAttribute
            {
                Id = "abc123"
            };

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
                ResolvedAttribute = attribute
            };

            return new MobileTableItemValueBinder<TodoItem>(context);
        }

        private static HttpResponseMessage CreateOkResponse(object payload = null)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK);

            if (payload != null)
            {
                response.Content = new StringContent(JsonConvert.SerializeObject(payload));
            }

            return response;
        }
    }
}