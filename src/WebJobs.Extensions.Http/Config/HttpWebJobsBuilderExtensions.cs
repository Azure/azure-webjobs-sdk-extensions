// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Http integration
    /// </summary>
    public static class HttpWebJobsBuilderExtensions
    {
        /// <summary>
        /// Adds the HTTP services and extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        public static IWebJobsBuilder AddHttp(this IWebJobsBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<HttpExtensionConfigProvider>()
                .BindOptions<HttpOptions>();

            builder.Services.AddSingleton<IBindingProvider, HttpDirectRequestBindingProvider>();

            return builder;
        }

        /// <summary>
        /// Adds the HTTP services and extension to the provided <see cref="IWebJobsBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IWebJobsBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{HttpExtensionOptions}"/> to configure the provided <see cref="HttpOptions"/>.</param>
        public static IWebJobsBuilder AddHttp(this IWebJobsBuilder builder, Action<HttpOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddHttp();
            builder.Services.Configure(configure);

            return builder;
        }
    }
}
