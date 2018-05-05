// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Azure.WebJobs.Host.Bindings.Path;
using Microsoft.Azure.WebJobs.Host.Triggers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Options;
using Moq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpTriggerBindingTests
    {
        ILoggerFactory _loggerFactory;

        public HttpTriggerBindingTests()
        {
            _loggerFactory = new LoggerFactory();
            _loggerFactory.AddProvider(new TestLoggerProvider());
        }

        [Fact]
        public async Task GetRequestBindingDataAsync_ReadsFromBody()
        {
            string input = "{ test: 'testing', baz: 123, nestedArray: [ { nesting: 'yes' } ], nestedObject: { a: 123, b: 456 } }";
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test", null, input);
            var bindingData = await HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingDataAsync(request);

            Assert.Equal(5, bindingData.Count);
            Assert.Equal("testing", bindingData["test"]);
            Assert.Equal("123", bindingData["baz"]);

            JObject nestedObject = (JObject)bindingData["nestedObject"];
            Assert.Equal(123, (int)nestedObject["a"]);
        }

        [Fact]
        public async Task GetRequestBindingDataAsync_ReadsFromQueryString()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test?name=Mathew%20Charles&location=Seattle");

            var bindingData = await HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingDataAsync(request);

            Assert.Equal(4, bindingData.Count);
            Assert.Equal("Mathew Charles", bindingData["name"]);
            Assert.Equal("Seattle", bindingData["location"]);

            TestBindingData(bindingData,
                "{name}", "Mathew Charles",
                "{location}", "Seattle",
                "{query.name}", "Mathew Charles",
                "{query.location}", "Seattle");
        }

        [Fact]
        public async Task GetRequestBindingDataAsync_ReadsFromRoute()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test");

            Dictionary<string, object> routeData = new Dictionary<string, object>
            {
                { "Name", "Mathew Charles" },
                { "Location", "Seattle" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            var bindingData = await HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingDataAsync(request);

            Assert.Equal(4, bindingData.Count);
            Assert.Equal("Mathew Charles", bindingData["Name"]);
            Assert.Equal("Seattle", bindingData["Location"]);
        }

        // When we have the same name in multiple places, ensure that
        // we can access it unambiguously via binding expression
        [Fact]
        public async Task GetRequestBindingDataAsync_ReadsFrom_Duplicates()
        {
            string input = "{ name: 'body1', nestedObject: { name: 'body2' } }";
            var headers = new HeaderDictionary();
            headers.Add("name", "Mathew");

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/{test:alpha}/test?name=Amy", headers, input);

            var routeData = new Dictionary<string, object>
            {
                { "test", "path1" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            var bindingData = await HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingDataAsync(request);

            TestBindingData(bindingData,
                "{headers.name}", "Mathew",
                "{test}", "path1",
                "{query.name}", "Amy");
        }

        // Ensure specifically we can bind to the authorization headers  
        [Fact]
        public async Task GetRequestBindingDataAsync_Auth_Header()
        {
            var headers = new HeaderDictionary();

            headers.Add("Authorization", "Bearer ey123");
            headers.Add("x-ms-id-aad", "ey456");

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test", headers);

            var bindingData = await HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingDataAsync(request);

            TestBindingData(bindingData,
                "{headers.authorization}", "Bearer ey123",
                "{headers.x-ms-id-aad}", "ey456");
        }

        [Fact]
        public async Task BindAsync_Poco_FromRequestBody()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, true);

            JObject requestBody = new JObject
            {
                { "Name", "Mathew Charles" },
                { "Location", "Seattle" }
            };

            var headers = new HeaderDictionary();
            headers.Add("Content-Type", "application/json");
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", headers, requestBody.ToString());

            IServiceCollection services = new ServiceCollection();
            services.AddMvc();
            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

            request.HttpContext.RequestServices = services.BuildServiceProvider();

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            TestPoco testPoco = (TestPoco)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("Mathew Charles", testPoco.Name);
            Assert.Equal("Seattle", testPoco.Location);
        }

        [Fact]
        public async Task BindAsync_Poco_WebHookData()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123");
            TestPoco testPoco = new TestPoco
            {
                Name = "Mathew Charles",
                Location = "Seattle"
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsWebHookDataKey, testPoco);

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            TestPoco result = (TestPoco)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(testPoco, result);
        }

        [Fact]
        public async Task BindAsync_Poco_FromQueryParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Name=Mathew%20Charles&Location=Seattle");

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            TestPoco testPoco = (TestPoco)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("Mathew Charles", testPoco.Name);
            Assert.Equal("Seattle", testPoco.Location);
        }

        [Fact]
        public async Task BindAsync_Poco_FromRouteParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc");

            Dictionary<string, object> routeData = new Dictionary<string, object>
            {
                { "Name", "Mathew Charles" },
                { "Location", "Seattle" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            TestPoco testPoco = (TestPoco)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("Mathew Charles", testPoco.Name);
            Assert.Equal("Seattle", testPoco.Location);
        }

        [Fact]
        public async Task BindAsync_Poco_MergedBindingData()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunctionEx").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, true);

            JObject requestBody = new JObject
            {
                { "Name", "Mathew Charles" },
                { "Phone", "(425) 555-6666" }
            };

            var headers = new HeaderDictionary();
            headers.Add("Content-Type", "application/json");

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Age=25", headers, requestBody.ToString());
            var services = new ServiceCollection();
            services.AddOptions();
            services.AddSingleton(Options.Create(new MvcOptions()));
            var formatter = new Mock<IInputFormatter>();
            formatter.Setup(f => f.ReadAsync(It.IsAny<InputFormatterContext>()))
                .ReturnsAsync((InputFormatterContext c) =>
                {
                    TextReader reader = c.ReaderFactory(c.HttpContext.Request.Body, Encoding.UTF8);
                    JsonSerializer serializer = new JsonSerializer();
                    object result = serializer.Deserialize(reader, c.Metadata.ModelType);

                    return InputFormatterResult.Success(result);
                });

            services.AddMvcCore(o => o.InputFormatters.Add(formatter.Object));
            request.HttpContext.RequestServices = services.BuildServiceProvider();

            Dictionary<string, object> routeData = new Dictionary<string, object>
            {
                { "Location", "Seattle" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(9, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);
            Assert.Equal("(425) 555-6666", triggerData.BindingData["Phone"]);
            Assert.Equal("25", triggerData.BindingData["Age"]);

            TestPocoEx testPoco = (TestPocoEx)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("Mathew Charles", testPoco.Name);
            Assert.Equal("Seattle", testPoco.Location);
            Assert.Equal("(425) 555-6666", testPoco.Phone);
            Assert.Equal(25, testPoco.Age);
        }

        [Fact]
        public async Task BindAsync_HttpRequest_FromRequestBody()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, false);

            // we intentionally do not send a content type on the request
            // to ensure that we can still extract binding data in such cases
            JObject requestBody = new JObject
            {
                { "Name", "Mathew Charles" },
                { "Location", "Seattle" }
            };
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", null, requestBody.ToString());

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            HttpRequest result = (HttpRequest)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(request, result);
        }

        [Fact]
        public async Task BindAsync_HttpRequest_FromQueryParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, false);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Name=Mathew%20Charles&Location=Seattle");

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            HttpRequest result = (HttpRequest)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Same(request, result);
        }

        [Fact]
        public async Task BindAsync_HttpRequestMessage_FromRequestBody()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestMessageFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, false);

            // we intentionally do not send a content type on the request
            // to ensure that we can still extract binding data in such cases
            JObject requestBody = new JObject
            {
                { "Name", "Mathew Charles" },
                { "Location", "Seattle" }
            };

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", null, requestBody.ToString());
            request.ContentType = "application/json";

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            HttpRequestMessage result = (HttpRequestMessage)(await triggerData.ValueProvider.GetValueAsync());
            Assert.NotNull(result);

            var contentResult = await result.Content.ReadAsAsync<JObject>();
            Assert.Equal("Mathew Charles", contentResult["Name"]);
        }

        [Fact]
        public async Task BindAsync_HttpRequestMessage_FromQueryParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestMessageFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, false);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Name=Mathew%20Charles&location=Hawaii&Location=Ohio&Location=Seattle");

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("Mathew Charles", triggerData.BindingData["Name"]);
            Assert.Equal("Seattle", triggerData.BindingData["Location"]);

            HttpRequestMessage result = (HttpRequestMessage)(await triggerData.ValueProvider.GetValueAsync());
            Assert.NotNull(result);
        }

        [Fact]
        public async Task BindAsync_String()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestStringFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, false);

            var headers = new HeaderDictionary();
            headers.Add("Content-Type", "application/text");
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", headers, "This is a test");

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(3, triggerData.BindingData.Count);

            string result = (string)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("This is a test", result);
        }

        [Fact]
        public async Task BindAsync_Dynamic()
        {
            ParameterInfo parameterInfo = GetType().GetMethod(nameof(TestDynamicFunction)).GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, false);

            var headers = new HeaderDictionary();
            headers.Add("Content-Type", "application/json");
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", headers, "{ \"value\" : \"This is a test\" }");

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(4, triggerData.BindingData.Count);

            var result = (JObject)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("This is a test", result["value"].ToString());
        }

        [Fact]
        public static void ApplyBindingData_Succeeds()
        {
            TestPocoEx poco = new TestPocoEx();
            Dictionary<string, string> properties = new Dictionary<string, string>
            {
                { "A", "123" },
                { "B", "456" },
                { "c", "789" }
            };
            Dictionary<string, object> bindingData = new Dictionary<string, object>()
            {
                { "name", "Ted" },
                { "Location", "Seattle" },
                { "Age", "25" },
                { "Readonly", "Test" },
                { "Properties", properties }
            };

            HttpTriggerAttributeBindingProvider.HttpTriggerBinding.ApplyBindingData(poco, bindingData);

            Assert.Equal("Ted", poco.Name);
            Assert.Equal("Seattle", poco.Location);
            Assert.Equal(25, poco.Age);  // verifies string was converted
            Assert.Null(poco.Readonly);
            Assert.Equal(3, poco.Properties.Count);
            foreach (var pair in properties)
            {
                Assert.Equal(pair.Value, poco.Properties[pair.Key]);
            }
        }

        private static void TestBindingData(IReadOnlyDictionary<string, object> bindingData, params string[] values)
        {
            for (int i = 0; i < values.Length; i += 2)
            {
                var expression = values[i];
                var expectedResult = values[i + 1];
                var template = BindingTemplate.FromString(expression);
                var result = template.Bind(bindingData);
                Assert.Equal(expectedResult, result);
            }
        }

        public void TestPocoFunction(TestPoco poco)
        {
        }

        public void TestPocoFunctionEx(TestPocoEx poco)
        {
        }

        public void TestHttpRequestFunction(HttpRequest req)
        {
        }

        public void TestHttpRequestMessageFunction(HttpRequestMessage req)
        {
        }

        public void TestStringFunction(string body)
        {
        }

        public void TestDynamicFunction(dynamic body)
        {
        }

        public class TestPoco
        {
            public string Name { get; set; }

            public string Location { get; set; }
        }

        public class TestPocoEx : TestPoco
        {
            public int Age { get; set; }

            public string Phone { get; set; }

            public string Readonly { get; }

            public IDictionary<string, string> Properties { get; set; }
        }
    }
}
