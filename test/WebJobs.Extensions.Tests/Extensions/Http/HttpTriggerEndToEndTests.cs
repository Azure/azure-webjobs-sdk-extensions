// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Xunit;


namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpTriggerEndToEndTests
    {
        private JobHost _host;

        public HttpTriggerEndToEndTests()
        {
            var config = new JobHostConfiguration();
            config.UseHttp();
            _host = new JobHost(config);
        }

        public void HttpToBlobTest()
        {

        }

        public class RequestInfo
        {
            public string A { get; set; }
            public string B { get; set; }
        }

        public static class TestFunctions
        {
            public static void HttpToBlob([HttpTrigger("get", "post")] RequestInfo request)
            {

            }
        }
    }
}
