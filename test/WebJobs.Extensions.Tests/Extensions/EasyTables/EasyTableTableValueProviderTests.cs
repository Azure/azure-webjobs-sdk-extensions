// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableTableValueProviderTests
    {
        private EasyTableContext _context = new EasyTableContext
        {
            Client = new MobileServiceClient("http://someuri"),
            ResolvedTableName = "TodoItem"
        };

        [Fact]
        public void GetValue_JObject_ReturnsCorrectTable()
        {
            // Arrange
            var parameter = EasyTableTestHelper.GetValidInputTableParameters()
                .Where(p => p.ParameterType == typeof(IMobileServiceTable)).Single();
            var provider = new EasyTableTableValueProvider<JObject>(parameter, _context);

            // Act
            IMobileServiceTable value = provider.GetValue() as IMobileServiceTable;

            // Assert
            Assert.NotNull(value);
            Assert.Equal("TodoItem", value.TableName);
        }

        [Fact]
        public void GetValue_Poco_ReturnsCorrectTable()
        {
            // Arrange
            var parameter = EasyTableTestHelper.GetValidInputTableParameters()
                .Where(p => p.ParameterType == typeof(IMobileServiceTable<TodoItem>)).Single();
            var provider = new EasyTableTableValueProvider<TodoItem>(parameter, _context);

            // Act
            IMobileServiceTable<TodoItem> value = provider.GetValue() as IMobileServiceTable<TodoItem>;

            // Assert
            Assert.NotNull(value);
            Assert.Equal("TodoItem", value.TableName);
        }
    }
}