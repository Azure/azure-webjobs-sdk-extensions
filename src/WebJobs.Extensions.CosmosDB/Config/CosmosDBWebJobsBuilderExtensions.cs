// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.ComponentModel;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB;
using Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger;
using Microsoft.Azure.WebJobs.Host.Scale;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            builder.Services.AddAzureClientsCore();
            builder.Services.TryAddSingleton<ICosmosDBServiceFactory, DefaultCosmosDBServiceFactory>();
            builder.Services.TryAddSingleton<ICosmosDBSerializerFactory, DefaultCosmosDBSerializerFactory>();
            
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

        /// <summary>
        /// Adds the Storage Queues extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder"></param>
        /// <param name="triggerMetadata">Trigger metadata.</param>
        /// <returns></returns>
        ///
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static IWebJobsBuilder AddCosmosDbScaleForTrigger(this IWebJobsBuilder builder, TriggerMetadata triggerMetadata)
        {
            IServiceProvider serviceProvider = null;
            Lazy<CosmosDbScalerProvider> scalerProvider = new Lazy<CosmosDbScalerProvider>(() => new CosmosDbScalerProvider(serviceProvider, triggerMetadata));

            builder.Services.AddSingleton<IScaleMonitorProvider>(resolvedServiceProvider =>
            {
                serviceProvider = serviceProvider ?? resolvedServiceProvider;
                return scalerProvider.Value;
            });

            builder.Services.AddSingleton<ITargetScalerProvider>(resolvedServiceProvider =>
            {
                serviceProvider = serviceProvider ?? resolvedServiceProvider;
                return scalerProvider.Value;
            });

            return builder;
        }
    }
}