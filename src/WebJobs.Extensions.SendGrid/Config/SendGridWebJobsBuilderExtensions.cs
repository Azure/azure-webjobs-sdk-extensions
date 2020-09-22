// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.SendGrid.Bindings;
using Microsoft.Azure.WebJobs.Extensions.SendGrid.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for SendGrid integration.
    /// </summary>
    public static class SendGridWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the SendGrid extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddSendGrid(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<SendGridExtensionConfigProvider>()
                .ConfigureOptions<SendGridOptions>((rootConfig, extensionPath, options) =>
                {
                    // Set the default, which can be overridden.
                    options.ApiKey = rootConfig[SendGridExtensionConfigProvider.AzureWebJobsSendGridApiKeyName];

                    IConfigurationSection section = rootConfig.GetSection(extensionPath);
                    SendGridHelpers.ApplyConfiguration(section, options);
                });

            builder.Services.TryAddSingleton<ISendGridClientFactory, DefaultSendGridClientFactory>();
            builder.Services.TryAddSingleton<ISendGridResponseHandler, DefaultSendGridResponseHandler>();

            return builder;
        }

        /// <summary>
        /// Adds the SendGrid extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{SendGridOptions}"/> to configure the provided <see cref="SendGridOptions"/>.</param>
        public static IWebJobsBuilder AddSendGrid(this IWebJobsBuilder builder, Action<SendGridOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddSendGrid();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
