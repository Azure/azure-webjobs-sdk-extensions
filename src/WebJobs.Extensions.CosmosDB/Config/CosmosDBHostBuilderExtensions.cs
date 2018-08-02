// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for CosmosDB integration
    /// </summary>
    public static class CosmosDBHostBuilderExtensions
    {
        /// <summary>
        /// Adds the CosmosDB extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        public static IHostBuilder AddCosmosDB(this IHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<CosmosDBExtensionConfigProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the Timer extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{CosmosDBOptions}"/> to configure the provided <see cref="CosmosDBOptions"/>.</param>
        public static IHostBuilder AddCosmosDB(this IHostBuilder builder, Action<CosmosDBOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddCosmosDB()
                .ConfigureServices(c => c.Configure(configure));

            return builder;
        }
    }
}
