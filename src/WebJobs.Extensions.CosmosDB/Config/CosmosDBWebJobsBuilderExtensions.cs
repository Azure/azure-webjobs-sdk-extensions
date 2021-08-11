// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for CosmosDB integration.
    /// </summary>
    public static class CosmosDBWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the CosmosDB extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddCosmosDB(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }
            
            builder.AddExtension<CosmosDBExtensionConfigProvider>()               
                .ConfigureOptions<CosmosDBOptions>((config, path, options) =>
                {
                    IConfigurationSection section = config.GetSection(path);
                    section.Bind(options);
                });                

            builder.Services.AddSingleton<ICosmosDBServiceFactory, DefaultCosmosDBServiceFactory>();
            builder.Services.AddSingleton<ICosmosDBSerializerFactory, DefaultCosmosDBSerializerFactory>();

            return builder;
        }

        /// <summary>
        /// Adds the CosmosDB extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{CosmosDBOptions}"/> to configure the provided <see cref="CosmosDBOptions"/>.</param>
        public static IWebJobsBuilder AddCosmosDB(this IWebJobsBuilder builder, Action<CosmosDBOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddCosmosDB();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}