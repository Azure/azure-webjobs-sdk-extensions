// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileTableQueryBindingTests
    {
        [Fact]
        public async Task BindAsync_Returns_CorrectValueProvider()
        {
            // Arrange
            var parameter = MobileAppTestHelper.GetValidInputQueryParameters().Single();
            var expectedType = typeof(MobileTableQueryValueProvider<TodoItem>);
            var mobileTableContext = new MobileTableContext();
            var binding = new MobileTableQueryBinding(parameter, mobileTableContext);

            // Act
            var valueProvider = await binding.BindAsync(null, null);

            // Assert
            Assert.Equal(expectedType, valueProvider.GetType());
        }

        [Theory]
        [InlineData(typeof(IMobileServiceTableQuery<TodoItem>), null, true)]
        [InlineData(typeof(IMobileServiceTableQuery<JObject>), null, false)]
        [InlineData(typeof(IMobileServiceTableQuery<NoId>), null, false)]
        [InlineData(typeof(TodoItem), null, false)]
        [InlineData(typeof(IMobileServiceTable<object>), null, false)]
        [InlineData(typeof(IMobileServiceTable<object>), "Item", false)] // object only works for output binding
        public void IsValidQueryType_ValidatesCorrectly(Type parameterType, string tableName, bool expected)
        {
            // Arrange
            var context = new MobileTableContext { ResolvedTableName = tableName };

            // Act
            bool result = MobileTableQueryBinding.IsValidQueryType(parameterType, context);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}