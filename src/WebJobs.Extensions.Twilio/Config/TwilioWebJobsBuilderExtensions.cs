// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Twilio;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Twilio integration
    /// </summary>
    public static class TwilioWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the Twilio SMS extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddTwilioSms(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<TwilioExtensionConfigProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the Twilo SMS extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{TwilioSmsOptions}"/> to configure the provided <see cref="TwilioSmsOptions"/>.</param>
        public static IWebJobsBuilder AddTwilioSms(this IWebJobsBuilder builder, Action<TwilioSmsOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddTwilioSms();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
