// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileTableTableValueProviderTests
    {
        [Fact]
        public void GetValue_JObject_ReturnsCorrectTable()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetValidInputTableParameters()
                .Where(p => p.ParameterType == typeof(IMobileServiceTable)).Single();
            var provider = new MobileTableTableValueProvider<JObject>(parameter, GetContext("TodoItem"));

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
            var parameter = MobileAppTestHelper.GetValidInputTableParameters()
                .Where(p => p.ParameterType == typeof(IMobileServiceTable<TodoItem>)).Single();
            var provider = new MobileTableTableValueProvider<TodoItem>(parameter, GetContext());

            // Act
            IMobileServiceTable<TodoItem> value = provider.GetValue() as IMobileServiceTable<TodoItem>;

            // Assert
            Assert.NotNull(value);
            Assert.Equal("TodoItem", value.TableName);
        }

        [Fact]
        public void GetValue_PocoWithTableName_ReturnsCorrectTable()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetValidInputTableParameters()
                .Where(p => p.ParameterType == typeof(IMobileServiceTable<TodoItem>)).Single();
            var provider = new MobileTableTableValueProvider<TodoItem>(parameter, GetContext("SomeOtherTable"));

            // Act
            IMobileServiceTable<TodoItem> value = provider.GetValue() as IMobileServiceTable<TodoItem>;

            // Assert
            Assert.NotNull(value);
            Assert.Equal("SomeOtherTable", value.TableName);
        }

        private MobileTableContext GetContext(string tableName = null)
        {
            return new MobileTableContext
            {
                Client = new MobileServiceClient("http://someuri"),
                ResolvedTableName = tableName
            };
        }
    }
}