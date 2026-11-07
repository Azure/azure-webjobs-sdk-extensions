// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class ScaleConfigurationTests
    {
        [Fact]
        public void ScaleHostProvidesApplicationSettingsAndExtensionOptions()
        {
            var settings = new Dictionary<string, string>
            {
                ["CosmosConnection"] = "AccountEndpoint=https://example.invalid;AccountKey=c29tZV9rZXk=;",
                ["DatabaseSetting"] = "database",
                ["AzureWebJobs:extensions:cosmosDB:UserAgentSuffix"] = "scale-test"
            };
            var metadata = new TriggerMetadata(JObject.Parse(@"{
                'type': 'cosmosDBTrigger', 'functionName': 'TestFunction',
                'connection': 'CosmosConnection', 'databaseName': 'database',
                'containerName': 'items', 'leaseContainerName': 'leases'
            }"));

            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                .ConfigureWebJobsScale((context, builder) =>
                {
                    builder.AddCosmosDB();
                    builder.AddCosmosDbScaleForTrigger(metadata);
                }, _ => { })
                .Build();

            Assert.Equal("database", host.Services.GetRequiredService<IConfiguration>()["DatabaseSetting"]);
            Assert.Equal("scale-test", host.Services.GetRequiredService<IOptions<CosmosDBOptions>>().Value.UserAgentSuffix);
            Assert.NotNull(host.Services.GetRequiredService<ITargetScalerProvider>().GetTargetScaler());
        }

        [Theory]
        [InlineData("BatchLimit", 6)]
        [InlineData(null, 4)]
        public void ScaleHostResolvesNamedSettingAndExtensionDefault(string settingName, int expectedWorkers)
        {
            var settings = new Dictionary<string, string>
            {
                ["CosmosConnection"] = "AccountEndpoint=https://example.invalid;AccountKey=c29tZV9rZXk=;",
                ["BatchLimit"] = "20",
                ["AzureWebJobs:extensions:cosmosDB:DefaultMaxItemsPerInvocation"] = "30"
            };
            var trigger = JObject.Parse(@"{
                'type': 'cosmosDBTrigger', 'functionName': 'TestFunction',
                'connection': 'CosmosConnection', 'databaseName': 'database',
                'containerName': 'items', 'leaseContainerName': 'leases'
            }");
            if (settingName != null)
            {
                trigger["maxItemsPerInvocationSetting"] = settingName;
            }

            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                .ConfigureWebJobsScale((context, builder) =>
                {
                    builder.AddCosmosDB();
                    builder.AddCosmosDbScaleForTrigger(new TriggerMetadata(trigger));
                }, _ => { })
                .Build();

            var scaler = (CosmosDBTargetScaler)host.Services.GetRequiredService<ITargetScalerProvider>().GetTargetScaler();
            Assert.Equal(expectedWorkers, scaler.GetScaleResultInternal(new TargetScalerContext(), 120, 0).TargetWorkerCount);
            Assert.Equal(3, scaler.GetScaleResultInternal(new TargetScalerContext { InstanceConcurrency = 50 }, 120, 0).TargetWorkerCount);
        }

        [Theory]
        [InlineData("BatchLimit", "invalid", "30", 0, "BatchLimit")]
        [InlineData("BatchLimit", null, "30", 0, "BatchLimit")]
        [InlineData("BatchLimit", "20", "30", 5, "both")]
        [InlineData(null, "20", "invalid", 5, "DefaultMaxItemsPerInvocation")]
        public void ScaleHostRejectsInvalidConfiguration(string settingName, string settingValue, string defaultValue, int explicitValue, string expectedMessage)
        {
            var settings = new Dictionary<string, string>
            {
                ["CosmosConnection"] = "AccountEndpoint=https://example.invalid;AccountKey=c29tZV9rZXk=;",
                ["BatchLimit"] = settingValue,
                ["AzureWebJobs:extensions:cosmosDB:DefaultMaxItemsPerInvocation"] = defaultValue
            };
            var trigger = JObject.Parse(@"{
                'type': 'cosmosDBTrigger', 'functionName': 'TestFunction',
                'connection': 'CosmosConnection', 'databaseName': 'database',
                'containerName': 'items', 'leaseContainerName': 'leases'
            }");
            if (settingName != null)
            {
                trigger["maxItemsPerInvocationSetting"] = settingName;
            }

            trigger["maxItemsPerInvocation"] = explicitValue;
            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                .ConfigureWebJobsScale((context, builder) =>
                {
                    builder.AddCosmosDB();
                    builder.AddCosmosDbScaleForTrigger(new TriggerMetadata(trigger));
                }, _ => { })
                .Build();

            var error = Assert.Throws<InvalidOperationException>(() => host.Services.GetRequiredService<ITargetScalerProvider>());
            Assert.Contains(expectedMessage, error.Message);
            Assert.DoesNotContain("invalid", error.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void ScaleHostWithoutExtensionRegistrationReadsConfiguredDefault()
        {
            var settings = new Dictionary<string, string>
            {
                ["CosmosConnection"] = "AccountEndpoint=https://example.invalid;AccountKey=c29tZV9rZXk=;",
                ["AzureWebJobs:extensions:cosmosDB:DefaultMaxItemsPerInvocation"] = "30"
            };
            var metadata = new TriggerMetadata(JObject.Parse(@"{
                'type': 'cosmosDBTrigger', 'functionName': 'TestFunction',
                'connection': 'CosmosConnection', 'databaseName': 'database',
                'containerName': 'items', 'leaseContainerName': 'leases'
            }"));
            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                .ConfigureWebJobsScale((context, builder) => builder.AddCosmosDbScaleForTrigger(metadata), _ => { })
                .Build();

            var scaler = (CosmosDBTargetScaler)host.Services.GetRequiredService<ITargetScalerProvider>().GetTargetScaler();
            Assert.Equal(4, scaler.GetScaleResultInternal(new TargetScalerContext(), 120, 0).TargetWorkerCount);
        }

        [Fact]
        public void ScaleHostUsesRegisteredNameResolverForNamedBatchSetting()
        {
            var settings = new Dictionary<string, string>
            {
                ["CosmosConnection"] = "AccountEndpoint=https://example.invalid;AccountKey=c29tZV9rZXk=;",
                ["BatchLimit"] = "30"
            };
            var resolver = new TestNameResolver();
            resolver.Values["BatchLimit"] = "20";
            var metadata = new TriggerMetadata(JObject.Parse(@"{
                'type': 'cosmosDBTrigger', 'functionName': 'TestFunction',
                'connection': 'CosmosConnection', 'databaseName': 'database',
                'containerName': 'items', 'leaseContainerName': 'leases',
                'maxItemsPerInvocationSetting': 'BatchLimit'
            }"));
            using IHost host = new HostBuilder()
                .ConfigureAppConfiguration(config => config.AddInMemoryCollection(settings))
                .ConfigureServices(services => services.AddSingleton<INameResolver>(resolver))
                .ConfigureWebJobsScale((context, builder) => builder.AddCosmosDbScaleForTrigger(metadata), _ => { })
                .Build();

            var scaler = (CosmosDBTargetScaler)host.Services.GetRequiredService<ITargetScalerProvider>().GetTargetScaler();
            Assert.Equal(6, scaler.GetScaleResultInternal(new TargetScalerContext(), 120, 0).TargetWorkerCount);
        }
    }
}
