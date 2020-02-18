// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra.Tests
{
    public class CosmosDBWebJobsStartupTests
    {
        [Fact]
        public void CosmosDBStartupIsDiscoverable()
        {
            // Simulate startup discovery
            IHost host = new HostBuilder()
                .ConfigureWebJobs(builder =>
                {
                    builder.AddCosmosDBCassandra();
                })
                .Build();

            var extensionConfig = host.Services.GetServices<IExtensionConfigProvider>().Single();
            Assert.NotNull(extensionConfig);
            Assert.IsType<CosmosDBCassandraExtensionConfigProvider>(extensionConfig);
        }
    }
}
