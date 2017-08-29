// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    public class MobileServiceApiKeyHandlerTests
    {
        [Fact]
        public async Task SendAsync_AddsHeader()
        {
            // Arrange
            var testHandler = new TestHandler();
            var handler = new MobileServiceApiKeyHandler("my_api_key")
            {
                InnerHandler = testHandler
            };
            var client = new HttpClient(handler);

            // Act
            var response = await client.GetAsync("https://someuri/");

            // Assert
            var headerValue = testHandler.IssuedRequest.Headers.GetValues(MobileServiceApiKeyHandler.ZumoApiKeyHeaderName).Single();
            Assert.Equal("my_api_key", headerValue);
        }
    }
}
