// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Http;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpTriggerAttributeTests
    {
        [Fact]
        public void Constructor_AuthLevelOnly_ReturnsExpectedResult()
        {
            var attrib = new HttpTriggerAttribute(AuthorizationLevel.Admin);

            Assert.Equal(AuthorizationLevel.Admin, attrib.AuthLevel);
            Assert.Null(attrib.Methods);
        }

        [Fact]
        public void Constructor_AuthLevelAndMethods_ReturnsExpectedResult()
        {
            var attrib = new HttpTriggerAttribute(AuthorizationLevel.Admin, "GET", "POST");

            Assert.Equal(AuthorizationLevel.Admin, attrib.AuthLevel);
            Assert.Equal(2, attrib.Methods.Length);
            Assert.Equal("GET", attrib.Methods[0]);
            Assert.Equal("POST", attrib.Methods[1]);
        }

        [Fact]
        public void Constructor_MethodsOnly_ReturnsExpectedResult()
        {
            var attrib = new HttpTriggerAttribute("GET", "POST");

            Assert.Equal(AuthorizationLevel.Function, attrib.AuthLevel);
            Assert.Equal(2, attrib.Methods.Length);
            Assert.Equal("GET", attrib.Methods[0]);
            Assert.Equal("POST", attrib.Methods[1]);
        }
    }
}
