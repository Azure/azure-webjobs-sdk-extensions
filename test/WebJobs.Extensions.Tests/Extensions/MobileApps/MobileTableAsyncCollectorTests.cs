// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileTableAsyncCollectorTests
    {
        public static IEnumerable<object[]> TableNameScenarios
        {
            get
            {
                return new[]
                {
                    // Returning ItemToInsert, ResolvedTableName, ExpectedTableName
                    new object[] { new TodoItem { Id = "id", Text = "abc123" }, null, "TodoItem" },
                    new object[] { new TodoItem { Id = "id", Text = "abc123" }, "Item", "Item" },
                    new object[] { JObject.Parse("{'text':'abc123'}"), "Item", "Item" }
                };
            }
        }

        [Theory]
        [MemberData("TableNameScenarios")]
        public Task AddAsync_TableName_Wins<T>(T item, string tableName, string expectedTableName)
        {
            return RunTableNameTest(item, tableName, expectedTableName);
        }

        [Fact]
        public async Task AddAsync_AnonymousType()
        {
            // Note: Generics and anonymous types don't mix, so this test isn't
            //       using the RunTableNameTest method and is instead copying it.

            // Arrange
            var handler = new TestHandler("{}");
            var collector = CreateCollector<object>(handler, "Item");

            // Act
            await collector.AddAsync(new { id = "id", Text = "abc123", Deleted = true });

            // Assert
            Assert.Equal("https://someuri/tables/Item", handler.IssuedRequest.RequestUri.ToString());

            // Make sure Mobile Services is treating this like a JObject rather than a Poco. Pocos have
            // SystemProperties (like Deleted) stripped automatically. And make sure lowercase 'id' values are
            // sent.
            Assert.Equal("{\"id\":\"id\",\"Text\":\"abc123\",\"Deleted\":true}", handler.IssuedRequestContent);
        }

        [Fact]
        public async Task AddAsync_JObject_InsertsItem()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var mockTable = new Mock<IMobileServiceTable>(MockBehavior.Strict);
            var item = new JObject();

            mockTable
                .Setup(m => m.InsertAsync(item))
                .Returns(Task.FromResult(item as JToken));

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

            IAsyncCollector<JObject> collector = new MobileTableAsyncCollector<JObject>(context);

            // Act
            await collector.AddAsync(item);

            // Assert
            mockClient.VerifyAll();
            mockTable.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_Poco_InsertsItem()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var mockTable = new Mock<IMobileServiceTable<TodoItem>>(MockBehavior.Strict);
            var item = new TodoItem();

            mockTable
                .Setup(m => m.InsertAsync(item))
                .Returns(Task.FromResult(item));

            mockClient
                .Setup(m => m.GetTable<TodoItem>())
                .Returns(mockTable.Object);

            var context = new MobileTableContext
            {
                Client = mockClient.Object,
            };

            IAsyncCollector<TodoItem> collector = new MobileTableAsyncCollector<TodoItem>(context);

            // Act
            await collector.AddAsync(item);

            // Assert
            mockClient.VerifyAll();
            mockTable.VerifyAll();
        }

        [Fact]
        public async Task AddAsync_Object_InsertsItem()
        {
            // Arrange
            var mockClient = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var mockTable = new Mock<IMobileServiceTable>(MockBehavior.Strict);
            var item = new { id = "abc123", text = "123abc" };

            mockTable
                .Setup(m => m.InsertAsync(It.Is<JObject>(j => j["id"].ToString() == "abc123" && j["text"].ToString() == "123abc")))
                .Returns(Task.FromResult(JToken.Parse("{}")));

            mockClient
                .Setup(m => m.GetTable("Item"))
                .Returns(mockTable.Object);

            var attribute = new MobileTableAttribute
            {
                TableName = "Item"
            };

            var context = new MobileTableContext
            {
                ResolvedAttribute = attribute,
                Client = mockClient.Object,
            };

            IAsyncCollector<object> collector = new MobileTableAsyncCollector<object>(context);

            // Act
            await collector.AddAsync(item);

            // Assert
            mockClient.VerifyAll();
            mockTable.VerifyAll();
        }

        private static async Task RunTableNameTest<T>(T itemToInsert, string tableName, string expectedTableName)
        {
            // Arrange      
            var handler = new TestHandler("{}");
            var collector = CreateCollector<T>(handler, tableName);

            // Act
            await collector.AddAsync(itemToInsert);

            // Assert
            Assert.Equal("https://someuri/tables/" + expectedTableName, handler.IssuedRequest.RequestUri.ToString());
        }

        private static IAsyncCollector<T> CreateCollector<T>(HttpMessageHandler handler, string tableName)
        {
            var attribute = new MobileTableAttribute
            {
                TableName = tableName
            };

            var context = new MobileTableContext
            {
                ResolvedAttribute = attribute,
                Client = new MobileServiceClient("https://someuri", handler)
            };
            return new MobileTableAsyncCollector<T>(context);
        }
    }
}