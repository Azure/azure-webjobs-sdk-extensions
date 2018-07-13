// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.WindowsAzure.MobileServices;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps
{
    public class MobileAppUtilityTests
    {
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1804:RemoveUnusedLocals", MessageId = "attribute")]
        [Theory]
        [InlineData(typeof(TodoItem), null, true)]
        [InlineData(typeof(JObject), "Item", true)]
        [InlineData(typeof(IMobileServiceTable), null, false)]
        [InlineData(typeof(IAsyncCollector<TodoItem>), null, false)]
        [InlineData(typeof(string), null, false)]
        [InlineData(typeof(NoId), null, false)]
        [InlineData(typeof(TwoId), null, false)]
        [InlineData(typeof(PrivateId), null, false)]
        [InlineData(typeof(JObject), null, false)]
        [InlineData(typeof(TodoItem), "Item", true)]
        [InlineData(typeof(object), "Item", false)]
        [InlineData(typeof(object), null, false)]
        public void IsValidMobileTableType_CorrectlyEvaluates(Type typeToEvaluate, string tableName, bool expected)
        {
            // Act
            bool result = MobileAppUtility.IsValidItemType(typeToEvaluate, tableName);

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