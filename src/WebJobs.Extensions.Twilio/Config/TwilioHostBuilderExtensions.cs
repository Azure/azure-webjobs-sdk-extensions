// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Twilio;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Twilio integration
    /// </summary>
    public static class TwilioHostBuilderExtensions
    {
        /// <summary>
        /// Adds the Twilio SMS extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        public static IHostBuilder AddTwilioSms(this IHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<TwilioExtensionConfigProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the Twilo SMS extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{TwilioSmsOptions}"/> to configure the provided <see cref="TwilioSmsOptions"/>.</param>
        public static IHostBuilder AddTwilioSms(this IHostBuilder builder, Action<TwilioSmsOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddTwilioSms()
                .ConfigureServices(c => c.Configure(configure));

            return builder;
        }
    }
}
