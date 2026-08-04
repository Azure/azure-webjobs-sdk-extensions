// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
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

namespace Microsoft.Azure.WebJobs.Extensions.Http.Tests
{
    public class HttpTriggerBindingTests
    {
        public HttpTriggerBindingTests()
        {
        }

        [Fact]
        public void GetRequestBindingData_ReadsFromBody()
        {
            string input = "{ test: 'testing', baz: 123, nestedArray: [ { nesting: 'yes' } ], nestedObject: { a: 123, b: 456 } }";
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test", null, input);
            var bindingData = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingData(request, input);

            Assert.Equal(5, bindingData.Count);
            Assert.Equal("testing", bindingData["test"]);
            Assert.Equal("123", bindingData["baz"]);

            JObject nestedObject = (JObject)bindingData["nestedObject"];
            Assert.Equal(123, (int)nestedObject["a"]);
        }

        [Fact]
        public void GetRequestBindingData_ReadsFromQueryString()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test?name=FirstName%20LastName&location=MyCity");

            var bindingData = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingData(request);

            Assert.Equal(4, bindingData.Count);
            Assert.Equal("FirstName LastName", bindingData["name"]);
            Assert.Equal("MyCity", bindingData["location"]);

            TestBindingData(bindingData,
                "{name}", "FirstName LastName",
                "{location}", "MyCity",
                "{query.name}", "FirstName LastName",
                "{query.location}", "MyCity");
        }

        [Fact]
        public void GetRequestBindingData_ReadsFromRoute()
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test");

            Dictionary<string, object> routeData = new()
            {
                { "Name", "FirstName LastName" },
                { "Location", "MyCity" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            var bindingData = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingData(request);

            Assert.Equal(4, bindingData.Count);
            Assert.Equal("FirstName LastName", bindingData["Name"]);
            Assert.Equal("MyCity", bindingData["Location"]);
        }

        [Theory]
        [ClassData(typeof(BindsAllTypesTestData))]
        public void GetRequestBindingData_ReadsFromRoute_BindsAllTypes(Type type, object value, string stringValue)
        {
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test");

            Dictionary<string, object> routeData = new()
            {
                { "test", stringValue }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            Dictionary<string, Type> bindingDataContract = new()
            {
                { "test", type }
            };

            var bindingData = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingData(request, bindingDataContract: bindingDataContract);

            Assert.Equal(3, bindingData.Count);
            Assert.Equal(value, bindingData["test"]);

            // Can only compare types if something was passed in
            if (value != null)
            {
                // Data is bound by underlying type if nullable
                Assert.Equal(Nullable.GetUnderlyingType(type) ?? type, bindingData["test"].GetType());
            }
        }

        // When we have the same name in multiple places, ensure that
        // we can access it unambiguously via binding expression
        [Fact]
        public void GetRequestBindingData_ReadsFrom_Duplicates()
        {
            string input = "{ name: 'body1', nestedObject: { name: 'body2' } }";
            HeaderDictionary headers = new()
            {
                ["name"] = "FirstName"
            };

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/{test:alpha}/test?name=Amy", headers, input);

            Dictionary<string, object> routeData = new()
            {
                ["test"] = "path1"
            };

            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);
            var bindingData = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingData(request, input);

            TestBindingData(bindingData,
                "{headers.name}", "FirstName",
                "{test}", "path1",
                "{query.name}", "Amy");
        }

        // Ensure specifically we can bind to the authorization headers  
        [Fact]
        public void GetRequestBindingData_Auth_Header()
        {
            HeaderDictionary headers = new()
            {
                { "Authorization", "Bearer ey123" },
                { "x-ms-id-aad", "ey456" }
            };

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/test", headers);

            var bindingData = HttpTriggerAttributeBindingProvider.HttpTriggerBinding.GetRequestBindingData(request);

            TestBindingData(bindingData,
                "{headers.authorization}", "Bearer ey123",
                "{headers.x-ms-id-aad}", "ey456");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BindAsync_Poco_FromRequestBody(bool isChunked)
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            var httpOptions = Options.Create<HttpOptions>(new HttpOptions()
            {
                EnableChunkedRequestBinding = isChunked
            });
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, true, httpOptions);

            JObject requestBody = new()
            {
                { "Name", "FirstName LastName" },
                { "Location", "MyCity" }
            };

            HeaderDictionary headers = new() { ["Content-Type"] = "application/json" };
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", headers, requestBody.ToString());
            if (isChunked) 
            {
                request.ContentLength = null;
                request.Headers.TransferEncoding = "chunked";
            }

            IServiceCollection services = new ServiceCollection();
            services.AddMvc();
            services.AddSingleton<ObjectPoolProvider, DefaultObjectPoolProvider>();
            services.AddSingleton<ILoggerFactory, NullLoggerFactory>();

            request.HttpContext.RequestServices = services.BuildServiceProvider();

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            TestPoco testPoco = (TestPoco)await triggerData.ValueProvider.GetValueAsync();
            Assert.Equal("FirstName LastName", testPoco.Name);
            Assert.Equal("MyCity", testPoco.Location);
        }

        [Fact]
        public async Task BindAsync_Poco_WebHookData()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123");
            TestPoco testPoco = new()
            {
                Name = "FirstName LastName",
                Location = "MyCity"
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsWebHookDataKey, testPoco);

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            TestPoco result = (TestPoco)await triggerData.ValueProvider.GetValueAsync();
            Assert.Same(testPoco, result);
        }

        [Fact]
        public async Task BindAsync_Poco_FromQueryParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Name=FirstName%20LastName&Location=MyCity");

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            TestPoco testPoco = (TestPoco)await triggerData.ValueProvider.GetValueAsync();
            Assert.Equal("FirstName LastName", testPoco.Name);
            Assert.Equal("MyCity", testPoco.Location);
        }
        
        [Fact]
        public async Task BindAsync_Poco_FromQueryParameters_WithDifferentNaming()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunctionWithCustomName").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&custom_value=Mathew%20Charles&Location=Seattle");

            FunctionBindingContext functionContext = new FunctionBindingContext(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new ValueBindingContext(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(6, triggerData.BindingData.Count);
            Assert.Null(triggerData.BindingData[nameof(TestPocoWithDataMember.CustomValue)]);
            Assert.Equal("Mathew Charles", triggerData.BindingData["custom_value"]);
            Assert.Equal("Seattle", triggerData.BindingData[nameof(TestPocoWithDataMember.Location)]);

            TestPocoWithDataMember testPoco = (TestPocoWithDataMember)(await triggerData.ValueProvider.GetValueAsync());
            Assert.Equal("Mathew Charles", testPoco.CustomValue);
            Assert.Equal("Seattle", testPoco.Location);
        }

        [Fact]
        public async Task BindAsync_Poco_FromRouteParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, true);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc");

            Dictionary<string, object> routeData = new()
            {
                { "Name", "FirstName LastName" },
                { "Location", "MyCity" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            TestPoco testPoco = (TestPoco)await triggerData.ValueProvider.GetValueAsync();
            Assert.Equal("FirstName LastName", testPoco.Name);
            Assert.Equal("MyCity", testPoco.Location);
        }

        [Fact]
        public async Task BindAsync_Poco_MergedBindingData()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestPocoFunctionEx").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, true);

            JObject requestBody = new()
            {
                { "Name", "FirstName LastName" },
                { "Phone", "(425) 555-6666" }
            };

            HeaderDictionary headers = new() { ["Content-Type"] = "application/json" };

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Age=25", headers, requestBody.ToString());
            var services = new ServiceCollection();
            services.AddOptions();
            services.AddSingleton(Options.Create(new MvcOptions()));
            var formatter = new Mock<IInputFormatter>();
            formatter.Setup(f => f.ReadAsync(It.IsAny<InputFormatterContext>()))
                .ReturnsAsync((InputFormatterContext c) =>
                {
                    TextReader reader = c.ReaderFactory(c.HttpContext.Request.Body, Encoding.UTF8);
                    JsonSerializer serializer = new();
                    object result = serializer.Deserialize(reader, c.Metadata.ModelType);

                    return InputFormatterResult.Success(result);
                });

            services.AddMvcCore(o => o.InputFormatters.Add(formatter.Object));
            request.HttpContext.RequestServices = services.BuildServiceProvider();

            Dictionary<string, object> routeData = new()
            {
                { "Location", "MyCity" }
            };
            request.HttpContext.Items.Add(HttpExtensionConstants.AzureWebJobsHttpRouteDataKey, routeData);

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(9, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);
            Assert.Equal("(425) 555-6666", triggerData.BindingData["Phone"]);
            Assert.Equal("25", triggerData.BindingData["Age"]);

            TestPocoEx testPoco = (TestPocoEx)await triggerData.ValueProvider.GetValueAsync();
            Assert.Equal("FirstName LastName", testPoco.Name);
            Assert.Equal("MyCity", testPoco.Location);
            Assert.Equal("(425) 555-6666", testPoco.Phone);
            Assert.Equal(25, testPoco.Age);
        }

        [Fact]
        public async Task BindAsync_HttpRequest_FromRequestBody()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, false);

            // we intentionally do not send a content type on the request
            // to ensure that we can still extract binding data in such cases
            JObject requestBody = new()
            {
                { "Name", "FirstName LastName" },
                { "Location", "MyCity" }
            };
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", null, requestBody.ToString());

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            HttpRequest result = (HttpRequest)await triggerData.ValueProvider.GetValueAsync();
            Assert.Same(request, result);
        }

        [Fact]
        public async Task BindAsync_HttpRequest_FromQueryParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, false);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Name=FirstName%20LastName&Location=MyCity");

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            HttpRequest result = (HttpRequest)await triggerData.ValueProvider.GetValueAsync();
            Assert.Same(request, result);
        }

        [Fact]
        public async Task BindAsync_HttpRequestMessage_FromRequestBody()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestMessageFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, false);

            // we intentionally do not send a content type on the request
            // to ensure that we can still extract binding data in such cases
            JObject requestBody = new()
            {
                { "Name", "FirstName LastName" },
                { "Location", "MyCity" }
            };

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", null, requestBody.ToString());
            request.ContentType = "application/json";

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            HttpRequestMessage result = (HttpRequestMessage)await triggerData.ValueProvider.GetValueAsync();
            Assert.NotNull(result);

            var contentResult = await result.Content.ReadAsAsync<JObject>();
            Assert.Equal("FirstName LastName", contentResult["Name"]);
        }

        [Fact]
        public async Task BindAsync_HttpRequestMessage_FromQueryParameters()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestHttpRequestMessageFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, false);

            HttpRequest request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions/myfunc?code=abc123&Name=FirstName%20LastName&location=Hawaii&Location=Ohio&Location=MyCity");

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(5, triggerData.BindingData.Count);
            Assert.Equal("FirstName LastName", triggerData.BindingData["Name"]);
            Assert.Equal("MyCity", triggerData.BindingData["Location"]);

            HttpRequestMessage result = (HttpRequestMessage)await triggerData.ValueProvider.GetValueAsync();
            Assert.NotNull(result);
        }

        [Fact]
        public async Task BindAsync_String()
        {
            ParameterInfo parameterInfo = GetType().GetMethod("TestStringFunction").GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, false);

            HeaderDictionary headers = new() { ["Content-Type"] = "application/text" };
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", headers, "This is a test");

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(3, triggerData.BindingData.Count);

            string result = (string)await triggerData.ValueProvider.GetValueAsync();
            Assert.Equal("This is a test", result);
        }

        [Fact]
        public async Task BindAsync_Dynamic()
        {
            ParameterInfo parameterInfo = GetType().GetMethod(nameof(TestDynamicFunction)).GetParameters()[0];
            HttpTriggerAttributeBindingProvider.HttpTriggerBinding binding = new(new HttpTriggerAttribute(), parameterInfo, false);

            HeaderDictionary headers = new() { ["Content-Type"] = "application/json" };
            HttpRequest request = HttpTestHelpers.CreateHttpRequest("POST", "http://functions/myfunc?code=abc123", headers, "{ \"value\" : \"This is a test\" }");

            FunctionBindingContext functionContext = new(Guid.NewGuid(), CancellationToken.None);
            ValueBindingContext context = new(functionContext, CancellationToken.None);
            ITriggerData triggerData = await binding.BindAsync(request, context);

            Assert.Equal(4, triggerData.BindingData.Count);

            var result = (JObject)await triggerData.ValueProvider.GetValueAsync();
            Assert.Equal("This is a test", result["value"].ToString());
        }

        [Fact]
        public static void ApplyBindingData_Succeeds()
        {
            TestPocoEx poco = new();
            Dictionary<string, string> properties = new()
            {
                { "A", "123" },
                { "B", "456" },
                { "c", "789" }
            };
            Dictionary<string, object> bindingData = new()
            {
                { "name", "Ted" },
                { "Location", "MyCity" },
                { "Age", "25" },
                { "Readonly", "Test" },
                { "Properties", properties }
            };

            HttpTriggerAttributeBindingProvider.HttpTriggerBinding.ApplyBindingData(poco, bindingData);

            Assert.Equal("Ted", poco.Name);
            Assert.Equal("MyCity", poco.Location);
            Assert.Equal(25, poco.Age);  // verifies string was converted
            Assert.Null(poco.Readonly);
            Assert.Equal(3, poco.Properties.Count);
            foreach (var pair in properties)
            {
                Assert.Equal(pair.Value, poco.Properties[pair.Key]);
            }
        }
        
        [Fact]
        public static void ApplyBindingDataWithDataMember_Succeeds()
        {
            TestPocoWithDataMember poco = new TestPocoWithDataMember();
            Dictionary<string, object> bindingData = new Dictionary<string, object>()
            {
                { "Location", "Seattle" },
                { "custom_value", "25" }
            };

            HttpTriggerAttributeBindingProvider.HttpTriggerBinding.ApplyBindingData(poco, bindingData);

            Assert.Equal("Seattle", poco.Location);
            Assert.Equal("25", poco.CustomValue);
        }

        [Fact]
        public static void LazyBindingData_DelaysInstantiation()
        {
            int invokeCount = 0;
            Dictionary<string, object> bindingData = new()
            {
                { "d1", 1 },
                { "d2", 2 },
                { "d3", 3 }
            };
            Func<IReadOnlyDictionary<string, object>> factory = () =>
            {
                invokeCount++;
                return bindingData;
            };

            var lazyBindingData = new HttpTriggerAttributeBindingProvider.HttpTriggerBinding.LazyBindingData(factory);

            Assert.Equal(0, invokeCount);

            Assert.Equal(3, lazyBindingData.Count);

            Assert.Equal(1, invokeCount);
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

        public void TestPocoFunctionWithCustomName(TestPocoWithDataMember poco)
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

        public class BindsAllTypesTestData : IEnumerable<object[]>
        {
            private readonly List<object[]> _data =
            [
                [typeof(string), "12345", "12345"],
                [typeof(int), (int)12345, "12345"],
                [typeof(int?), (int)12345, "12345"],
                [typeof(int?), null, null],
                [typeof(decimal), new decimal(1.2345), "1.2345"],
                [typeof(double), (double)1.2345, "1.2345"],
                [typeof(float), 1.2345f, "1.2345"],
                [typeof(long), 12345L, "12345"],
                [typeof(Guid), new Guid("CD2C1638-1638-72D5-1638-DEADBEEF1638"), "CD2C1638-1638-72D5-1638-DEADBEEF1638"],
                [typeof(Guid?), new Guid("CD2C1638-1638-72D5-1638-DEADBEEF1638"), "CD2C1638-1638-72D5-1638-DEADBEEF1638"],
                [typeof(Guid?), null, null],
                [typeof(bool), true, "true"],
                [typeof(DateTime), new DateTime(2019, 8, 20), "2019-08-20"],
                [typeof(DateTime?), new DateTime(2019, 8, 20), "2019-08-20"],
                [typeof(DateTime?), null, null]
            ];

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        public class TestPoco
        {
            public string Name { get; set; }

            public string Location { get; set; }
        }

        public class TestPocoWithDataMember
        {
            [DataMember(Name = "custom_value")]
            public string CustomValue { get; set; }

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