// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.DocumentDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB.Models;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    [Trait("Category", "E2E")]
    public class DocumentDBEndToEndTests
    {
        private const string DatabaseName = "TestDatabase";
        private const string CollectionName = "TestCollection";

        private const string AttributeConnStr = "AccountEndpoint=https://attribute;AccountKey=YXR0cmlidXRl;";
        private const string ConfigConnStr = "AccountEndpoint=https://config;AccountKey=Y29uZmln;";
        private const string DefaultConnStr = "AccountEndpoint=https://default;AccountKey=ZGVmYXVsdA==;";

        [Fact]
        public async Task OutputBindings()
        {
            // Arrange
            var serviceMock = new Mock<IDocumentDBService>(MockBehavior.Strict);
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

            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr, null, null))
                .Returns(serviceMock.Object);

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            //Act
            await RunTestAsync("Outputs", factoryMock.Object, testTrace);

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr, null, null), Times.Once());
            serviceMock.Verify(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()), Times.Exactly(8));
            Assert.Equal("Outputs", testTrace.Events.Single().Message);
        }

        [Fact]
        public async Task ClientBinding()
        {
            // Arrange
            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(DefaultConnStr, null, null))
                .Returns<string, ConnectionMode?, Protocol?>((connectionString, connectionMode, protocol) => new DocumentDBService(connectionString, connectionMode, protocol));

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            // Also verify that this falls back to the default by setting the config connection string to null
            await RunTestAsync("Client", factoryMock.Object, testTrace, configConnectionString: null);

            //Assert
            factoryMock.Verify(f => f.CreateService(DefaultConnStr, null, null), Times.Once());
            Assert.Equal("Client", testTrace.Events.Single().Message);
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

            var serviceMock = new Mock<IDocumentDBService>(MockBehavior.Strict);

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

            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(It.IsAny<string>(), null, null))
                .Returns(serviceMock.Object);

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            await RunTestAsync(nameof(DocumentDBEndToEndFunctions.Inputs), factoryMock.Object, testTrace, item1Id);

            // Assert
            factoryMock.Verify(f => f.CreateService(It.IsAny<string>(), null, null), Times.Once());
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("Inputs", testTrace.Events[0].Message);
            serviceMock.VerifyAll();
        }

        [Fact]
        public async Task TriggerObject()
        {
            // Arrange
            string itemId = "docid1";
            Uri itemUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, itemId);

            string key = "[\"partkey1\"]";

            var serviceMock = new Mock<IDocumentDBService>(MockBehavior.Strict);

            serviceMock
               .Setup(m => m.ReadDocumentAsync(itemUri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == key)))
               .ReturnsAsync(new Document { Id = itemId });

            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(AttributeConnStr, null, null))
                .Returns(serviceMock.Object);

            var jobject = JObject.FromObject(new QueueData { DocumentId = "docid1", PartitionKey = "partkey1" });

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            await RunTestAsync(nameof(DocumentDBEndToEndFunctions.TriggerObject), factoryMock.Object, testTrace, jobject.ToString());

            // Assert
            factoryMock.Verify(f => f.CreateService(AttributeConnStr, null, null), Times.Once());
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("TriggerObject", testTrace.Events[0].Message);
        }

        [Fact]
        public async Task NoConnectionString()
        {
            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(DocumentDBNoConnectionStringFunctions), "Broken", new DefaultDocumentDBServiceFactory(), new TestTraceWriter(), configConnectionString: null, includeDefaultConnectionString: false));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        [Fact]
        public async Task InvalidEnumerableBindings()
        {
            // Verify that the validator is properly wired up. Unit tests already check all other permutations.

            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(DocumentDBInvalidEnumerableBindingFunctions), "BrokenEnumerable", new DefaultDocumentDBServiceFactory(), new TestTraceWriter()));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Equal("'Id' cannot be specified when binding to an IEnumerable property.", ex.InnerException.Message);
        }

        [Fact]
        public async Task InvalidItemBindings()
        {
            // Verify that the validator is properly wired up. Unit tests already check all other permutations.

            // Act
            var ex = await Assert.ThrowsAsync<FunctionIndexingException>(
                () => RunTestAsync(typeof(DocumentDBInvalidItemBindingFunctions), "BrokenItem", new DefaultDocumentDBServiceFactory(), new TestTraceWriter()));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
            Assert.Equal("'Id' is required when binding to a JObject property.", ex.InnerException.Message);
        }

        private Task RunTestAsync(string testName, IDocumentDBServiceFactory factory, TestTraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr)
        {
            return RunTestAsync(typeof(DocumentDBEndToEndFunctions), testName, factory, testTrace, argument, configConnectionString);
        }

        private async Task RunTestAsync(Type testType, string testName, IDocumentDBServiceFactory factory, TestTraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr, bool includeDefaultConnectionString = true)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
            };

            config.Tracing.Tracers.Add(testTrace);

            var arguments = new Dictionary<string, object>();
            arguments.Add("triggerData", argument);

            var documentDBConfig = new DocumentDBConfiguration()
            {
                ConnectionString = configConnectionString,
                DocumentDBServiceFactory = factory
            };

            var resolver = new TestNameResolver();
            resolver.Values.Add("Database", "ResolvedDatabase");
            resolver.Values.Add("Collection", "ResolvedCollection");
            resolver.Values.Add("MyConnectionString", AttributeConnStr);
            resolver.Values.Add("Query", "ResolvedQuery");
            if (includeDefaultConnectionString)
            {
                resolver.Values.Add(DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName, DefaultConnStr);
            }

            config.NameResolver = resolver;

            config.UseDocumentDB(documentDBConfig);

            JobHost host = new JobHost(config);

            await host.StartAsync();
            testTrace.Events.Clear();
            await host.CallAsync(testType.GetMethod(testName), arguments);
            await host.StopAsync();
        }

        private class DocumentDBEndToEndFunctions
        {
            [NoAutomaticTrigger]
            public static void Outputs(
                [DocumentDB(DatabaseName, CollectionName)] out object newItem,
                [DocumentDB(DatabaseName, CollectionName)] out string newItemString,
                [DocumentDB(DatabaseName, CollectionName)] out object[] arrayItem,
                [DocumentDB(DatabaseName, CollectionName)] IAsyncCollector<object> asyncCollector,
                [DocumentDB(DatabaseName, CollectionName)] ICollector<object> collector,
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
                [DocumentDB] DocumentClient client,
                TraceWriter trace)
            {
                Assert.NotNull(client);

                trace.Warning("Client");
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("fakequeue1")] string triggerData,
                [DocumentDB(DatabaseName, CollectionName, Id = "{QueueTrigger}")] dynamic item1,
                [DocumentDB(DatabaseName, CollectionName, Id = "docid2", PartitionKey = "{QueueTrigger}")] dynamic item2,
                [DocumentDB(DatabaseName, CollectionName, Id = "docid3", PartitionKey = "partkey3")] Item item3,
                [DocumentDB("%Database%", "%Collection%", Id = "docid4")] JObject item4,
                [DocumentDB(DatabaseName, CollectionName, Id = "docid5")] string item5,
                [DocumentDB(DatabaseName, CollectionName, SqlQuery = "some query")] IEnumerable<JObject> query1,
                [DocumentDB(DatabaseName, CollectionName, SqlQuery = "some %Query% with '{QueueTrigger}' replacements")] IEnumerable<JObject> query2,
                [DocumentDB(DatabaseName, CollectionName)] JArray query3,
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
                [DocumentDB(DatabaseName, CollectionName, Id = "{DocumentId}", PartitionKey = "{PartitionKey}", ConnectionStringSetting = "MyConnectionString")] dynamic item1,
                TraceWriter trace)
            {
                Assert.NotNull(item1);

                trace.Warning("TriggerObject");
            }
        }

        private class DocumentDBNoConnectionStringFunctions
        {
            [NoAutomaticTrigger]
            public static void Broken(
                [DocumentDB] DocumentClient client)
            {
            }
        }

        private class DocumentDBInvalidEnumerableBindingFunctions
        {
            [NoAutomaticTrigger]
            public static void BrokenEnumerable(
                [DocumentDB(Id = "Some_Id")] IEnumerable<JObject> inputs)
            {
            }
        }

        private class DocumentDBInvalidItemBindingFunctions
        {
            [NoAutomaticTrigger]
            public static void BrokenItem(
                [DocumentDB(SqlQuery = "some query")] JObject item)
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
