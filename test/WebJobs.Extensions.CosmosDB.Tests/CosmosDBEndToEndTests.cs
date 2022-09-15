// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
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
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Fact]
        public async Task CosmosDBEndToEnd()
        {
            using (var host = await StartHostAsync(typeof(EndToEndTestClass)))
            {
                var client = await InitializeDocumentClientAsync(host.Services.GetRequiredService<IConfiguration>());

                // Call the outputs function directly, which will write out 3 documents 
                // using with the 'input' property set to the value we provide.
                var input = Guid.NewGuid().ToString();
                var parameter = new Dictionary<string, object>();
                parameter["input"] = input;

                await host.GetJobHost().CallAsync(nameof(EndToEndTestClass.Outputs), parameter);

                // Also insert a new Document so we can query on it.
                var response = await client.GetContainer(DatabaseName, CollectionName).UpsertItemAsync<Item>(new Item() { Id = Guid.NewGuid().ToString() });

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
                    return _loggerProvider.GetAllLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")) == 4
                        && _loggerProvider.GetAllLogMessages().Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger with string called!")) == 4;
                });

                // Make sure the Options were logged. Just check a few values.
                string optionsMessage = _loggerProvider.GetAllLogMessages()
                    .Single(m => m.Category == "Microsoft.Azure.WebJobs.Hosting.OptionsLoggingService" && m.FormattedMessage.StartsWith(nameof(CosmosDBOptions)))
                    .FormattedMessage;
                JObject loggedOptions = JObject.Parse(optionsMessage.Substring(optionsMessage.IndexOf(Environment.NewLine)));
                Assert.Null(loggedOptions["ConnectionMode"].Value<string>());
            }
        }

        [Fact]
        public async Task CosmosDBEndToEnd_WithRetry()
        {
            using (var host = await StartHostAsync(typeof(EndToEndTestClass_Retry)))
            {
                var client = await InitializeDocumentClientAsync(host.Services.GetRequiredService<IConfiguration>());

                // Call the outputs function directly, which will write out 3 documents 
                // using with the 'input' property set to the value we provide.
                var input = Guid.NewGuid().ToString();
                var parameter = new Dictionary<string, object>();
                parameter["input"] = input;

                await host.GetJobHost().CallAsync(nameof(EndToEndTestClass_Retry.Outputs), parameter);

                await TestHelpers.Await(() =>
                {
                    var logMessages = _loggerProvider.GetAllLogMessages();
                    foreach (LogMessage logMsg in logMessages)
                    {
                        if (logMsg.Exception != null)
                        {
                            Console.WriteLine(logMsg.Exception.InnerException.Message);
                        }
                    }
                    
                    return logMessages.Count(p => p.FormattedMessage != null && p.FormattedMessage.Contains("Trigger called!")) == 6
                        && logMessages.Count(p => p.Exception != null && p.Exception.InnerException.Message.Contains("Test exception") && !p.Category.StartsWith("Host.Results")) == 1;
                });
            }
        }

        private async Task<CosmosClient> InitializeDocumentClientAsync(IConfiguration configuration)
        {
            var client = new CosmosClient(configuration.GetConnectionStringOrSetting(Constants.DefaultConnectionStringName).Value);

            Database database = await client.CreateDatabaseIfNotExistsAsync(DatabaseName);

            try
            {
                await database.GetContainer(CollectionName).ReadContainerAsync();
            }
            catch (CosmosException cosmosException) when (cosmosException.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                await database.CreateContainerAsync(CollectionName, "/id");
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
            [NoAutomaticTrigger]
            public static async Task Outputs(
                string input,
                [CosmosDB(DatabaseName, CollectionName, CreateIfNotExists = true)] IAsyncCollector<object> collector,
                ILogger log)
            {
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
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseContainerIfNotExists = true)]IReadOnlyList<Item> documents,
                ILogger log)
            {
                foreach (var document in documents)
                {
                    log.LogInformation("Trigger called!");
                }
            }

            public static void TriggerWithString(
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseContainerIfNotExists = true, LeaseContainerPrefix = "withstring")] string documents,
                ILogger log)
            {
                foreach (var document in JArray.Parse(documents))
                {
                    log.LogInformation("Trigger with string called!");
                }
            }
        }

        private static class EndToEndTestClass_Retry
        {
            private static bool shouldThrow = true;
            
            [NoAutomaticTrigger]
            public static async Task Outputs(
                string input,
                [CosmosDB(DatabaseName, CollectionName, CreateIfNotExists = true)] IAsyncCollector<object> collector,
                ILogger log)
            {
                for (int i = 0; i < 3; i++)
                {
                    await collector.AddAsync(new { input = input, id = Guid.NewGuid().ToString() });
                }
            }

            [FixedDelayRetry(5, "00:00:01")]
            public static void Trigger(
                [CosmosDBTrigger(DatabaseName, CollectionName, CreateLeaseContainerIfNotExists = true)] IReadOnlyList<Item> documents,
                ILogger log)
            {
                foreach (var document in documents)
                {
                    log.LogInformation($"Trigger called!");
                }

                if (shouldThrow)
                {
                    shouldThrow = false;
                    throw new Exception("Test exception");
                }
            }
        }
    }
}
