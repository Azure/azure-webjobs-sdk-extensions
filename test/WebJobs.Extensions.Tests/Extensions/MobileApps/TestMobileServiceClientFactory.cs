// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    public class TestMobileServiceClientFactory : IMobileServiceClientFactory
    {
        private readonly HttpMessageHandler _innerHandler;

        public TestMobileServiceClientFactory(HttpMessageHandler innerHandler)
        {
            _innerHandler = innerHandler;
        }

        public IMobileServiceClient CreateClient(Uri mobileAppUri, HttpMessageHandler[] handlers)
        {
            // add the innerHandler to the end of the list of handlers
            var newHandlers = new List<HttpMessageHandler>();
            if (handlers != null)
            {
                newHandlers.AddRange(handlers);
            }
            newHandlers.Add(_innerHandler);

            return new MobileServiceClient(mobileAppUri, newHandlers.ToArray());
        }
    }
}
