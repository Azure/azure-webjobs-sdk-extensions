// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Common;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Castle.Core.Configuration;
using Castle.Core.Logging;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Xunit;
using IConfiguration = Microsoft.Extensions.Configuration.IConfiguration;
using TestLoggerProvider = Microsoft.Azure.WebJobs.Host.TestCommon.TestLoggerProvider;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    [Trait("Category", "E2E")]
    public class ScaleHostEndToEndTests
    {
        private const string FunctionName = "Function1";
        private const string DatabaseName = "E2EDb";
        private const string CollectionName = "E2EScaleCollection";
        private const string Connection = "CosmosDbConnection";
        private readonly TestLoggerProvider _loggerProvider = new TestLoggerProvider();

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task ScaleHostEndToEndTest(bool tbsEnabled)
        {
            IHost webJobsHost = new HostBuilder()
                .ConfigureWebJobs()
                .Build();
            var configuration = webJobsHost.Services.GetRequiredService<IConfiguration>();
            var connectionString = configuration.GetConnectionStringOrSetting(Constants.DefaultConnectionStringName).Value;
            DbConnectionStringBuilder builder = new DbConnectionStringBuilder()
            {
                ConnectionString = connectionString
            };

            string triggers = $@"{{
            ""triggers"": [
                {{
                    ""name"": ""myQueueItem"",
                    ""type"": ""queueTrigger"",
                    ""direction"": ""in"",
                    ""connection"": ""{Connection}"",
                    ""databaseName"": ""{DatabaseName}"",
                    ""containerName"": ""{CollectionName}"",
                    ""MaxItemsPerInvocation"": 1,
                    ""functionName"": ""{FunctionName}""
                }}
             ]}}";

            IHost host = new HostBuilder().ConfigureServices(services => services.AddAzureClientsCore()).Build();
            AzureComponentFactory defaultAzureComponentFactory = host.Services.GetService<AzureComponentFactory>();

            string hostId = "test-host";
            var loggerProvider = new TestLoggerProvider();

            IHostBuilder hostBuilder = new HostBuilder();
            hostBuilder.ConfigureLogging(configure =>
            {
                configure.SetMinimumLevel(LogLevel.Debug);
                configure.AddProvider(loggerProvider);
            });
            hostBuilder.ConfigureAppConfiguration((hostBuilderContext, config) =>
            {
                var settings = new Dictionary<string, string>()
                {
                    { $"{Connection}", connectionString }
                };

                // Adding app setting
                config.AddInMemoryCollection(settings);
            })
            .ConfigureServices(services =>
            {
                services.AddAzureStorageScaleServices();
                services.AddSingleton<INameResolver, FakeNameResolver>();
            })
            .ConfigureWebJobsScale((context, builder) =>
            {
                builder.AddCosmosDB();
                builder.UseHostId(hostId);

                foreach (var jtoken in JObject.Parse(triggers)["triggers"])
                {
                    TriggerMetadata metadata = new TriggerMetadata(jtoken as JObject);
                    builder.AddCosmosDbScaleForTrigger(metadata);
                }
            },
            scaleOptions =>
            {
                scaleOptions.IsTargetScalingEnabled = tbsEnabled;
                scaleOptions.MetricsPurgeEnabled = false;
                scaleOptions.ScaleMetricsMaxAge = TimeSpan.FromMinutes(4);
                scaleOptions.IsRuntimeScalingEnabled = true;
                scaleOptions.ScaleMetricsSampleInterval = TimeSpan.FromSeconds(1);
            });

            using (var client = await CosmosDBEndToEndTests.InitializeDocumentClientAsync(configuration, DatabaseName, CollectionName))
            {
                var container = client.GetDatabase(DatabaseName).GetContainer(CollectionName);

                // Delete existing items
                var array = container.GetItemLinqQueryable<Item>(allowSynchronousQueryExecution: true).ToArray();
                foreach (var itemToDelete in array)
                {
                    await container.DeleteItemAsync<Item>(itemToDelete.Id, new PartitionKey(itemToDelete.Id));
                }

                // Add new items to trigger scale
                for (int i = 0; i < 2; i++)
                {
                    var item = new Item() { Id = Guid.NewGuid().ToString(), Text = "Scale" };
                    PartitionKey pk = new PartitionKey(item.Id);
                    await container.UpsertItemAsync<Item>(item, pk);
                }

                await StartProcessor(client);
            }

            IHost scaleHost = hostBuilder.Build();
            await scaleHost.StartAsync();

            await Host.TestCommon.TestHelpers.Await(async () =>
            {
                IScaleStatusProvider scaleManager = scaleHost.Services.GetService<IScaleStatusProvider>();

                var scaleStatus = await scaleManager.GetScaleStatusAsync(new ScaleStatusContext());

                bool scaledOut = false;
                if (!tbsEnabled)
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == null && scaleStatus.FunctionTargetScalerResults.Count == 0
                        && scaleStatus.FunctionScaleStatuses[FunctionName].Vote == ScaleVote.ScaleOut;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains(logMessages, p => p.Contains("1 scale monitors to sample"));
                    }
                }
                else
                {
                    scaledOut = scaleStatus.Vote == ScaleVote.ScaleOut && scaleStatus.TargetWorkerCount == 1 && scaleStatus.FunctionScaleStatuses.Count == 0
                     && scaleStatus.FunctionTargetScalerResults[FunctionName].TargetWorkerCount == 1;

                    if (scaledOut)
                    {
                        var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                        Assert.Contains(logMessages, p => p.Contains("1 target scalers to sample"));
                    }
                }

                if (scaledOut)
                {
                    var logMessages = loggerProvider.GetAllLogMessages().Select(p => p.FormattedMessage).ToArray();
                    Assert.Contains(logMessages, p => p.Contains("Runtime scale monitoring is enabled."));
                    if (!tbsEnabled)
                    {
                        Assert.Contains(logMessages, p => p.Contains("Scaling out based on votes"));
                    }
                }

                return scaledOut;
            }, pollingInterval: 2000, timeout: 180000, throwWhenDebugging: true);
        }

        private async Task StartProcessor(CosmosClient cosmosClient)
        {
            var leaseContainer = cosmosClient.GetContainer(DatabaseName, "leases");
            var monitoredContainer = cosmosClient.GetContainer(DatabaseName, CollectionName);

            var builder = monitoredContainer.GetChangeFeedProcessorBuilder<Item>(string.Empty, HandleChangesAsync)
                .WithInstanceName("MyInstance")
                .WithLeaseContainer(leaseContainer)
                .WithStartTime(DateTime.UtcNow)
                .WithPollInterval(TimeSpan.FromSeconds(10));

            var processor = builder.Build();
            await processor.StartAsync();
        }

        private static async Task HandleChangesAsync(
            ChangeFeedProcessorContext context,
            IReadOnlyCollection<Item> changes,
            CancellationToken cancellationToken)
        {
        }
    }
}
