// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.Documents.Client;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
{
    public class CosmosDBHostBuilderExtensionsTests
    {
        [Fact]
        public void ConfigurationBindsToOptions()
        {
            IHost host = new HostBuilder()
                 .ConfigureAppConfiguration(c =>
                 {
                     c.Sources.Clear();

                     var source = new MemoryConfigurationSource
                     {
                         InitialData = new Dictionary<string, string>
                        {
                            { "cosmosDB:Protocol", "Tcp" },
                            { "CosmosDB:LeaseOptions:leaseRenewInterval", "11:11:11" },
                            { "CosmosDB:LeaseOptions:LeasePrefix", "pre1" },
                            { "cosmosdb:leaseoptions:CheckpointFrequency:ProcessedDocumentCount", "1234" }
                        }
                     };

                     c.Add(source);
                 })
                .AddCosmosDB()
                .ConfigureServices(s =>
                {
                    // Verifies that you can modify the bound options
                    s.Configure<CosmosDBOptions>(o => o.LeaseOptions.LeasePrefix = "pre2");
                })
                .Build();

            var options = host.Services.GetService<IOptions<CosmosDBOptions>>().Value;

            Assert.Equal(Protocol.Tcp, options.Protocol);
            Assert.Equal(TimeSpan.Parse("11:11:11"), options.LeaseOptions.LeaseRenewInterval);
            Assert.Equal("pre2", options.LeaseOptions.LeasePrefix);
            Assert.Equal(1234, options.LeaseOptions.CheckpointFrequency.ProcessedDocumentCount);
        }
    }
}
