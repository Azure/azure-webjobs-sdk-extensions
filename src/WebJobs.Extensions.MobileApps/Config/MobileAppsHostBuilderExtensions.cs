// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Mobile Apps integration
    /// </summary>
    public static class MobileAppsHostBuilderExtensions
    {
        /// <summary>
        /// Adds the Mobile Apps extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        public static IHostBuilder AddMobileApps(this IHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<MobileAppsExtensionConfigProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the Mobile Apps extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{MobileAppsOptions}"/> to configure the provided <see cref="MobileAppsOptions"/>.</param>
        public static IHostBuilder AddMobileApps(this IHostBuilder builder, Action<MobileAppsOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddMobileApps()
                .ConfigureServices(c => c.Configure(configure));

            return builder;
        }
    }
}
