// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Hosting;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    [Trait("Category", "E2E")]
    public class HttpTriggerEndToEndTests
    {
        private JobHost _host;

        public HttpTriggerEndToEndTests()
        {
            var host = new HostBuilder()
                .ConfigureDefaultTestHost(builder =>
                {
                    builder.AddHttp(o =>
                    {
                        o.SetResponse = SetResultHook;
                    })
                    .AddAzureStorage();
                }, typeof(TestFunctions))
                .Build();

            _host = host.GetJobHost();
        }

        private void SetResultHook(HttpRequest request, object result)
        {
            request.HttpContext.Items["$ret"] = result;
        }

        [Fact]
        public async Task BasicInvoke()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/api/123/two/test?q1=123&q2=two");
            request.Headers.Add("h1", "value1");
            request.Headers.Add("h2", "value2");
            var routeDataValues = new Dictionary<string, object>
            {
                { "r1", 123 },
                { "r2",  "two" }
            };
            request.HttpContext.Items[HttpExtensionConstants.AzureWebJobsHttpRouteDataKey] = routeDataValues;

            var method = typeof(TestFunctions).GetMethod("TestFunction1");
            await _host.CallAsync(method, new { req = request });
        }

        [Fact]
        public async Task BasicResponse()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/api/abc");
            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.TestResponse));
            await _host.CallAsync(method, new { req = request });

            Assert.Equal("test-response", request.HttpContext.Items["$ret"]); // Verify resposne was set
        }

        [Fact]
        public async Task QueryHeaderBindingParameters()
        {
            string testId = Guid.NewGuid().ToString();
            string testValue = Guid.NewGuid().ToString();
            string testSuffix = Guid.NewGuid().ToString();
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", $"http://functions.com/api/test?testId={testId}");
            request.Headers.Add("h1", "value1");
            request.Headers.Add("h2", "value2");
            request.Headers.Add("testSuffix", testSuffix);
            request.Headers.Add("testValue", testValue);
            var routeDataValues = new Dictionary<string, object>
            {
                { "r1", 123 },
                { "r2",  "two" }
            };
            request.HttpContext.Items[HttpExtensionConstants.AzureWebJobsHttpRouteDataKey] = routeDataValues;

            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.TestFunction2));
            await _host.CallAsync(method, new { req = request });

            // verify blob was written
            string blobName = $"test-{testId}-{testSuffix}";
            var account = CloudStorageAccount.Parse(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("test-output");
            var blobRef = await container.GetBlobReferenceFromServerAsync(blobName);
            await TestHelpers.Await(() => blobRef.ExistsAsync());

            MemoryStream stream = new MemoryStream();
            await blobRef.DownloadToStreamAsync(stream);
            stream.Seek(0, SeekOrigin.Begin);

            string result;
            using (var streamReader = new StreamReader(stream))
            {
                result = await streamReader.ReadToEndAsync();
            }

            Assert.Equal(testValue, result);
        }

        public static class TestFunctions
        {
            public static void TestFunction1(
                [HttpTrigger("get", "post", Route = "{r1:int}/{r2:alpha}/test")] HttpRequest req,
                int r1,
                string r2,
                IDictionary<string, string> headers,
                IDictionary<string, string> query)
            {
                Assert.Equal(123, r1);
                Assert.Equal("two", r2);

                Assert.Equal("123", query["q1"]);
                Assert.Equal("two", query["q2"]);

                Assert.Equal("value1", headers["h1"]);
                Assert.Equal("value2", headers["h2"]);
            }

            public static void TestFunction2(
                [HttpTrigger("post")] HttpRequest req,
                [Blob("test-output/test-{query.testId}-{headers.testSuffix}")] out string blob,
                IDictionary<string, string> headers,
                IDictionary<string, string> query)
            {
                blob = headers["testValue"];
            }

            public static Task<string> TestResponse(
                [HttpTrigger("get", "post")] HttpRequest req)
            {
                // Return value becomes the HttpResponseMessage.
                return Task.FromResult("test-response");
            }
        }
    }
}