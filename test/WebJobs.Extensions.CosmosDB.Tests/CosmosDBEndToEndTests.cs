// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    // The EndToEnd tests require the AzureWebJobsCosmosDBConnectionString environment variable to be set.
    [Trait("Category", "E2E")]
    public class CosmosDBEndToEndTests
    {
        private const string DatabaseName = "E2EDb";
        private const string CollectionName = "E2ECollection";
        private const string LeaseCollectionName = "leases";
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public async Task CosmosDBEndToEnd()
        {
            Console.WriteLine("start");
            _loggerProvider.ClearAllLogMessages();
            using (var host = await StartHostAsync(typeof(EndToEndTestClass)))
            {
                var client = await InitializeDocumentClientAsync(host.Services.GetRequiredService<IConfiguration>(), DatabaseName, CollectionName);

                try
                {
                    // Call the outputs function directly, which will write out 3 documents 
                    // using with the 'input' property set to the value we provide.
                    var input = Guid.NewGuid().ToString();
                    var parameter = new Dictionary<string, object>();
                    parameter["input"] = input;

                    await host.GetJobHost().CallAsync(nameof(EndToEndTestClass.Outputs), parameter);

                    string idToCreate = Guid.NewGuid().ToString();
                    Console.WriteLine($"Creating {idToCreate}");

                    // Also insert a new Document so we can query on it.
                    var response = await client.GetContainer(DatabaseName, CollectionName).UpsertItemAsync<Item>(new Item() { Id = idToCreate });

                    // Now craft a queue message to send to the Inputs, which will pull these documents.
                    var queueInput = new QueueItem
                    {
                        DocumentId = response.Resource.Id,
                        Input = input
                    };

                    parameter.Clear();
                    parameter["item"] = JsonConvert.SerializeObject(queueInput);

                    await host.GetJobHost().CallAsync(nameof(EndToEndTestClass.Inputs), parameter);

                    await TestHelpers.Await(() =>
                    {
                        var logMessages = _loggerProvider.GetAllLogMessages();
                        int count1 = logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!"));
                        int count2 = logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger with string called!"));
                        int count3 = logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger with retry called!"));
                        int count4 = logMessages.Count(p => p.Exception != null && p.Exception.InnerException.Message.Contains("Test exception") && !p.Category.StartsWith("Host.Results"));
                        Console.WriteLine($"count1 {count1}, count2, {count2}, count3 {count3}, count4 {count4}");
                        return logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")) == 4
                            && logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger with string called!")) == 4
                            && logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger with retry called!")) == 8
                            && logMessages.Count(p => p.Exception != null && p.Exception.InnerException.Message.Contains("Test exception") && !p.Category.StartsWith("Host.Results")) > 0;
                    });

                    // Make sure the Options were logged. Just check a few values.
                    string optionsMessage = _loggerProvider.GetAllLogMessages()
                        .Single(m => m.Category == "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService" && m.FormattedMessage.StartsWith(nameof(CosmosDBOptions)))
                        .FormattedMessage;
                    JObject loggedOptions = JObject.Parse(optionsMessage.Substring(optionsMessage.IndexOf(Environment.NewLine)));
                    Assert.Null(loggedOptions["ConnectionMode"].Value<string>());
                }
                finally
                {
                    // Clean-up leases
                    Container leaseContainer = client.GetContainer(DatabaseName, LeaseCollectionName);
                    using FeedIterator<JObject> leaseIterator = leaseContainer.GetItemQueryIterator<JObject>();
                    while (leaseIterator.HasMoreResults)
                    {
                        FeedResponse<JObject> leaseIteratorResponse = await leaseIterator.ReadNextAsync();
                        foreach (JObject lease in leaseIteratorResponse)
                        {
                            Console.WriteLine($"Deleting lease {lease.Value<string>("id")}");
                            await leaseContainer.DeleteItemStreamAsync(lease.Value<string>("id"), new PartitionKey(lease.Value<string>("id")));
                        }
                    }

                    await host.StopAsync();
                }
            }
            Console.WriteLine("end");
        }

        [Fact]
        public async Task CosmosDBEndToEndCancellation()
        {
            Console.WriteLine("Cancellationstart");
            _loggerProvider.ClearAllLogMessages();
            using (var host = await StartHostAsync(typeof(EndToEndCancellationTestClass)))
            {
                var client = await InitializeDocumentClientAsync(host.Services.GetRequiredService<IConfiguration>(), DatabaseName, CollectionName);

                // Insert an item to ensure the function is triggered
                var response = await client.GetContainer(DatabaseName, CollectionName).UpsertItemAsync<Item>(new Item() { Id = Guid.NewGuid().ToString() });

                // Trigger cancellation by stopping the host after the function has been triggered
                await TestHelpers.Await(() => _loggerProvider.GetAllLogMessages().Any(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")));
                await host.StopAsync();
            }

            // Start the host again and wait for the logs to show the cancelled item was reprocessed
            using (var host = await StartHostAsync(typeof(EndToEndCancellationTestClass)))
            {
                var client = await InitializeDocumentClientAsync(host.Services.GetRequiredService<IConfiguration>(), DatabaseName, CollectionName);

                try
                {
                    await TestHelpers.Await(() =>
                        {
                            var logMessages = _loggerProvider.GetAllLogMessages();
                            return logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")) > 1
                                && logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger canceled!")) == 1
                                && logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Saw the first document again!")) == 1
                                && logMessages.Count(p => p.Exception is TaskCanceledException) > 0;
                        });
                }
                finally
                {
                    // Clean-up leases
                    Container leaseContainer = client.GetContainer(DatabaseName, LeaseCollectionName);
                    using FeedIterator<JObject> leaseIterator = leaseContainer.GetItemQueryIterator<JObject>();
                    while (leaseIterator.HasMoreResults)
                    {
                        FeedResponse<JObject> leaseIteratorResponse = await leaseIterator.ReadNextAsync();
                        foreach (JObject lease in leaseIteratorResponse)
                        {
                            Console.WriteLine($"Deleting lease {lease.Value<string>("id")}");
                            await leaseContainer.DeleteItemStreamAsync(lease.Value<string>("id"), new PartitionKey(lease.Value<string>("id")));
                        }
                    }

                    await host.StopAsync();
                }
            }

            Console.WriteLine("Cancellationend");
        }

        public static async Task<CosmosClient> InitializeDocumentClientAsync(IConfiguration configuration, string databaseName, string collectionName)
        {
            var client = new CosmosClient(configuration.GetConnectionStringOrSetting(Constants.DefaultConnectionStringName).Value);

            Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName);

            try
            {
                await database.GetContainer(collectionName).ReadContainerAsync();
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await database.CreateContainerAsync(collectionName, "/id");
            }

            return client;
        }

        private async Task<IHost> StartHostAsync(Type testType)
        {
            ExplicitTypeLocator locator = new ExplicitTypeLocator(testType);

            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder
                    .AddAzureStorage()
                    .AddCosmosDB();
                })
                .ConfigureAppConfiguration(c =>
                {
                    c.AddTestSettings();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ITypeLocator>(locator);
                })
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.AddProvider(_loggerProvider);
                })
                .Build();

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
            private static bool shouldThrow = true;
            
            [NoAutomaticTrigger]
            public static async Task Outputs(
                string input,
                [CosmosDB(DatabaseName, CollectionName, CreateIfNotExists = true)] IAsyncCollector<object> collector,
                ILogger log)
            {
                Console.WriteLine("outputs");
                for (int i = 0; i < 3; i++)
                {
                    await collector.AddAsync(new { input = input, id = Guid.NewGuid().ToString() });
                }
            }

            [NoAutomaticTrigger]
            public static void Inputs(
                [QueueTrigger("NotUsed")] QueueItem item,
                [CosmosDB(DatabaseName, CollectionName, Id = "{DocumentId}")] JObject document,
                [CosmosDB(DatabaseName, CollectionName, SqlQuery = "SELECT * FROM c where c.input = {Input}")] IEnumerable<Item> documents,
                ILogger log)
            {
                Assert.NotNull(document);
                Assert.Equal(3, documents.Count());
            }

            public static void Trigger(
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseContainerIfNotExists = true, LeaseContainerPrefix = "ciTrigger")]IReadOnlyList<Item> documents,
                ILogger log)
            {
                foreach (var document in documents)
                {
                    Console.WriteLine("first" + document.Id);
                    log.LogInformation("Trigger called!");
                }
            }

            public static void TriggerWithString(
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseContainerIfNotExists = true, LeaseContainerPrefix = "ciTriggerWithString")] string documents,
                ILogger log)
            {
                foreach (var document in JArray.Parse(documents))
                {
                    log.LogInformation("Trigger with string called!");
                }
            }

            [FixedDelayRetry(5, "00:00:01")]
            public static void TriggerWithRetry(
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseContainerIfNotExists = true, LeaseContainerPrefix = "ciTriggerWithRetry")] IReadOnlyList<Item> documents,
                ILogger log)
            {
                foreach (var document in documents)
                {
                    Console.WriteLine("retry" + document.Id);
                    log.LogInformation($"Trigger with retry called!");
                }

                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new Exception("Test exception");
                }
            }
        }

        private static class EndToEndCancellationTestClass
        {
            private static string firstDocumentId = null;

            public static async Task Trigger(
                [CosmosDBTrigger(
                    DatabaseName,
                    CollectionName,
                    CreateLeaseContainerIfNotExists = true,
                    LeaseContainerPrefix = "ciTriggerWithCancellation",
                    LeaseExpirationInterval = 20 * 1000,
                    LeaseRenewInterval = 5 * 1000,
                    FeedPollDelay = 500,
                    StartFromBeginning = true)]IReadOnlyList<Item> documents,
                ILogger log,
                CancellationToken cancellationToken)
            {
                log.LogInformation("Trigger called!");

                if (firstDocumentId == null)
                {
                    // The first time around, make a note of the first item's ID
                    firstDocumentId = documents[0].Id;
                    try
                    {
                        // Use a delay to simulate processing
                        await Task.Delay(TimeSpan.FromSeconds(30), cancellationToken);
                    }
                    catch (TaskCanceledException)
                    {
                        log.LogWarning("Trigger canceled!");
                        throw;
                    }
                }
                else if (documents.Any(document => document.Id == firstDocumentId))
                {
                    // Log a message if we see the first item from the earlier delay again
                    log.LogInformation("Saw the first document again!");
                }
            }
        }
    }
}
