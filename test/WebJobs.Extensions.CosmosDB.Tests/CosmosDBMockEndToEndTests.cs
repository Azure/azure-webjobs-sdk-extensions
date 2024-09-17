// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBMockEndToEndTests
    {
        private const string DatabaseName = "TestDatabase";
        private const string CollectionName = "TestCollection";

        private const string AttributeConnStr = "AccountEndpoint=https://attribute;AccountKey=YXR0cmlidXRl;";
        private const string DefaultConnStr = "AccountEndpoint=https://default;AccountKey=ZGVmYXVsdA==;";

        private static readonly IConfiguration _baseConfig = CosmosDBTestUtility.BuildConfiguration(new List<Tuple<string, string>>()
        {
            Tuple.Create(Constants.DefaultConnectionStringName, DefaultConnStr),
            Tuple.Create("MyConnectionString", AttributeConnStr)
        });

        private readonly ILoggerFactory _loggerFactory = new LoggerFactory();
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        public CosmosDBMockEndToEndTests()
        {
            _loggerFactory.AddProvider(_loggerProvider);
        }

        [Fact]
        public async Task OutputBindings()
        {
            // Arrange
            var serviceMock = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(m => m.UpsertItemAsync<object>(It.IsAny<object>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((object item, PartitionKey? partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<object>> mockResponse = new Mock<ItemResponse<object>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(item);

                    return mockResponse.Object;
                });

            mockContainer
                .Setup(m => m.UpsertItemAsync<JObject>(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((JObject item, PartitionKey? partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<JObject>> mockResponse = new Mock<ItemResponse<JObject>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(item);

                    return mockResponse.Object;
                });

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(Constants.DefaultConnectionStringName, It.IsAny<CosmosClientOptions>()))
                .Returns(serviceMock.Object);

            //Act
            await RunTestAsync("Outputs", factoryMock.Object);

            // Assert
            factoryMock.Verify(f => f.CreateService(Constants.DefaultConnectionStringName, It.IsAny<CosmosClientOptions>()), Times.Once());
            mockContainer.Verify(m => m.UpsertItemAsync<object>(It.IsAny<object>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(7));
            mockContainer.Verify(m => m.UpsertItemAsync<JObject>(It.IsAny<JObject>(), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(1));
            Assert.Equal("Outputs", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
        }

        [Fact]
        public async Task ClientBinding()
        {
            // Arrange
            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(Constants.DefaultConnectionStringName, It.IsAny<CosmosClientOptions>()))
                .Returns<string, CosmosClientOptions>((connectionString, connectionPolicy) => new CosmosClient(DefaultConnStr, connectionPolicy));

            // Act
            // Also verify that this falls back to the default by setting the config connection string to null
            await RunTestAsync("Client", factoryMock.Object);

            //Assert
            factoryMock.Verify(f => f.CreateService(Constants.DefaultConnectionStringName, It.IsAny<CosmosClientOptions>()), Times.Once());
            Assert.Equal("Client", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
        }

        [Fact]
        public async Task InputBindings()
        {
            // Arrange
            string item1Id = "docid1";
            string item2Id = "docid2";
            string item3Id = "docid3";
            string item4Id = "docid4";
            string item5Id = "docid5";

            string options2 = string.Format("[\"{0}\"]", item1Id); // this comes from the trigger
            string options3 = "[\"partkey3\"]";

            var serviceMock = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetContainer(It.Is<string>(d => d == "ResolvedDatabase"), It.Is<string>(c => c == "ResolvedCollection")))
                .Returns(mockContainer.Object);

            serviceMock
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(id => id == item1Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(new Item { Id = item1Id });

                    return mockResponse.Object;
                });

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(id => id == item2Id), It.Is<PartitionKey>(pk => pk.ToString() == options2), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(new Item { Id = item2Id });

                    return mockResponse.Object;
                });

            mockContainer
                .Setup(m => m.ReadItemAsync<Item>(It.Is<string>(id => id == item3Id), It.Is<PartitionKey>(pk => pk.ToString() == options3), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(new Item { Id = item3Id });

                    return mockResponse.Object;
                });

            mockContainer
                .Setup(m => m.ReadItemAsync<JObject>(It.Is<string>(id => id == item4Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<JObject>> mockResponse = new Mock<ItemResponse<JObject>>();
                    JObject item = new JObject();
                    item["Id"] = item4Id;
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(item);

                    return mockResponse.Object;
                });

            mockContainer
                .Setup(m => m.ReadItemAsync<JObject>(It.Is<string>(id => id == item5Id), It.IsAny<PartitionKey>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<JObject>> mockResponse = new Mock<ItemResponse<JObject>>();
                    JObject item = new JObject();
                    item["Id"] = item5Id;
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(item);

                    return mockResponse.Object;
                });

            mockContainer
                .Setup(m => m.GetItemQueryIterator<JObject>(
                    It.IsAny<QueryDefinition>(),
                    It.IsAny<string>(),
                    It.IsAny<QueryRequestOptions>()))
                .Returns((QueryDefinition a, string b, QueryRequestOptions c) =>
                {
                    Mock<FeedIterator<JObject>> mockIterator = new Mock<FeedIterator<JObject>>();
                    mockIterator.SetupSequence(m => m.HasMoreResults).Returns(true).Returns(false);
                    mockIterator
                        .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Mock.Of<FeedResponse<JObject>>());

                    return mockIterator.Object;
                });

            mockContainer
                .Setup(m => m.GetItemQueryIterator<JToken>(
                    It.IsAny<QueryDefinition>(),
                    It.IsAny<string>(),
                    It.IsAny<QueryRequestOptions>()))
                .Returns((QueryDefinition a, string b, QueryRequestOptions c) =>
                {
                    Mock<FeedIterator<JToken>> mockIterator = new Mock<FeedIterator<JToken>>();
                    mockIterator.SetupSequence(m => m.HasMoreResults).Returns(true).Returns(false);
                    mockIterator
                        .Setup(m => m.ReadNextAsync(It.IsAny<CancellationToken>()))
                        .ReturnsAsync(Mock.Of<FeedResponse<JToken>>());

                    return mockIterator.Object;
                });

            // We only expect item2 to be updated
            mockContainer
                .Setup(m => m.ReplaceItemAsync<Item>(It.Is<Item>(i => i.Id == item2Id), It.Is<string>(id => id == item2Id), It.IsAny<PartitionKey?>(), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Item item, string id, PartitionKey? partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<Item>> mockResponse = new Mock<ItemResponse<Item>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(item);

                    return mockResponse.Object;
                });

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(It.IsAny<string>(), It.IsAny<CosmosClientOptions>()))
                .Returns(serviceMock.Object);

            // Act
            await RunTestAsync(nameof(CosmosDBEndToEndFunctions.Inputs), factoryMock.Object, item1Id);

            // Assert
            factoryMock.Verify(f => f.CreateService(It.IsAny<string>(), It.IsAny<CosmosClientOptions>()), Times.Once());
            Assert.Equal("Inputs", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
            serviceMock.VerifyAll();

            mockContainer
               .Verify(m => m.GetItemQueryIterator<JObject>(
                   It.Is<QueryDefinition>(qd => qd != null && (qd.QueryText == "some query" || qd.QueryText == "some ResolvedQuery with '@QueueTrigger' replacements")),
                   It.IsAny<string>(),
                   It.Is<QueryRequestOptions>(ro => !ro.PartitionKey.HasValue)), Times.Exactly(2));

            mockContainer
               .Verify(m => m.GetItemQueryIterator<JObject>(
                   It.Is<QueryDefinition>(qd => qd == null),
                   It.IsAny<string>(),
                   It.Is<QueryRequestOptions>(ro => ro.PartitionKey == new PartitionKey(item1Id))), Times.Once);

            mockContainer
               .Verify(m => m.GetItemQueryIterator<JToken>(
                   It.Is<QueryDefinition>(qd => qd == null),
                   It.IsAny<string>(),
                   It.Is<QueryRequestOptions>(ro => !ro.PartitionKey.HasValue)), Times.Once);
        }

        [Fact]
        public async Task TriggerObject()
        {
            // Arrange
            string itemId = "docid1";

            string key = "[\"partkey1\"]";

            var serviceMock = new Mock<CosmosClient>(MockBehavior.Strict);

            var mockContainer = new Mock<Container>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.GetContainer(It.Is<string>(d => d == DatabaseName), It.Is<string>(c => c == CollectionName)))
                .Returns(mockContainer.Object);

            mockContainer
                .Setup(m => m.ReadItemAsync<dynamic>(It.Is<string>(id => id == itemId), It.Is<PartitionKey>(pk => pk.ToString() == key), It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, PartitionKey partitionKey, ItemRequestOptions itemRequestOptions, CancellationToken cancellationToken) =>
                {
                    Mock<ItemResponse<dynamic>> mockResponse = new Mock<ItemResponse<dynamic>>();
                    mockResponse
                        .Setup(m => m.Resource)
                        .Returns(new { id = itemId });

                    return mockResponse.Object;
                });

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService("MyConnectionString", It.IsAny<CosmosClientOptions>()))
                .Returns(serviceMock.Object);

            var jobject = JObject.FromObject(new QueueData { DocumentId = "docid1", PartitionKey = "partkey1" });

            // Act
            await RunTestAsync(nameof(CosmosDBEndToEndFunctions.TriggerObject), factoryMock.Object, jobject.ToString());

            // Assert
            factoryMock.Verify(f => f.CreateService("MyConnectionString", It.IsAny<CosmosClientOptions>()), Times.Once());
            Assert.Equal("TriggerObject", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
        }

        [Fact]
        public async Task NoConnectionStringSet()
        {
            // Act
            var ex = await Assert.ThrowsAsync<FunctionInvocationException>(
                () => RunTestAsync(typeof(NoConnectionString), nameof(NoConnectionString.Broken), new DefaultCosmosDBServiceFactory(Mock.Of<IConfiguration>(), Mock.Of<AzureComponentFactory>()), includeDefaultConnectionString: false));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task InvalidEnumerableBindings()
        {
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(InvalidEnumerable), nameof(InvalidEnumerable.BrokenEnumerable), new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>())));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);

            // TODO: Can WhenIsNull/NotNull provide better error messages?
            Assert.StartsWith("Can't bind CosmosDB to type 'System.Collections.Generic.IEnumerable`1[Newtonsoft.Json.Linq.JObject]'.", ex.InnerException.Message);
        }

        [Fact]
        public async Task InvalidItemBindings()
        {
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(InvalidItem), nameof(InvalidItem.BrokenItem), new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>())));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);

            // TODO: Can WhenIsNull/NotNull provide better error messages?
            Assert.StartsWith("Can't bind CosmosDB to type 'Newtonsoft.Json.Linq.JObject'.", ex.InnerException.Message);
        }

        [Fact]
        public async Task NoByteArrayEnumerableBindings()
        {
            // byte[] isn't supported by DocumentClient, so we need to make sure we reject it early.
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(InvalidByteArrayEnumerable), nameof(InvalidByteArrayEnumerable.BrokenEnumerable), new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>())));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.StartsWith("Can't bind CosmosDB to type 'System.Collections.Generic.IEnumerable`1[System.Byte[]]'.", ex.InnerException.Message);
        }

        [Fact]
        public async Task NoByteArrayItemBindings()
        {
            // byte[] isn't supported by DocumentClient, so we need to make sure we reject it early.
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(InvalidByteArrayItem), nameof(InvalidByteArrayItem.BrokenItem), new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>())));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.StartsWith("Can't bind CosmosDB to type 'System.Byte[]'.", ex.InnerException.Message);
        }

        [Fact]
        public async Task NoByteArrayCollectorBindings()
        {
            // byte[] isn't supported by DocumentClient, so we need to make sure we reject it early.
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(InvalidByteArrayCollector), nameof(InvalidByteArrayCollector.BrokenCollector), new DefaultCosmosDBServiceFactory(_baseConfig, Mock.Of<AzureComponentFactory>())));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.StartsWith("Nested collections are not supported", ex.InnerException.Message);
        }

        private Task RunTestAsync(string testName, ICosmosDBServiceFactory factory, object argument = null)
        {
            return RunTestAsync(typeof(CosmosDBEndToEndFunctions), testName, factory, argument);
        }

        private async Task RunTestAsync(Type testType, string testName, ICosmosDBServiceFactory factory, object argument = null, bool includeDefaultConnectionString = true)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);

            var arguments = new Dictionary<string, object>
            {
                { "triggerData", argument }
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("Database", "ResolvedDatabase");
            resolver.Values.Add("Collection", "ResolvedCollection");
            resolver.Values.Add("Query", "ResolvedQuery");

            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder.AddAzureStorage()
                    .AddCosmosDB();
                })
                .ConfigureAppConfiguration(c =>
                {
                    c.Sources.Clear();
                    if (includeDefaultConnectionString)
                    {
                        c.AddInMemoryCollection(new Dictionary<string, string>
                        {
                            { $"ConnectionStrings:{Constants.DefaultConnectionStringName}", DefaultConnStr },
                            { ConnectionStringNames.Storage, "UseDevelopmentStorage=true" },
                            { "MyConnectionString", AttributeConnStr }
                        });
                    }
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ICosmosDBServiceFactory>(factory);
                    services.AddSingleton<INameResolver>(resolver);
                    services.AddSingleton<ITypeLocator>(locator);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(_loggerProvider);
                })
                .Build();

            await host.StartAsync();
            await host.GetJobHost().CallAsync(testType.GetMethod(testName), arguments);
            await host.StopAsync();
        }

        private class CosmosDBEndToEndFunctions
        {
            [NoAutomaticTrigger]
            public static void Outputs(
                [CosmosDB(DatabaseName, CollectionName)] out object newItem,
                [CosmosDB(DatabaseName, CollectionName)] out string newItemString,
                [CosmosDB(DatabaseName, CollectionName)] out object[] arrayItem,
                [CosmosDB(DatabaseName, CollectionName)] IAsyncCollector<object> asyncCollector,
                [CosmosDB(DatabaseName, CollectionName)] ICollector<object> collector,
                TraceWriter trace)
            {
                newItem = new { };

                newItemString = "{}";

                arrayItem = new Item[]
                {
                    new Item(),
                    new Item()
                };

                Task.WaitAll(new[]
                {
                    asyncCollector.AddAsync(new { }),
                    asyncCollector.AddAsync(new { })
                });

                collector.Add(new { });
                collector.Add(new { });

                trace.Warning("Outputs");
            }

            [NoAutomaticTrigger]
            public static void Client(
                [CosmosDB] CosmosClient client,
                TraceWriter trace)
            {
                Assert.NotNull(client);

                trace.Warning("Client");
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("fakequeue1")] string triggerData,
                [CosmosDB(DatabaseName, CollectionName, Id = "{QueueTrigger}")] Item item1,
                [CosmosDB(DatabaseName, CollectionName, Id = "docid2", PartitionKey = "{QueueTrigger}")] Item item2,
                [CosmosDB(DatabaseName, CollectionName, Id = "docid3", PartitionKey = "partkey3")] Item item3,
                [CosmosDB("%Database%", "%Collection%", Id = "docid4")] JObject item4,
                [CosmosDB(DatabaseName, CollectionName, Id = "docid5")] string item5,
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "some query")] IEnumerable<JObject> query1,
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "some %Query% with '{QueueTrigger}' replacements")] IEnumerable<JObject> query2,
                [CosmosDB(DatabaseName, CollectionName)] JArray query3,
                [CosmosDB(DatabaseName, CollectionName, PartitionKey = "{QueueTrigger}")] IEnumerable<JObject> query4,
                TraceWriter trace)
            {
                Assert.NotNull(item1);
                Assert.NotNull(item2);
                Assert.NotNull(item3);
                Assert.NotNull(item4);
                Assert.NotNull(item5);
                Assert.NotNull(query1);
                Assert.NotNull(query2);
                Assert.NotNull(query3);
                Assert.NotNull(query4);

                // add some value to item2
                item2.Text = "changed";

                trace.Warning("Inputs");
            }

            [NoAutomaticTrigger]
            public static void TriggerObject(
                [QueueTrigger("fakequeue1")] QueueData triggerData,
                [CosmosDB(DatabaseName, CollectionName, Id = "{DocumentId}", PartitionKey = "{PartitionKey}", Connection = "MyConnectionString")] dynamic item1,
                TraceWriter trace)
            {
                Assert.NotNull(item1);

                trace.Warning("TriggerObject");
            }
        }

        private class NoConnectionString
        {
            [NoAutomaticTrigger]
            public static void Broken(
                [CosmosDB] CosmosClient client)
            {
            }
        }

        private class InvalidEnumerable
        {
            [NoAutomaticTrigger]
            public static void BrokenEnumerable(
                [CosmosDB(Id = "Some_Id")] IEnumerable<JObject> inputs)
            {
            }
        }

        private class InvalidItem
        {
            [NoAutomaticTrigger]
            public static void BrokenItem(
                [CosmosDB(SqlQuery = "some query")] JObject item)
            {
            }
        }

        private class InvalidByteArrayEnumerable
        {
            [NoAutomaticTrigger]
            [Disable]
            public static void BrokenEnumerable(
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "some query")] IEnumerable<byte[]> items)
            {
            }
        }

        private class InvalidByteArrayItem
        {
            [NoAutomaticTrigger]
            [Disable]
            public static void BrokenItem(
                [CosmosDB(DatabaseName, CollectionName, Id = "id")] byte[] item)
            {
            }
        }

        private class InvalidByteArrayCollector
        {
            [NoAutomaticTrigger]
            public static void BrokenCollector(
            [CosmosDB(DatabaseName, CollectionName)] IAsyncCollector<byte[]> items)
            {
            }
        }

        private class QueueData
        {
            public string DocumentId { get; set; }

            public string PartitionKey { get; set; }
        }
    }
}
