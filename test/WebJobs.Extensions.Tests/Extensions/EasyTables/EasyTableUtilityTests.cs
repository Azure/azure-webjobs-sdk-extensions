// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.EasyTables;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.EasyTables
{
    public class EasyTableUtilityTests
    {
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