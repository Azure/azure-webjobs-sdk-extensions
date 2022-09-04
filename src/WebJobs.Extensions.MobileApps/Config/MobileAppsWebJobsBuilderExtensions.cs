// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Mobile Apps integration
    /// </summary>
    public static class MobileAppsWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the Mobile Apps extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddMobileApps(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<MobileAppsExtensionConfigProvider>()
                .ConfigureOptions<MobileAppsOptions>((rootConfig, extensionPath, options) =>
                {
                    options.ApiKey = rootConfig[MobileAppsExtensionConfigProvider.AzureWebJobsMobileAppApiKeyName];
                    options.MobileAppUri = rootConfig.GetSection(MobileAppsExtensionConfigProvider.AzureWebJobsMobileAppUriName).Get<Uri>();

                    IConfigurationSection section = rootConfig.GetSection(extensionPath);
                    section.Bind(options);
                });

            builder.Services.AddSingleton<IMobileServiceClientFactory, DefaultMobileServiceClientFactory>();

            return builder;
        }

        /// <summary>
        /// Adds the Mobile Apps extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{MobileAppsOptions}"/> to configure the provided <see cref="MobileAppsOptions"/>.</param>
        public static IWebJobsBuilder AddMobileApps(this IWebJobsBuilder builder, Action<MobileAppsOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddMobileApps();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
