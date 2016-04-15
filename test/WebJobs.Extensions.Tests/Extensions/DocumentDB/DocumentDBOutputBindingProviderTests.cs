// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

extern alias DocumentDB;

using System;
using DocumentDB::Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.Documents;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBOutputBindingProviderTests
    {
        [Theory]
        [InlineData(typeof(Item), true, true)]
        [InlineData(typeof(JObject), true, true)]
        [InlineData(typeof(Item[]), true, true)]
        [InlineData(typeof(JObject[]), true, true)]
        [InlineData(typeof(Item), false, false)]
        [InlineData(typeof(JObject), false, false)]
        [InlineData(typeof(Item[]), false, false)]
        [InlineData(typeof(object), true, true)]
        [InlineData(typeof(object), false, false)]
        [InlineData(typeof(ICollector<Document>), false, true)]
        [InlineData(typeof(IAsyncCollector<JObject>), false, true)]
        [InlineData(typeof(ICollector<Item>), true, true)]
        public void IsTypeValid_ValidatesCorrectly(Type parameterType, bool isOutParameter, bool expected)
        {
            // Arrange
            Type typeToTest = isOutParameter ? parameterType.MakeByRefType() : parameterType;

            // Act
            bool result = DocumentDBOutputBindingProvider.IsTypeValid(typeToTest);

            // Assert
            Assert.Equal(expected, result);
        }
    }
}
