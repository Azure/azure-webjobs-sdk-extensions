// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableQueryBindingTests
    {
        [Fact]
        public async Task BindAsync_Returns_CorrectValueProvider()
        {
            // Arrange
            var parameter = EasyTableTestHelper.GetValidInputQueryParameters().Single();
            var expectedType = typeof(EasyTableQueryValueProvider<TodoItem>);
            var easyTableContext = new EasyTableContext();
            var binding = new EasyTableQueryBinding(parameter, easyTableContext);

            // Act
            var valueProvider = await binding.BindAsync(null, null);

            // Assert
            Assert.Equal(expectedType, valueProvider.GetType());
        }

        [Theory]
        [InlineData(typeof(IMobileServiceTableQuery<TodoItem>), true)]
        [InlineData(typeof(IMobileServiceTableQuery<JObject>), false)]
        [InlineData(typeof(IMobileServiceTableQuery<NoId>), false)]
        [InlineData(typeof(TodoItem), false)]
        public void IsValidQueryType_ValidatesCorrectly(Type parameterType, bool expected)
        {
            // Act
            bool result = EasyTableQueryBinding.IsValidQueryType(parameterType);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}