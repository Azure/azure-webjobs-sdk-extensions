// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.Documents;
using Microsoft.Azure.Documents.Client;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB
{
    // The EndToEnd tests require the AzureWebJobsCosmosDBConnectionString environment variable to be set.
    [Trait("Category", "E2E")]
    public class CosmosDBEndToEndTests
    {
        private const string DatabaseName = "E2EDb";
        private const string CollectionName = "E2ECollection";
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public async Task CosmosDBEndToEnd()
        {
            var client = await InitializeDocumentClientAsync();
            using (var host = await StartHostAsync(typeof(EndToEndTestClass)))
            {
                // Call the outputs function directly, which will write out 3 documents 
                // using with the 'input' property set to the value we provide.
                var input = Guid.NewGuid().ToString();
                var parameter = new Dictionary<string, object>();
                parameter["input"] = input;

                await host.CallAsync(nameof(EndToEndTestClass.Outputs), parameter);

                // Also insert a new Document so we can query on it.
                var collectionUri = UriFactory.CreateDocumentCollectionUri(DatabaseName, CollectionName);
                var response = await client.UpsertDocumentAsync(collectionUri, new Document());

                // Now craft a queue message to send to the Inputs, which will pull these documents.
                var queueInput = new QueueItem
                {
                    DocumentId = response.Resource.Id,
                    Input = input
                };

                parameter.Clear();
                parameter["item"] = JsonConvert.SerializeObject(queueInput);

                await host.CallAsync(nameof(EndToEndTestClass.Inputs), parameter);

                await TestHelpers.Await(() =>
                {
                    return _loggerProvider.GetAllLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")) == 4;
                });
            }
        }
        
        private async Task<DocumentClient> InitializeDocumentClientAsync()
        {
            var builder = new DbConnectionStringBuilder();
            builder.ConnectionString = AmbientConnectionStringProvider.Instance.GetConnectionString(CosmosDBConfiguration.AzureWebJobsCosmosDBConnectionStringName);

            var serviceUri = new Uri(builder["AccountEndpoint"].ToString());
            var client = new DocumentClient(serviceUri, builder["AccountKey"].ToString());

            var database = new Database() { Id = DatabaseName };
            await client.CreateDatabaseIfNotExistsAsync(database);

            var collection = new DocumentCollection() { Id = CollectionName };
            await client.CreateDocumentCollectionIfNotExistsAsync(UriFactory.CreateDatabaseUri(DatabaseName), collection);

            return client;
        }

        private async Task<JobHost> StartHostAsync(Type testType)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);
            ILoggerFactory loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(_loggerProvider);

            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator,
                LoggerFactory = loggerFactory
            };

            config.UseCosmosDB();

            JobHost host = new JobHost(config);

            await host.StartAsync();
            return host;
        }

        public class QueueItem
        {
            public string DocumentId { get; set; }

            public string Input { get; set; }
        }

        private static class EndToEndTestClass
        {
            [NoAutomaticTrigger]
            public static async Task Outputs(
                string input,
                [CosmosDB(DatabaseName, CollectionName, CreateIfNotExists = true)] IAsyncCollector<object> collector,
                ILogger log)
            {
                for (int i = 0; i < 3; i++)
                {
                    await collector.AddAsync(new { input });
                }
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("NotUsed")] QueueItem item,
                [CosmosDB(DatabaseName, CollectionName, Id = "{DocumentId}")] JObject document,
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "SELECT * FROM c where c.input = {Input}")] IEnumerable<Document> documents,
                ILogger log)
            {
                Assert.NotNull(document);
                Assert.Equal(3, documents.Count());
            }

            public static void Trigger(
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseCollectionIfNotExists = true)]IReadOnlyList<Document> documents,
                ILogger log)
            {
                foreach (var document in documents)
                {
                    log.LogInformation("Trigger called!");
                }
            }
        }
    }
}
