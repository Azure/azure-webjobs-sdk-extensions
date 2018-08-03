// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for CosmosDB integration.
    /// </summary>
    public static class CosmosDBHostBuilderExtensions
    {
        /// <summary>
        /// Enables use of the CosmosDB extensions
        /// </summary>
        /// <param name="hostBuilder">The <see cref="IHostBuilder"/> to register the extension with.</param>
        public static IHostBuilder AddCosmosDB(this IHostBuilder hostBuilder)
        {
            return hostBuilder
                .AddExtension<CosmosDBExtensionConfigProvider>()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<ICosmosDBServiceFactory, DefaultCosmosDBServiceFactory>();
                });
        }
    }
}