// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Timers storage integration.
    /// </summary>
    public static class TimersStorageWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds Azure Storage based implementations for Timers services to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IWebJobsBuilder AddTimersStorage(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.Services.AddSingleton<ScheduleMonitor, StorageScheduleMonitor>();

            return builder;
        }

        /// <summary>
        /// Adds Azure Storage based implementations for Timers services to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{TimersOptions}"/> to configure the provided <see cref="TimersOptions"/>.</param>
        /// <remarks>
        /// Currently there are no configurable options on <see cref="TimersOptions"/> so this overload does not provide any utility.
        /// </remarks>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IWebJobsBuilder AddTimersStorage(this IWebJobsBuilder builder, Action<TimersOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddTimersStorage();
            builder.Services.Configure(configure);

            return builder;
        }

        /// <summary>
        /// Adds the Timer extension along with an Azure Storage backed implementation to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IWebJobsBuilder AddTimersWithStorage(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddTimers();
            builder.AddTimersStorage();

            return builder;
        }

        /// <summary>
        /// Adds the Timer extension along with an Azure Storage backed implementation to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{TimersOptions}"/> to configure the provided <see cref="TimersOptions"/>.</param>
        /// <remarks>
        /// Currently there are no configurable options on <see cref="TimersOptions"/> so this overload does not provide any utility.
        /// </remarks>
        /// <returns>A reference to this instance after the operation has completed.</returns>
        public static IWebJobsBuilder AddTimersWithStorage(this IWebJobsBuilder builder, Action<TimersOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddTimers();
            builder.AddTimersStorage();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
