// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Tests
{
    public class HttpTriggerAttributeTests
    {
        [Fact]
        public void Constructor_AuthLevelOnly_ReturnsExpectedResult()
        {
            HttpTriggerAttribute attrib = new(AuthorizationLevel.Admin);

            Assert.Equal(AuthorizationLevel.Admin, attrib.AuthLevel);
            Assert.Null(attrib.Methods);
        }

        [Fact]
        public void Constructor_AuthLevelAndMethods_ReturnsExpectedResult()
        {
            HttpTriggerAttribute attrib = new(AuthorizationLevel.Admin, "GET", "POST");

            Assert.Equal(AuthorizationLevel.Admin, attrib.AuthLevel);
            Assert.Equal(2, attrib.Methods.Length);
            Assert.Equal("GET", attrib.Methods[0]);
            Assert.Equal("POST", attrib.Methods[1]);
        }

        [Fact]
        public void Constructor_MethodsOnly_ReturnsExpectedResult()
        {
            HttpTriggerAttribute attrib = new("GET", "POST");

            Assert.Equal(AuthorizationLevel.Function, attrib.AuthLevel);
            Assert.Equal(2, attrib.Methods.Length);
            Assert.Equal("GET", attrib.Methods[0]);
            Assert.Equal("POST", attrib.Methods[1]);
        }
    }
}
