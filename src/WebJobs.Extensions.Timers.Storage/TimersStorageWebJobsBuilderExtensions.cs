﻿// Copyright (c) .NET Foundation. All rights reserved.
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
        /// Adds the Timers Azure storage backed implementation to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <remarks>
        /// Note: Certain Azure services must be registered by the consumer to use the storage-backed implementation.
        /// This is done in <see path="" cref="RuntimeStorageWebJobsBuilderExtensions.AddAzureStorageCoreServices(IWebJobsBuilder)"/>.
        /// </remarks>
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
        /// Adds the Timers Azure storage backed implementation to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{TimersOptions}"/> to configure the provided <see cref="TimersOptions"/>.</param>
        /// <remarks>Note: Certain Azure services must be registered by the consumer to use the storage-backed implementation.
        /// This is done in <see path="" cref="RuntimeStorageWebJobsBuilderExtensions.AddAzureStorageCoreServices(IWebJobsBuilder)"/>.
        /// Currently there are no configurable options on <see cref="TimersOptions"/> so this overload does not provide any utility.
        /// </remarks>
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
        /// Adds the Timer extension along with Azure storage backed implementation to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <remarks>Note: Certain Azure services must be registered by the consumer to use the storage-backed implementation.</remarks>
        /// This is done in <see path="" cref="RuntimeStorageWebJobsBuilderExtensions.AddAzureStorageCoreServices(IWebJobsBuilder)"/>.
        public static IWebJobsBuilder AddTimersWithStorage(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddTimers();
            builder.Services.AddSingleton<ScheduleMonitor, StorageScheduleMonitor>();

            return builder;
        }
    }
}
