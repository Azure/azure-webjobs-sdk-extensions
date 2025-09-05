// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Tests
{
    public class HttpTriggerAttributeBindingProviderTests
    {
        [Fact]
        public void HttpTriggerBinding_ToInvokeString_ReturnsExpectedResult()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "https://functions.azurewebsites.net/api/httptrigger?code=123&name=FirstName");
            string result = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.ToInvokeString(request);
            Assert.Equal("Method: GET, Uri: https://functions.azurewebsites.net/api/httptrigger", result);
        }
    }
}
