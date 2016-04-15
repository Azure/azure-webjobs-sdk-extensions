// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileTableOutputBindingProviderTests
    {
        [Theory]
        [InlineData(typeof(TodoItem), null, true, true)]
        [InlineData(typeof(JObject), "Items", true, true)]
        [InlineData(typeof(TodoItem[]), null, true, true)]
        [InlineData(typeof(JObject[]), "Items", true, true)]
        [InlineData(typeof(TodoItem), null, false, false)]
        [InlineData(typeof(JObject), "Items", false, false)]
        [InlineData(typeof(TodoItem[]), null, false, false)]
        [InlineData(typeof(JObject[]), "Items", false, false)]
        [InlineData(typeof(NoId), null, true, false)]
        [InlineData(typeof(ICollector<TodoItem>), null, true, false)]
        [InlineData(typeof(object), "Item", true, true)]
        [InlineData(typeof(object), null, true, false)]
        [InlineData(typeof(ICollector<TodoItem>), null, false, true)]
        [InlineData(typeof(IAsyncCollector<TodoItem>), null, false, true)]
        [InlineData(typeof(ICollector<JObject>), "Items", false, true)]
        [InlineData(typeof(IAsyncCollector<JObject>), "Items", false, true)]
        [InlineData(typeof(ICollector<TodoItem>), null, true, false)]
        [InlineData(typeof(TodoItem), null, false, false)]
        [InlineData(typeof(ICollector<NoId>), null, false, false)]
        [InlineData(typeof(ICollector<object>), "Item", false, true)]
        [InlineData(typeof(ICollector<object>), null, false, false)]
        [InlineData(typeof(ICollector<JObject>), null, false, false)]
        public void IsTypeValid_ValidatesCorrectly(Type parameterType, string tableName, bool isOutParameter, bool expected)
        {
            // Arrange
            Type typeToTest = isOutParameter ? parameterType.MakeByRefType() : parameterType;
            var context = new MobileTableContext { ResolvedTableName = tableName };

            // Act
            bool result = MobileTableOutputBindingProvider.IsTypeValid(typeToTest, context);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}