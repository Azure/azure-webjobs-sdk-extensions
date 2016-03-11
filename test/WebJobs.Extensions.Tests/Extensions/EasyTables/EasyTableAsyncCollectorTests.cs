// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableAsyncCollectorTests
    {
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

            var context = new EasyTableContext
            {
                Client = mockClient.Object,
                ResolvedTableName = "TodoItem"
            };

            IAsyncCollector<JObject> collector = new EasyTableAsyncCollector<JObject>(context);

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

            var context = new EasyTableContext
            {
                Client = mockClient.Object,
            };

            IAsyncCollector<TodoItem> collector = new EasyTableAsyncCollector<TodoItem>(context);

            // Act
            await collector.AddAsync(item);

            // Assert
            mockClient.VerifyAll();
            mockTable.VerifyAll();
        }
    }
}