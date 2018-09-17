// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Tests
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
                    builder.UseExternalStartup(new TestStartupTypeLocator());
                })
                .Build();

            var extensionConfig = host.Services.GetServices<IExtensionConfigProvider>().Single();
            Assert.NotNull(extensionConfig);
            Assert.IsType<CosmosDBExtensionConfigProvider>(extensionConfig);
        }

        private class TestStartupTypeLocator : IWebJobsStartupTypeLocator
        {
            public Type[] GetStartupTypes()
            {
                WebJobsStartupAttribute startupAttribute = typeof(CosmosDBOptions).Assembly
                    .GetCustomAttributes<WebJobsStartupAttribute>().Single();

                return new[] { startupAttribute.WebJobsStartupType };
            }
        }
    }
}
