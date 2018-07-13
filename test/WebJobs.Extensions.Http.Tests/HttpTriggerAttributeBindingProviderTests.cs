// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpTriggerAttributeBindingProviderTests
    {
        [Fact]
        public void HttpTriggerBinding_ToInvokeString_ReturnsExpectedResult()
        {
            var headers = new HeaderDictionary
            {
                { "Custom1", "Testing" }
            };

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "https://functions.azurewebsites.net/api/httptrigger?code=123&name=Mathew");

            string result = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.ToInvokeString(request);
            Assert.Equal("Method: GET, Uri: https://functions.azurewebsites.net/api/httptrigger", result);
        }
    }
}
