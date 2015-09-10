using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.WebHooks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.WebHooks
{
    public class WebHookEndToEndTests : IClassFixture<WebHookEndToEndTests.TestFixture>
    {
        private readonly TestFixture _fixture;

        public WebHookEndToEndTests(TestFixture fixture)
        {
            _fixture = fixture;
            WebHookTestFunctions.InvokeData = null;
        }

        [Fact]
        public async Task ImplicitRoute_Succeeds()
        {
            await VerifyWebHook("WebHookTestFunctions/ImplicitRoute");
        }

        [Fact]
        public async Task ExplicitRoute_Succeeds()
        {
            await VerifyWebHook("test/hook");
        }

        [Fact]
        public async Task BindToString_Succeeds()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/BindToString", new StringContent(testData));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            string body = (string)WebHookTestFunctions.InvokeData;
            Assert.Equal(testData, body);
        }

        [Fact]
        public async Task InvalidHttpMethod_ReturnsMethodNotAllowed()
        {
            HttpResponseMessage response = await _fixture.Client.GetAsync("WebHookTestFunctions/BindToString");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }

        [Fact]
        public async Task InvalidRoute_ReturnsNotFound()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/DNE", new StringContent(testData));
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task FunctionThrows_ReturnsInternalServerError()
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/Throw", new StringContent(testData));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }

        [Fact]
        public async Task InvokeNonWebHook_Succeeds()
        {
            TestPoco poco = new TestPoco
            {
                A = Guid.NewGuid().ToString(),
                B = Guid.NewGuid().ToString()
            };
            JObject body = new JObject();
            JsonSerializer serializer = new JsonSerializer();
            StringWriter sr = new StringWriter();
            serializer.Serialize(sr, poco);
            body.Add("poco", sr.ToString());
            string json = body.ToString();

            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/NonWebHook", new StringContent(json));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            TestPoco result = (TestPoco)WebHookTestFunctions.InvokeData;
            Assert.Equal(poco.A, result.A);
            Assert.Equal(poco.B, result.B);
        }

        [Fact]
        public async Task InvokeNonWebHook_InvalidBody_ReturnsInternalServerError()
        {
            HttpResponseMessage response = await _fixture.Client.PostAsync("WebHookTestFunctions/NonWebHook", new StringContent("*92 kkdlinvalid"));
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }

        private async Task VerifyWebHook(string route)
        {
            string testData = Guid.NewGuid().ToString();
            HttpResponseMessage response = await _fixture.Client.PostAsync(route, new StringContent(testData));
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            HttpRequestMessage request = (HttpRequestMessage)WebHookTestFunctions.InvokeData;
            string body = await request.Content.ReadAsStringAsync();
            Assert.Equal(testData, body);
        }

        [Fact]
        public async Task DashboardStringInvoke_Succeeds()
        {
            // test the Dashboard invoke path which takes an invoke string in the
            // following format
            string testData = Guid.NewGuid().ToString();
            JObject content = new JObject();
            content.Add("url", string.Format("{0}{1}", _fixture.BaseUrl, "WebHookTestFunctions/ImplicitRoute"));
            content.Add("body", testData);
            string json = content.ToString();

            var args = new { request = json };
            await _fixture.Host.CallAsync(typeof(WebHookTestFunctions).GetMethod("ImplicitRoute"), args);

            HttpRequestMessage request = (HttpRequestMessage)WebHookTestFunctions.InvokeData;
            string body = await request.Content.ReadAsStringAsync();
            Assert.Equal(testData, body);
        }

        public static class WebHookTestFunctions
        {
            public static object InvokeData { get; set; }

            public static void ImplicitRoute([WebHookTrigger] HttpRequestMessage request)
            {
                InvokeData = request;
            }

            public static void ExplicitRoute([WebHookTrigger("test/hook")] HttpRequestMessage request)
            {
                InvokeData = request;
            }

            public static void BindToString([WebHookTrigger] string body)
            {
                InvokeData = body;
            }

            public static void NonWebHook([QueueTrigger("testqueue")] TestPoco poco)
            {
                InvokeData = poco;
            }

            public static void Throw([WebHookTrigger] HttpRequestMessage request)
            {
                throw new Exception("Kaboom!");
            }
        }

        public class TestFixture : IDisposable
        {
            public TestFixture()
            {
                int testPort = 43000;

                BaseUrl = string.Format("http://localhost:{0}/", testPort);
                Client = new HttpClient();
                Client.BaseAddress = new Uri(BaseUrl);

                JobHostConfiguration config = new JobHostConfiguration
                {
                    TypeLocator = new ExplicitTypeLocator(typeof(WebHookTestFunctions))
                };
                WebHooksConfiguration webHooksConfig = new WebHooksConfiguration(testPort);
                Host = new JobHost(config);
                config.UseWebHooks(Host, webHooksConfig);

                Host.Start();
            }

            public HttpClient Client { get; private set; }

            public JobHost Host { get; private set; }

            public string BaseUrl { get; private set; }

            public void Dispose()
            {
                Host.Stop();
                Host.Dispose();
            }
        }

        public class TestPoco
        {
            public string A { get; set; }
            public string B { get; set; }
        }
    }
}
