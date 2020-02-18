// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.CosmosDBCassandra;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for CosmosDB integration.
    /// </summary>
    public static class CosmosDBCassandraWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the CosmosDB extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddCosmosDBCassandra(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<CosmosDBCassandraExtensionConfigProvider>()
                .ConfigureOptions<CosmosDBCassandraOptions>((config, path, options) =>
                {
                    // TODO: Add to Options the Cassandra connection details
                    options.ConnectionString = config.GetConnectionString(Constants.DefaultConnectionStringName);

                    IConfigurationSection section = config.GetSection(path);
                    section.Bind(options);
                });

            builder.Services.AddSingleton<ICosmosDBCassandraServiceFactory, DefaultCosmosDBCassandraServiceFactory>();

            return builder;
        }

        /// <summary>	
        /// Adds the CosmosDB extension to the provided <see cref="IWebJobsBuilder"/>.	
        /// </summary>	
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>	
        /// <param name="configure">An <see cref="Action{CosmosDBCassandraOptions}"/> to configure the provided <see cref="CosmosDBCassandraOptions"/>.</param>	
        public static IWebJobsBuilder AddCosmosDBCassandra(this IWebJobsBuilder builder, Action<CosmosDBCassandraOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddCosmosDBCassandra();
            builder.Services.Configure(configure);


            return builder; 
        }
    }

}