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
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Indexers;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.DocumentDB
{
    public class DocumentDBEndToEndTests
    {
        private const string DatabaseName = "TestDatabase";
        private const string CollectionName = "TestCollection";

        private const string AttributeConnStr = "AccountEndpoint=https://attribute;AccountKey=attribute";
        private const string ConfigConnStr = "AccountEndpoint=https://config;AccountKey=config";
        private const string DefaultConnStr = "AccountEndpoint=https://default;AccountKey=default";

        [Fact]
        public void OutputBindings()
        {
            // Arrange
            var serviceMock = new Mock<IDocumentDBService>(MockBehavior.Strict);
            serviceMock
                .Setup(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()))
                .ReturnsAsync(new Document());

            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(ConfigConnStr))
                .Returns(serviceMock.Object);

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            //Act
            RunTest("Outputs", factoryMock.Object, testTrace);

            // Assert
            factoryMock.Verify(f => f.CreateService(ConfigConnStr), Times.Once());
            serviceMock.Verify(m => m.UpsertDocumentAsync(It.IsAny<Uri>(), It.IsAny<object>()), Times.Exactly(7));
            Assert.Equal("Outputs", testTrace.Events.Single().Message);
        }

        [Fact]
        public void ClientBinding()
        {
            // Arrange
            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(DefaultConnStr))
                .Returns<string>(connectionString => new DocumentDBService(connectionString));

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            // Also verify that this falls back to the default by setting the config connection string to null
            RunTest("Client", factoryMock.Object, testTrace, configConnectionString: null);

            //Assert
            factoryMock.Verify(f => f.CreateService(DefaultConnStr), Times.Once());
            Assert.Equal("Client", testTrace.Events.Single().Message);
        }

        [Fact]
        public void InputBindings()
        {
            // Arrange
            string item1Id = "docid1";
            string item2Id = "docid2";
            string item3Id = "docid3";
            string item4Id = "docid4";
            Uri item1Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item1Id);
            Uri item2Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item2Id);
            Uri item3Uri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, item3Id);
            Uri item4Uri = UriFactory.CreateDocumentUri("ResolvedDatabase", "ResolvedCollection", item4Id);

            string options2 = string.Format("[\"{0}\"]", item1Id); // this comes from the trigger
            string options3 = "[\"partkey3\"]";

            var serviceMock = new Mock<IDocumentDBService>(MockBehavior.Strict);

            serviceMock
                .Setup(m => m.ReadDocumentAsync<object>(item1Uri, null))
                .ReturnsAsync(new Document { Id = item1Id });

            serviceMock
               .Setup(m => m.ReadDocumentAsync<dynamic>(item2Uri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == options2)))
               .ReturnsAsync(new Document { Id = item2Id });

            serviceMock
                .Setup(m => m.ReadDocumentAsync<dynamic>(item3Uri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == options3)))
                .ReturnsAsync(new Document { Id = item3Id });

            serviceMock
                .Setup(m => m.ReadDocumentAsync<dynamic>(item4Uri, null))
                .ReturnsAsync(new Document { Id = item4Id });

            // We only expect item2 to be updated
            serviceMock
                .Setup(m => m.ReplaceDocumentAsync(item2Uri, It.Is<object>(d => ((Document)d).Id == item2Id)))
                .ReturnsAsync(new Document());

            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(It.IsAny<string>()))
                .Returns(serviceMock.Object);

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            RunTest("Inputs", factoryMock.Object, testTrace, item1Id);

            // Assert
            factoryMock.Verify(f => f.CreateService(It.IsAny<string>()), Times.Once());
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("Inputs", testTrace.Events[0].Message);
        }

        [Fact]
        public void TriggerObject()
        {
            // Arrange
            string itemId = "docid1";
            Uri itemUri = UriFactory.CreateDocumentUri(DatabaseName, CollectionName, itemId);

            string key = "[\"partkey1\"]";

            var serviceMock = new Mock<IDocumentDBService>(MockBehavior.Strict);

            serviceMock
               .Setup(m => m.ReadDocumentAsync<dynamic>(itemUri, It.Is<RequestOptions>(r => r.PartitionKey.ToString() == key)))
               .ReturnsAsync(new Document { Id = itemId });

            var factoryMock = new Mock<IDocumentDBServiceFactory>(MockBehavior.Strict);
            factoryMock
                .Setup(f => f.CreateService(AttributeConnStr))
                .Returns(serviceMock.Object);

            var jobject = JObject.FromObject(new QueueData { DocumentId = "docid1", PartitionKey = "partkey1" });

            var testTrace = new TestTraceWriter(TraceLevel.Warning);

            // Act
            RunTest("TriggerObject", factoryMock.Object, testTrace, jobject.ToString());

            // Assert
            factoryMock.Verify(f => f.CreateService(AttributeConnStr), Times.Once());
            Assert.Equal(1, testTrace.Events.Count);
            Assert.Equal("TriggerObject", testTrace.Events[0].Message);
        }

        [Fact]
        public void NoConnectionString()
        {
            // Act
            var ex = Assert.Throws<FunctionIndexingException>(
                () => RunTest(typeof(DocumentDBNoConnectionStringFunctions), "Broken", new DefaultDocumentDBServiceFactory(), new TestTraceWriter(), configConnectionString: null, includeDefaultConnectionString: false));

            // Assert
            Assert.IsType<InvalidOperationException>(ex.InnerException);
        }

        private void RunTest(string testName, IDocumentDBServiceFactory factory, TraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr)
        {
            RunTest(typeof(DocumentDBEndToEndFunctions), testName, factory, testTrace, argument, configConnectionString);
        }

        private void RunTest(Type testType, string testName, IDocumentDBServiceFactory factory, TraceWriter testTrace, object argument = null, string configConnectionString = ConfigConnStr, bool includeDefaultConnectionString = true)
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
            if (includeDefaultConnectionString)
            {
                resolver.Values.Add(DocumentDBConfiguration.AzureWebJobsDocumentDBConnectionStringName, DefaultConnStr);
            }

            config.NameResolver = resolver;

            config.UseDocumentDB(documentDBConfig);

            JobHost host = new JobHost(config);

            host.Start();
            host.Call(testType.GetMethod(testName), arguments);
            host.Stop();
        }

        private class DocumentDBEndToEndFunctions
        {
            [NoAutomaticTrigger]
            public static void Outputs(
                [DocumentDB(DatabaseName, CollectionName)] out object newItem,
                [DocumentDB(DatabaseName, CollectionName)] out object[] arrayItem,
                [DocumentDB(DatabaseName, CollectionName)] IAsyncCollector<object> asyncCollector,
                [DocumentDB(DatabaseName, CollectionName)] ICollector<object> collector,
                TraceWriter trace)
            {
                newItem = new { };

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
                [DocumentDB(DatabaseName, CollectionName, Id = "docid3", PartitionKey = "partkey3")] dynamic item3,
                [DocumentDB("%Database%", "%Collection%", Id = "docid4")] dynamic item4,
                TraceWriter trace)
            {
                Assert.NotNull(item1);
                Assert.NotNull(item2);
                Assert.NotNull(item3);
                Assert.NotNull(item4);

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

        private class QueueData
        {
            public string DocumentId { get; set; }
            public string PartitionKey { get; set; }
        }
    }
}
