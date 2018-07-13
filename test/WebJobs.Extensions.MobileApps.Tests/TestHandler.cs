// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    public class TestHandler : DelegatingHandler
    {
        private string _jsonContentToReturn;
        
        public TestHandler() : this("[]")
        {
        }

        public TestHandler(string jsonContentToReturn)
        {
            _jsonContentToReturn = jsonContentToReturn;
        }

        public HttpRequestMessage IssuedRequest { get; private set; }

        public string IssuedRequestContent { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.IssuedRequest = request;

            if (request.Content != null)
            {
                this.IssuedRequestContent = await request.Content.ReadAsStringAsync();
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent(_jsonContentToReturn);

            return response;
        }
    }
}
