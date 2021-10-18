// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests
{
    public class PublicSurfaceTests
    {
        [Fact]
        public void HttpPublicSurface_LimitedToSpecificTypes()
        {
            var assembly = typeof(HttpTriggerAttribute).Assembly;

            var expected = new[]
            {
                "HttpExtensionConstants",
                "AuthorizationLevel",
                "HttpTriggerAttribute",
                "HttpBindingApplicationBuilderExtension",
                "HttpBindingServiceCollectionExtensions",
                "HttpRequestExtensions",
                "IWebJobsRouteHandler",
                "IWebJobsRouter",
                "WebJobsRouteBuilder",
                "WebJobsRouter",
                "HttpOptions",
                "HttpWebJobsBuilderExtensions",
                "HttpWebJobsStartup",
            };

            JobHostTestHelpers.AssertPublicTypes(expected, assembly);
        }
    }
}
