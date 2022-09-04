// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Common
{
    public static class TestHelpers
    {
        private static readonly IConfiguration _emptyConfig = new ConfigurationBuilder().Build();

        public static Task Await(Func<bool> condition, int timeout = 60 * 1000, int pollingInterval = 2 * 1000, bool throwWhenDebugging = false)
        {
            return Await(() => Task.FromResult(condition()), timeout, pollingInterval, throwWhenDebugging);
        }

        public static async Task Await(Func<Task<bool>> condition, int timeout = 60 * 1000, int pollingInterval = 2 * 1000, bool throwWhenDebugging = false)
        {
            DateTime start = DateTime.Now;
            while (!await condition())
            {
                await Task.Delay(pollingInterval);

                bool shouldThrow = !Debugger.IsAttached || (Debugger.IsAttached && throwWhenDebugging);
                if (shouldThrow && (DateTime.Now - start).TotalMilliseconds > timeout)
                {
                    throw new ApplicationException("Condition not reached within timeout.");
                }
            }
        }

        public static JobHost GetJobHost(this IHost host)
        {
            return host.Services.GetService<IJobHost>() as JobHost;
        }

        public static ExtensionConfigContext CreateExtensionConfigContext(INameResolver resolver)
        {
            var mockWebHookProvider = new Mock<IWebHookProvider>();
            var mockExtensionRegistry = new Mock<IExtensionRegistry>();

            // TODO: ConverterManager needs to be fixed but this will work for now.
            IHost host = new HostBuilder()
                .ConfigureWebJobs()
                .Build();

            var converterManager = host.Services.GetRequiredService<IConverterManager>();

            return new ExtensionConfigContext(_emptyConfig, resolver, converterManager, mockWebHookProvider.Object, mockExtensionRegistry.Object);
        }
    }
}
