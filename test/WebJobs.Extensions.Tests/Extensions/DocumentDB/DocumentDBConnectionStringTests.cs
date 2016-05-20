// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.DocumentDB.Config;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBConnectionStringTests
    {
        [Theory]
        [InlineData("AccountEndpoint=https://someuri/;AccountKey=some_key;", "https://someuri/", "some_key")]
        [InlineData("AccountEndpoint=https://someuri/", "https://someuri/", null)]
        [InlineData("AccountKey=some_key", null, "some_key")]
        public void Constructor_ParsesCorrectly(string connectionString, string expectedUri, string expectedKey)
        {
            // Act
            var docDBConnStr = new DocumentDBConnectionString(connectionString);

            // Assert
            if (expectedUri == null)
            {
                Assert.Null(docDBConnStr.ServiceEndpoint);
            }
            else
            {
                Assert.Equal(expectedUri, docDBConnStr.ServiceEndpoint.ToString());
            }
            Assert.Equal(expectedKey, docDBConnStr.AuthKey);
        }
    }
}
