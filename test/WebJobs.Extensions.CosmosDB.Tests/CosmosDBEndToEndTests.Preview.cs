// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.
#if PREVIEW

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.CosmosDB.Models;
using Microsoft.Azure.WebJobs.Host.Listeners;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    // The EndToEnd tests require the AzureWebJobsCosmosDBConnectionString environment variable to be set.
    public sealed partial class CosmosDBEndToEndTests
    {
        private const string AllVersionsDeleteCollection = "allVersionsAndDeletes";

        [Fact]
        public async Task CosmosDBEndToEndChangeFeedModeWrongType()
        {
            _loggerProvider.ClearAllLogMessages();
            using var host = BuildHost(typeof(IncorrectBindingTypeTestClass));
            using var client = await InitializeDocumentClientAsync(
                host.Services.GetRequiredService<IConfiguration>(), DatabaseName, AllVersionsDeleteCollection, "/_partitionKey");

            try
            {
                await host.StartAsync();
            }
            catch (FunctionListenerException ex)
            {
                InvalidOperationException inner = Assert.IsType<InvalidOperationException>(ex.InnerException);
                Assert.StartsWith($"When using ChangeFeedMode.AllVersionsAndDeletes, the trigger binding type must be Microsoft.Azure.Cosmos.ChangeFeedItem<T>.", inner.Message);
            }
        }

        [Fact(Skip = "Emulator doesn't appear to work with AllChangesAndDelete")]
        public async Task CosmosDBEndToEndChangeFeedMode()
        {
            _loggerProvider.ClearAllLogMessages();
            using var host = BuildHost(typeof(AllVersionsAndDeleteTestClass));
            using var client = await InitializeDocumentClientAsync(
                host.Services.GetRequiredService<IConfiguration>(), DatabaseName, AllVersionsDeleteCollection, "/_partitionKey");

            await host.StartAsync();
            await Task.Delay(TimeSpan.FromSeconds(10)); // change feed listener takes a bit to start

            Item item = new() { Id = Guid.NewGuid().ToString(), Text = "Some Text" };
            var container = client.GetContainer(DatabaseName, AllVersionsDeleteCollection);
            await container.CreateItemAsync(item, PartitionKey.None);

            IReadOnlyCollection<ChangeFeedItem<Item>> input =
                await AllVersionsAndDeleteTestClass.Task.WaitAsync(TimeSpan.FromSeconds(30));

            Assert.Single(input, i => i.Current.Id == item.Id);
        }

        private static class IncorrectBindingTypeTestClass
        {
            public static void Trigger(
                [CosmosDBTrigger(
                    DatabaseName,
                    AllVersionsDeleteCollection,
                    CreateLeaseContainerIfNotExists = true,
                    LeaseContainerPrefix = "ciIncorrectBindingType",
                    ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] IReadOnlyCollection<Item> documents) // Not ChangeFeedItem<Item>
            {
                // This method is intentionally left blank.
            }
        }

        private static class AllVersionsAndDeleteTestClass
        {
            private static TaskCompletionSource<IReadOnlyCollection<ChangeFeedItem<Item>>> _triggered = new();

            public static Task<IReadOnlyCollection<ChangeFeedItem<Item>>> Task => _triggered.Task;

            public static void Trigger(
                [CosmosDBTrigger(
                    DatabaseName,
                    AllVersionsDeleteCollection,
                    CreateLeaseContainerIfNotExists = true,
                    LeaseContainerPrefix = "ciAllVersionsAndDelete",
                    ChangeFeedMode = CosmosDBChangeFeedMode.AllVersionsAndDeletes)] IReadOnlyCollection<ChangeFeedItem<Item>> documents)
            {
                _triggered.TrySetResult(documents);
            }
        }
    }
}
#endif
