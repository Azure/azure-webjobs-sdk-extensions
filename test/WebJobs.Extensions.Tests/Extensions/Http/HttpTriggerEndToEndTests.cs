// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Xunit;
using Newtonsoft.Json;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    [Trait("Category", "E2E")]
    public class HttpTriggerEndToEndTests
    {
        private JobHostConfiguration _config;
        private JobHost _host;

        public HttpTriggerEndToEndTests()
        {
            var httpConfig = new HttpExtensionConfiguration();
            httpConfig.SetResponse = SetResultHook;
            _config = new JobHostConfiguration
            {
                TypeLocator = new ExplicitTypeLocator(typeof(TestFunctions))
            };
            _config.UseHttp(httpConfig);
            _host = new JobHost(_config);
        }

        private void SetResultHook(HttpRequestMessage request, object result)
        {
            request.Properties["$ret"] = result;
        }

        [Fact]
        public void BasicInvoke()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://functions.com/api/123/two/test?q1=123&q2=two");
            request.Headers.Add("h1", "value1");
            request.Headers.Add("h2", "value2");
            var routeDataValues = new Dictionary<string, object>
            {
                { "r1", 123 },
                { "r2",  "two" }
            };
            request.Properties[HttpExtensionConstants.AzureWebJobsHttpRouteDataKey] = routeDataValues;

            var method = typeof(TestFunctions).GetMethod("TestFunction1");
            _host.Call(method, new { req = request });
        }

        [Fact]
        public void BasicResponse()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://functions.com/api/abc");
            var method = typeof(TestFunctions).GetMethod("TestResponse");
            _host.Call(method, new { req = request });

            Assert.Equal(request.Properties["$ret"], "test-response"); // Verify resposne was set
        }

        // Verify we can do a host.call and pass in a httprequestmessage directly apart from the [HttpTrigger].
        [Fact]
        public void BindToRequestAndPocoSeparately()
        {
            var json = JsonConvert.SerializeObject(new TestFunctions.MyPoco { Value = "abc" });
            var request = new HttpRequestMessage(HttpMethod.Post, "http://functions.com/api/abc")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var directRequest = new HttpRequestMessage();

            // Trigger data is specificaly the parameter with [HttpTrigger], not the parameter of type request. 
            var method = typeof(TestFunctions).GetMethod("TestMultiBinding");
            _host.Call(method, new {
                triggerValue = request,
                directRequest = directRequest // overides the implicit HttpRequestMessage binding. 
            });

            Assert.Equal(directRequest.Properties["result"], "abc"); // Verify resposne was set
        }

        // verify that we can bind [HttpTrigger] to a poco and still get a HttpRequestMessage. 
        [Fact]
        public void BindToRequestAndPocoAtSameTime()
        {
            var json = JsonConvert.SerializeObject(new TestFunctions.MyPoco { Value = "abc" });
            var request = new HttpRequestMessage(HttpMethod.Post, "http://functions.com/api/abc")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            // Trigger data is specificaly the parameter with [HttpTrigger], not the parameter of type request. 
            var method = typeof(TestFunctions).GetMethod("TestMultiBinding");
            _host.Call(method, new { triggerValue = request,  });

            Assert.Equal(request.Properties["result"], "abc"); // Verify resposne was set
        }

        [Fact]
        public async Task QueryHeaderBindingParameters()
        {
            string testId = Guid.NewGuid().ToString();
            string testValue = Guid.NewGuid().ToString();
            string testSuffix = Guid.NewGuid().ToString();
            var request = new HttpRequestMessage(HttpMethod.Get, $"http://functions.com/api/test?testId={testId}");
            request.Headers.Add("h1", "value1");
            request.Headers.Add("h2", "value2");
            request.Headers.Add("testSuffix", testSuffix);
            request.Headers.Add("testValue", testValue);
            var routeDataValues = new Dictionary<string, object>
            {
                { "r1", 123 },
                { "r2",  "two" }
            };
            request.Properties[HttpExtensionConstants.AzureWebJobsHttpRouteDataKey] = routeDataValues;

            var method = typeof(TestFunctions).GetMethod("TestFunction2");
            await _host.CallAsync(method, new { req = request });

            // verify blob was written
            string blobName = $"test-{testId}-{testSuffix}";
            var account = CloudStorageAccount.Parse(_config.StorageConnectionString);
            CloudBlobClient client = account.CreateCloudBlobClient();
            CloudBlobContainer container = client.GetContainerReference("test-output");
            var blobRef = await container.GetBlobReferenceFromServerAsync(blobName);
            await TestHelpers.Await(() => blobRef.Exists());

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
                [HttpTrigger("get", "post", Route = "{r1:int}/{r2:alpha}/test")] HttpRequestMessage req,
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
                [HttpTrigger("post")] HttpRequestMessage req,
                [Blob("test-output/test-{query.testId}-{headers.testSuffix}")] out string blob,
                IDictionary<string, string> headers,
                IDictionary<string, string> query)
            {
                blob = headers["testValue"];
            }
                        
            public static Task<string> TestResponse(
                [HttpTrigger("get", "post")] HttpRequestMessage req)
            {
                // Return value becomes the HttpResponseMessage.
                return Task.FromResult("test-response"); 
            }

            // Test that we can bind to both a Poco and the direct request message
            public static void TestMultiBinding(
                  [HttpTrigger("get", "post")] MyPoco triggerValue,
                  HttpRequestMessage directRequest)
            {
                directRequest.Properties["result"] = triggerValue.Value;
            }

            public class MyPoco
            {
                public string Value { get; set; }
            }
        }
    }
}
