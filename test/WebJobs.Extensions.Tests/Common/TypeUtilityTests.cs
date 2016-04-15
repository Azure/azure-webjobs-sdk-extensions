// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public class TypeUtilityTests
    {
        [Theory]
        [InlineData(typeof(JObject), true, typeof(JObject))]
        [InlineData(typeof(TodoItem), true, typeof(TodoItem))]
        [InlineData(typeof(JObject[]), true, typeof(JObject))]
        [InlineData(typeof(TodoItem[]), true, typeof(TodoItem))]
        [InlineData(typeof(IAsyncCollector<JObject>), false, typeof(JObject))]
        [InlineData(typeof(IAsyncCollector<TodoItem>), false, typeof(TodoItem))]
        [InlineData(typeof(ICollector<JObject>), false, typeof(JObject))]
        [InlineData(typeof(ICollector<TodoItem>), false, typeof(TodoItem))]
        [InlineData(typeof(JObject), false, typeof(JObject))]
        [InlineData(typeof(TodoItem), false, typeof(TodoItem))]
        [InlineData(typeof(IMobileServiceTable), false, typeof(IMobileServiceTable))]
        [InlineData(typeof(IMobileServiceTable<TodoItem>), false, typeof(TodoItem))]
        [InlineData(typeof(IMobileServiceTableQuery<TodoItem>), false, typeof(TodoItem))]
        [InlineData(typeof(IEnumerable<IEnumerable<TodoItem>>), false, typeof(IEnumerable<TodoItem>))]
        [InlineData(typeof(TodoItem[][]), false, typeof(TodoItem[][]))]
        [InlineData(typeof(IAsyncCollector<TodoItem>), true, typeof(IAsyncCollector<TodoItem>))]
        public void GetCoreType_Returns_CorrectType(Type parameterType, bool isOutParameter, Type expectedType)
        {
            // Arrange
            Type typeToTest = isOutParameter ? parameterType.MakeByRefType() : parameterType;

            // Act
            Type coreType = TypeUtility.GetCoreType(typeToTest);

            // Assert
            Assert.Equal(coreType, expectedType);
        }

        [Fact]
        public void GetCoreType_ThrowsException_IfMultipleGenericArguments()
        {
            Assert.Throws<InvalidOperationException>(() => TypeUtility.GetCoreType(typeof(IDictionary<string, string>)));
        }
    }
}
