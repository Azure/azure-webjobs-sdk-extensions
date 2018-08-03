// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
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
        private const string ConfigConnStr = "AccountEndpoint=https://config;AccountKey=Y29uZmln;";
        private const string DefaultConnStr = "AccountEndpoint=https://default;AccountKey=ZGVmYXVsdA==;";

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
            var serviceMock = new Mock<ICosmosDBService>(MockBehavior.Strict);
            serviceMock
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .Returns<Uri, object>((uri, item) =>
                {
                    // Simulate what DocumentClient does. This will throw an error if a string
                    // is directly passed as the item. We can't use DocumentClient directly for this
                    // because it requires a real connection, but we're mocking here.
                    JObject jObject = JObject.FromObject(item);

                    return Task.FromResult(new Document());
                });

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr, null, null))
                .Returns(serviceMock.Object);

            //Act
            await RunTestAsync("Outputs", factoryMock.Object);

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr, null, null), Times.Once());
            serviceMock.Verify(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()), Times.Exactly(8));
            Assert.Equal("Outputs", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
        }

        [Fact]
        public async Task ClientBinding()
        {
            // Arrange
            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(DefaultConnStr, null, null))
                .Returns<string, ConnectionMode?, Protocol?>((connectionString, connectionMode, protocol) => new CosmosDBService(connectionString, connectionMode, protocol));

            // Act
            // Also verify that this falls back to the default by setting the config connection string to null
            await RunTestAsync("Client", factoryMock.Object, configConnectionString: null);

            //Assert
            factoryMock.Verify(f => f.CreateService(DefaultConnStr, null, null), Times.Once());
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
            Uri item1Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item1Id);
            Uri item2Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item2Id);
            Uri item3Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item3Id);
            Uri item4Uri = UriFactory.CreateDocumentUri("ResolvedDatabase", "ResolvedCollection", item4Id);
            Uri item5Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item5Id);
            Uri collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);

            string options2 = string.Format("[\"{0}\"]", item1Id); // this comes from the trigger
            string options3 = "[\"partkey3\"]";

            var serviceMock = new Mock<ICosmosDBService>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.ReadDocumentAsync(item1Uri, null))
                .ReturnsAsync(new Document { Id = item1Id });

            serviceMock
               .Setup(m => m.ReadDocumentAsync(item2Uri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == options2)))
               .ReturnsAsync(new Document { Id = item2Id });

            serviceMock
                .Setup(m => m.ReadDocumentAsync(item3Uri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == options3)))
                .ReturnsAsync(new Document { Id = item3Id });

            serviceMock
                .Setup(m => m.ReadDocumentAsync(item4Uri, null))
                .ReturnsAsync(new Document { Id = item4Id });

            serviceMock
                .Setup(m => m.ReadDocumentAsync(item5Uri, null))
                .ReturnsAsync(new Document { Id = item5Id });

            serviceMock
                .Setup(m => m.ExecuteNextAsync<JObject>(
                    collectionUri,
                    It.Is<SqlQuerySpec>((s) =>
                        s.QueryText == "some query" &&
                        s.Parameters.Count() == 0),
                    null))
                .ReturnsAsync(new DocumentQueryResponse<JObject>());

            serviceMock
                .Setup(m => m.ExecuteNextAsync<JObject>(
                    collectionUri,
                    It.Is<SqlQuerySpec>((s) =>
                        s.QueryText == "some ResolvedQuery with '@QueueTrigger' replacements" &&
                        s.Parameters.Count() == 1 &&
                        s.Parameters[0].Name == "@QueueTrigger" &&
                        s.Parameters[0].Value.ToString() == "docid1"),
                    null))
                .ReturnsAsync(new DocumentQueryResponse<JObject>());

            serviceMock
                .Setup(m => m.ExecuteNextAsync<JObject>(
                    collectionUri,
                    It.Is<SqlQuerySpec>((s) =>
                        s.QueryText == null &&
                        s.Parameters.Count() == 0),
                    null))
                .ReturnsAsync(new DocumentQueryResponse<JObject>());

            // We only expect item2 to be updated
            serviceMock
                .Setup(m => m.ReplaceDocumentAsync(item2Uri, It.Is<object>(d => ((Document)d).Id == item2Id)))
                .ReturnsAsync(new Document());

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(It.IsAny<string>(), null, null))
                .Returns(serviceMock.Object);

            // Act
            await RunTestAsync(nameof(CosmosDBEndToEndFunctions.Inputs), factoryMock.Object, item1Id);

            // Assert
            factoryMock.Verify(f => f.CreateService(It.IsAny<string>(), null, null), Times.Once());
            Assert.Equal("Inputs", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
            serviceMock.VerifyAll();
        }

        [Fact]
        public async Task TriggerObject()
        {
            // Arrange
            string itemId = "docid1";
            Uri itemUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, itemId);

            string key = "[\"partkey1\"]";

            var serviceMock = new Mock<ICosmosDBService>(MockBehavior.Strict);

            serviceMock
               .Setup(m => m.ReadDocumentAsync(itemUri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == key)))
               .ReturnsAsync(new Document { Id = itemId });

            var factoryMock = new Mock<ICosmosDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(AttributeConnStr, null, null))
                .Returns(serviceMock.Object);

            var jobject = JObject.FromObject(new QueueData { DocumentId = "docid1", PartitionKey = "partkey1" });

            // Act
            await RunTestAsync(nameof(CosmosDBEndToEndFunctions.TriggerObject), factoryMock.Object, jobject.ToString());

            // Assert
            factoryMock.Verify(f => f.CreateService(AttributeConnStr, null, null), Times.Once());
            Assert.Equal("TriggerObject", _loggerProvider.GetAllUserLogMessages().Single().FormattedMessage);
        }

        [Fact]
        public async Task NoConnectionStringSet()
        {
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(NoConnectionString), nameof(NoConnectionString.Broken), new DefaultCosmosDBServiceFactory(), configConnectionString: null, includeDefaultConnectionString: false));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task InvalidEnumerableBindings()
        {
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(InvalidEnumerable), nameof(InvalidEnumerable.BrokenEnumerable), new DefaultCosmosDBServiceFactory()));

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
                () => RunTestAsync(typeof(InvalidItem), nameof(InvalidItem.BrokenItem), new DefaultCosmosDBServiceFactory()));

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
                () => RunTestAsync(typeof(InvalidByteArrayEnumerable), nameof(InvalidByteArrayEnumerable.BrokenEnumerable), new DefaultCosmosDBServiceFactory()));

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
                () => RunTestAsync(typeof(InvalidByteArrayItem), nameof(InvalidByteArrayItem.BrokenItem), new DefaultCosmosDBServiceFactory()));

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
                () => RunTestAsync(typeof(InvalidByteArrayCollector), nameof(InvalidByteArrayCollector.BrokenCollector), new DefaultCosmosDBServiceFactory()));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.StartsWith("Nested collections are not supported", ex.InnerException.Message);
        }

        private Task RunTestAsync(string testName, ICosmosDBServiceFactory factory, object argument = null, string configConnectionString = ConfigConnStr)
        {
            return RunTestAsync(typeof(CosmosDBEndToEndFunctions), testName, factory, argument, configConnectionString);
        }

        private async Task RunTestAsync(Type testType, string testName, ICosmosDBServiceFactory factory, object argument = null, string configConnectionString = ConfigConnStr, bool includeDefaultConnectionString = true)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);

            var arguments = new Dictionary<string, object>
            {
                { "triggerData", argument }
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("Database", "ResolvedDatabase");
            resolver.Values.Add("Collection", "ResolvedCollection");
            resolver.Values.Add("MyConnectionString", AttributeConnStr);
            resolver.Values.Add("Query", "ResolvedQuery");
            if (includeDefaultConnectionString)
            {
                resolver.Values.Add(CosmosDBExtensionConfigProvider.AzureWebJobsCosmosDBConnectionStringName, DefaultConnStr);
            }

            IHost host = new HostBuilder()
                .ConfigureWebJobsHost()
                .AddAzureStorage()
                .AddCosmosDB()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ICosmosDBServiceFactory>(factory);
                    services.AddSingleton<INameResolver>(resolver);
                    services.AddSingleton<ITypeLocator>(locator);

                    services.Configure<CosmosDBOptions>(o => o.ConnectionString = configConnectionString);
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

                arrayItem = new Document[]
                {
                    new Document(),
                    new Document()
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
                [CosmosDB] DocumentClient client,
                TraceWriter trace)
            {
                Assert.NotNull(client);

                trace.Warning("Client");
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("fakequeue1")] string triggerData,
                [CosmosDB(DatabaseName, CollectionName, Id = "{QueueTrigger}")] dynamic item1,
                [CosmosDB(DatabaseName, CollectionName, Id = "docid2", PartitionKey = "{QueueTrigger}")] dynamic item2,
                [CosmosDB(DatabaseName, CollectionName, Id = "docid3", PartitionKey = "partkey3")] Item item3,
                [CosmosDB("%Database%", "%Collection%", Id = "docid4")] JObject item4,
                [CosmosDB(DatabaseName, CollectionName, Id = "docid5")] string item5,
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "some query")] IEnumerable<JObject> query1,
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "some %Query% with '{QueueTrigger}' replacements")] IEnumerable<JObject> query2,
                [CosmosDB(DatabaseName, CollectionName)] JArray query3,
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

                // add some value to item2
                item2.text = "changed";

                trace.Warning("Inputs");
            }

            [NoAutomaticTrigger]
            public static void TriggerObject(
                [QueueTrigger("fakequeue1")] QueueData triggerData,
                [CosmosDB(DatabaseName, CollectionName, Id = "{DocumentId}", PartitionKey = "{PartitionKey}", ConnectionStringSetting = "MyConnectionString")] dynamic item1,
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
                [CosmosDB] DocumentClient client)
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
