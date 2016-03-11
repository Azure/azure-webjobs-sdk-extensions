// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableUtilityTests
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
        [InlineData(typeof(TodoItem[][]), false, typeof(TodoItem[]))]
        [InlineData(typeof(IAsyncCollector<TodoItem>), true, typeof(TodoItem))]
        public void GetCoreType_Returns_CorrectType(Type parameterType, bool isOutParameter, Type expectedType)
        {
            // Arrange
            Type typeToTest = isOutParameter ? parameterType.MakeByRefType() : parameterType;

            // Act
            Type coreType = EasyTableUtility.GetCoreType(typeToTest);

            // Assert
            Assert.Equal(coreType, expectedType);
        }

        [Fact]
        public void GetCoreType_ThrowsException_IfMultipleGenericArguments()
        {
            Assert.Throws<InvalidOperationException>(() => EasyTableUtility.GetCoreType(typeof(IDictionary<string, string>)));
        }

        [Theory]
        [InlineData(typeof(TodoItem), true)]
        [InlineData(typeof(JObject), true)]
        [InlineData(typeof(IMobileServiceTable), false)]
        [InlineData(typeof(IAsyncCollector<TodoItem>), false)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(NoId), false)]
        [InlineData(typeof(TwoId), false)]
        [InlineData(typeof(PrivateId), false)]
        public void IsValidEasyTableType_CorrectlyEvaluates(Type typeToEvaluate, bool expected)
        {
            // Act
            bool result = EasyTableUtility.IsValidItemType(typeToEvaluate);

            // Assert
            Assert.Equal(expected, result);
        }

        private class TwoId
        {
            public string ID { get; set; }

            public string Id { get; set; }
        }

        private class PrivateId
        {
            private string Id { get; set; }
        }
    }
}