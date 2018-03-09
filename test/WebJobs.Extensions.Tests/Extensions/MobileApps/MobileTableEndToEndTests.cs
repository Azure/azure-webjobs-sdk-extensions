// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.MobileApps;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.WindowsAzure.MobileServices;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.MobileApps
{
    [Trait("Category", "E2E")]
    public class MobileTableEndToEndTests
    {
        private const string TableName = "TestTable";
        private const string DefaultUri = "https://default/";
        private const string AttributeUri = "https://attribute/";
        private const string ConfigUri = "https://config/";
        private const string DefaultKey = "Default";
        private const string AttributeKey = "Attribute";
        private const string ConfigKey = "Config";

        [Fact]
        public async Task OutputBindings()
        {
            var serviceMock = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var tableJObjectMock = new Mock<IMobileServiceTable>(MockBehavior.Strict);
            var tablePocoMock = new Mock<IMobileServiceTable<TodoItem>>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetTable(TableName))
                .Returns(tableJObjectMock.Object);

            serviceMock
                .Setup(m => m.GetTable<TodoItem>())
                .Returns(tablePocoMock.Object);

            tableJObjectMock
                .Setup(t => t.InsertAsync(It.IsAny<JObject>()))
                .ReturnsAsync(new JObject());

            tablePocoMock
                .Setup(t => t.InsertAsync(It.IsAny<TodoItem>()))
                .Returns(Task.FromResult(0));

            // Also verify the default uri and null api key is used
            var factoryMock = new Mock<IMobileServiceClientFactory>(MockBehavior.Strict);
            factoryMock
                    .Setup(f => f.CreateClient(new Uri(DefaultUri), null))
                    .Returns(serviceMock.Object);

            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            await RunTestAsync("Outputs", factoryMock.Object, testTrace, includeDefaultKey: false);

            factoryMock.Verify(f => f.CreateClient(It.IsAny<Uri>(), It.IsAny<HttpMessageHandler[]>()), Times.Once());

            // parameters of type object are converted to JObject
            tableJObjectMock.Verify(m => m.InsertAsync(It.IsAny<JObject>()), Times.Exactly(14));
            tablePocoMock.Verify(t => t.InsertAsync(It.IsAny<TodoItem>()), Times.Exactly(7));
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("Outputs", testTrace.Events[0].Message);
            factoryMock.VerifyAll();
        }

        [Fact]
        public async Task ClientBinding()
        {
            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Verify the values from teh attribute are being used.
            var factoryMock = CreateMockFactory(new Uri(AttributeUri), AttributeKey);

            await RunTestAsync("Client", factoryMock.Object, testTrace);

            Assert.Equal("Client", testTrace.Events.Single().Message);
            factoryMock.VerifyAll();
        }

        private Mock<IMobileServiceClientFactory> CreateMockFactory(Uri expectedUri, string expectedApiKey)
        {
            var factoryMock = new Mock<IMobileServiceClientFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateClient(expectedUri, It.Is<HttpMessageHandler[]>(h => ((MobileServiceApiKeyHandler)h.Single()).ApiKey == expectedApiKey)))
                .Returns<Uri, HttpMessageHandler[]>((uri, h) => new MobileServiceClient(uri, h));

            return factoryMock;
        }

        [Fact]
        public async Task QueryBinding()
        {
            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Verify that we pick up from the config correctly
            var factoryMock = CreateMockFactory(new Uri(ConfigUri), ConfigKey);
            await RunTestAsync("Query", factoryMock.Object, testTrace, configUri: new Uri(ConfigUri), configKey: ConfigKey);

            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("Query", testTrace.Events[0].Message);
            factoryMock.VerifyAll();
        }

        [Fact]
        public async Task TableBindings()
        {
            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Verify that we pick up the defaults
            var factoryMock = CreateMockFactory(new Uri(DefaultUri), DefaultKey);
            await RunTestAsync("Table", factoryMock.Object, testTrace);

            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("Table", testTrace.Events[0].Message);
            factoryMock.VerifyAll();
        }

        [Fact]
        public async Task InputBindings()
        {
            var serviceMock = new Mock<IMobileServiceClient>(MockBehavior.Strict);
            var tableJObjectMock = new Mock<IMobileServiceTable>(MockBehavior.Strict);
            var tablePocoMock = new Mock<IMobileServiceTable<TodoItem>>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetTable(TableName))
                .Returns(tableJObjectMock.Object);

            serviceMock
                .Setup(m => m.GetTable<TodoItem>())
                .Returns(tablePocoMock.Object);

            tableJObjectMock
                .Setup(m => m.LookupAsync("item1"))
                .ReturnsAsync(JObject.FromObject(new { Id = "item1" }));

            tableJObjectMock
                .Setup(m => m.LookupAsync("triggerItem"))
                .ReturnsAsync(JObject.FromObject(new { Id = "triggerItem" }));

            tableJObjectMock
                .Setup(m => m.UpdateAsync(It.Is<JObject>(j => j["Id"].ToString() == "triggerItem")))
                .ReturnsAsync(new JObject());

            tablePocoMock
                .Setup(m => m.LookupAsync("item3"))
                .ReturnsAsync(new TodoItem { Id = "item3" });

            tablePocoMock
                .Setup(m => m.LookupAsync("triggerItem"))
                .ReturnsAsync(new TodoItem { Id = "triggerItem" });

            var factoryMock = new Mock<IMobileServiceClientFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateClient(new Uri(DefaultUri), null))
                .Returns(serviceMock.Object);

            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);
            await RunTestAsync("Inputs", factoryMock.Object, testTrace, "triggerItem", includeDefaultKey: false);

            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("Inputs", testTrace.Events[0].Message);
        }

        [Fact]
        public async Task ApiKey()
        {
            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Set up mocks for the three flavors of ApiKey:
            var defaultUri = new Uri(DefaultUri);
            var factoryMock = new Mock<IMobileServiceClientFactory>(MockBehavior.Strict);
            int nullCalled = 0;
            int emptyCalled = 0;
            int keyCalled = 0;

            // if ApiKey is null, use the DefaultKey
            factoryMock
                .Setup(f => f.CreateClient(defaultUri, It.Is<HttpMessageHandler[]>(h => h == null ? false : ((MobileServiceApiKeyHandler)h.Single()).ApiKey == DefaultKey)))
                .Callback(() => nullCalled++)
                .Returns<Uri, HttpMessageHandler[]>((uri, h) => new MobileServiceClient(uri, h));

            // if ApiKey is an empty string, do not use any key
            factoryMock
                .Setup(f => f.CreateClient(defaultUri, null))
                .Callback(() => emptyCalled++)
                .Returns<Uri, HttpMessageHandler[]>((uri, h) => new MobileServiceClient(uri, null));

            // otherwise, use the specified key after resolving it
            factoryMock
                .Setup(f => f.CreateClient(defaultUri, It.Is<HttpMessageHandler[]>(h => h == null ? false : ((MobileServiceApiKeyHandler)h.Single()).ApiKey == AttributeKey)))
                .Callback(() => keyCalled++)
                .Returns<Uri, HttpMessageHandler[]>((uri, h) => new MobileServiceClient(uri, h));

            await RunTestAsync("ApiKey", factoryMock.Object, testTrace);

            Assert.Equal(1, nullCalled);
            Assert.Equal(1, emptyCalled);
            Assert.Equal(1, keyCalled);
        }

        [Fact]
        public async Task BrokenTableBinding()
        {
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(() => IndexBindings(typeof(MobileTableBrokenTable)));
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public async Task BrokenItemBinding()
        {
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(() => IndexBindings(typeof(MobileTableBrokenItem)));
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public async Task BrokenQueryBinding()
        {
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(() => IndexBindings(typeof(MobileTableBrokenQuery)));
            Assert.IsType<ArgumentException>(ex.InnerException);
        }

        [Fact]
        public async Task NoUri()
        {
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(() => IndexBindings(typeof(MobileTableNoUri), includeDefaultUri: false));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task NoId()
        {
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(() => IndexBindings(typeof(MobileTableNoId)));
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.StartsWith("'Id' must be set", ex.InnerException.Message);
        }

        [Fact]
        public async Task ObjectWithNoTable()
        {
            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Verify that we pick up from the config correctly
            var factoryMock = CreateMockFactory(new Uri(ConfigUri), ConfigKey);

            var exception = await Assert.ThrowsAsync<FunctionInvocationException>(
                () => RunTestAsync(nameof(MobileTableEndToEndFunctions.NoTable), factoryMock.Object, testTrace, configUri: new Uri(ConfigUri), configKey: ConfigKey));

            Assert.IsType<InvalidOperationException>(exception.InnerException.InnerException);
            Assert.Equal("The table name must be specified.", exception.InnerException.InnerException.Message);
            factoryMock.VerifyAll();
        }

        private async Task IndexBindings(Type testType, bool includeDefaultUri = true)
        {
            // Just start the jobhost -- this should fail if function indexing fails.

            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            var nameResolver = new TestNameResolver();
            if (includeDefaultUri)
            {
                nameResolver.Values.Add(MobileAppsConfiguration.AzureWebJobsMobileAppUriName, "https://default");
            }
            JobHostConfiguration config = new JobHostConfiguration
            {
                NameResolver = nameResolver,
                TypeLocator = locator,
            };

            config.UseMobileApps();

            JobHost host = new JobHost(config);

            await host.StartAsync();
            await host.StopAsync();
        }

        private async Task RunTestAsync(string testName, IMobileServiceClientFactory factory, TestTraceWriter testTrace, object argument = null,
            Uri configUri = null, string configKey = null, bool includeDefaultKey = true, bool includeDefaultUri = true, Type testType = null)
        {
            testType = testType ?? typeof(MobileTableEndToEndFunctions);
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
            };

            config.Tracing.Tracers.Add(testTrace);

            var arguments = new Dictionary<string, object>();
            arguments.Add("triggerData", argument);

            var mobileAppsConfig = new MobileAppsConfiguration
            {
                MobileAppUri = configUri,
                ApiKey = configKey,
                ClientFactory = factory
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("MyUri", AttributeUri);
            resolver.Values.Add("MyKey", AttributeKey);
            if (includeDefaultUri)
            {
                resolver.Values.Add(MobileAppsConfiguration.AzureWebJobsMobileAppUriName, DefaultUri);
            }
            if (includeDefaultKey)
            {
                resolver.Values.Add(MobileAppsConfiguration.AzureWebJobsMobileAppApiKeyName, DefaultKey);
            }

            config.NameResolver = resolver;

            config.UseMobileApps(mobileAppsConfig);

            JobHost host = new JobHost(config);

            await host.StartAsync();
            testTrace.Events.Clear();
            await host.CallAsync(testType.GetMethod(testName), arguments);
            await host.StopAsync();
        }

        private class MobileTableEndToEndFunctions
        {
            [NoAutomaticTrigger]
            public static void Outputs(
                [MobileTable(TableName = TableName)] out JObject newJObject,
                [MobileTable(TableName = TableName)] out JObject[] arrayJObject,
                [MobileTable(TableName = TableName)] IAsyncCollector<JObject> asyncCollectorJObject,
                [MobileTable(TableName = TableName)] ICollector<JObject> collectorJObject,
                [MobileTable] out TodoItem newPoco,
                [MobileTable] out TodoItem[] arrayPoco,
                [MobileTable] IAsyncCollector<TodoItem> asyncCollectorPoco,
                [MobileTable] ICollector<TodoItem> collectorPoco,
                [MobileTable(TableName = TableName)] out object newObject, // we explicitly allow object
                [MobileTable(TableName = TableName)] out object[] arrayObject,
                [MobileTable(TableName = TableName)] IAsyncCollector<object> asyncCollectorObject,
                [MobileTable(TableName = TableName)] ICollector<object> collectorObject,
                TraceWriter trace)
            {
                newJObject = new JObject();
                arrayJObject = new[]
                {
                    new JObject(),
                    new JObject()
                };
                Task.WaitAll(new[]
                {
                    asyncCollectorJObject.AddAsync(new JObject()),
                    asyncCollectorJObject.AddAsync(new JObject())
                });
                collectorJObject.Add(new JObject());
                collectorJObject.Add(new JObject());

                newPoco = new TodoItem();
                arrayPoco = new[]
                {
                    new TodoItem(),
                    new TodoItem()
                };
                Task.WaitAll(new[]
                {
                    asyncCollectorPoco.AddAsync(new TodoItem()),
                    asyncCollectorPoco.AddAsync(new TodoItem())
                });
                collectorPoco.Add(new TodoItem());
                collectorPoco.Add(new TodoItem());

                newObject = new { };
                arrayObject = new[]
                {
                    new { },
                    new { }
                };
                Task.WaitAll(new[]
                {
                    asyncCollectorObject.AddAsync(new TodoItem()),
                    asyncCollectorObject.AddAsync(new TodoItem())
                });
                collectorObject.Add(new { });
                collectorObject.Add(new { });

                trace.Warning("Outputs");
            }

            [NoAutomaticTrigger]
            public static void Client(
                [MobileTable(MobileAppUriSetting = "MyUri", ApiKeySetting = "MyKey")] IMobileServiceClient client,
                TraceWriter trace)
            {
                Assert.NotNull(client);
                trace.Warning("Client");
            }

            [NoAutomaticTrigger]
            public static void Query(
                [MobileTable] IMobileServiceTableQuery<TodoItem> query,
                TraceWriter trace)
            {
                Assert.NotNull(query);
                trace.Warning("Query");
            }

            [NoAutomaticTrigger]
            public static void Table(
                [MobileTable(TableName = TableName)] IMobileServiceTable tableJObject,
                [MobileTable] IMobileServiceTable<TodoItem> tablePoco,
                TraceWriter trace)
            {
                Assert.NotNull(tableJObject);
                Assert.NotNull(tablePoco);
                trace.Warning("Table");
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("fakequeue1")] string triggerData,
                [MobileTable(TableName = TableName, Id = "item1")] JObject item1,
                [MobileTable(TableName = TableName, Id = "{QueueTrigger}")] JObject item2,
                [MobileTable(Id = "item3")] TodoItem item3,
                [MobileTable(Id = "{QueueTrigger}")] TodoItem item4,
                TraceWriter trace)
            {
                Assert.NotNull(item1);
                Assert.NotNull(item2);
                Assert.NotNull(item3);
                Assert.NotNull(item4);

                // only modify item2
                item2["Text"] = "changed";

                trace.Warning("Inputs");
            }

            [NoAutomaticTrigger]
            public static void TriggerObject(
                [QueueTrigger("fakequeue1")] QueueData triggerData,
                [MobileTable(Id = "{RecordId}")] TodoItem item)
            {
                Assert.NotNull(item);
            }

            [NoAutomaticTrigger]
            public static void ApiKey(
                [MobileTable(ApiKeySetting = null)] IMobileServiceClient client1,
                [MobileTable(ApiKeySetting = "")] IMobileServiceClient client2,
                [MobileTable(ApiKeySetting = "MyKey")] IMobileServiceClient client3)
            {
            }

            // This will fail on invocation because TableName is not specified
            [NoAutomaticTrigger]
            public static void NoTable(
                [MobileTable] out object item)
            {
                item = new { Text = "hello!" };
            }
        }

        private class MobileTableBrokenTable
        {
            public static void Broken(
                [MobileTable] IMobileServiceTable<NoId> table)
            {
            }
        }

        private class MobileTableBrokenItem
        {
            public static void Broken(
                [MobileTable] NoId item)
            {
            }
        }

        private class MobileTableBrokenQuery
        {
            public static void Broken(
                [MobileTable] IMobileServiceTableQuery<NoId> query)
            {
            }
        }

        private class MobileTableNoUri
        {
            public static void Broken(
                [MobileTable] IMobileServiceClient client)
            {
            }
        }

        private class MobileTableNoId
        {
            public static void Broken(
                [MobileTable] TodoItem item)
            {
            }
        }

        private class QueueData
        {
            public string RecordId { get; set; }
        }
    }
}
