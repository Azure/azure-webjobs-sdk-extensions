// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.EasyTables
{
    public class TestHandler : DelegatingHandler
    {
        public HttpRequestMessage ActualRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            this.ActualRequest = request;

            var response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Content = new StringContent("[]");

            return Task.FromResult(response);
        }
    }
}
