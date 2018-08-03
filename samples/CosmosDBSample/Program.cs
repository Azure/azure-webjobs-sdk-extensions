// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace CosmosDBSample
{
    class Program
    {
        static async Task Main(string[] args)
        {
            IHost host = new HostBuilder()
                .AddAzureStorage()
                .AddCosmosDB()
                .UseConsoleLifetime()
                .Build();

            using (host)
            {
                await host.RunAsync();
            }
        }
    }
}