// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Extensions.Timers;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Timers integration
    /// </summary>
    public static class TimerHostBuilderExtensions
    {
        /// <summary>
        /// Adds the Timer extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        public static IHostBuilder AddTimers(this IHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<TimersExtensionConfigProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the Timer extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{TimersOptions}"/> to configure the provided <see cref="TimersOptions"/>.</param>
        public static IHostBuilder AddTimers(this IHostBuilder builder, Action<TimersOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddTimers()
                .ConfigureServices(c => c.Configure(configure));

            return builder;
        }
    }
}
