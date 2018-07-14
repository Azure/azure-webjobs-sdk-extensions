// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for Http integration
    /// </summary>
    public static class HttpHostBuilderExtensions
    {
        /// <summary>
        /// Adds the HTTP services and extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        public static IHostBuilder AddHttp(this IHostBuilder builder)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            builder.AddExtension<HttpExtensionConfigProvider>();
            builder.ConfigureServices(c =>
            {
                c.AddSingleton<IBindingProvider, HttpDirectRequestBindingProvider>();
            });

            return builder;
        }

        /// <summary>
        /// Adds the HTTP services and extension to the provided <see cref="IHostBuilder"/>.
        /// </summary>
        /// <param name="builder">The <see cref="IHostBuilder"/> to configure.</param>
        /// <param name="configure">An <see cref="Action{HttpExtensionOptions}"/> to configure the provided <see cref="HttpOptions"/>.</param>
        public static IHostBuilder AddHttp(this IHostBuilder builder, Action<HttpOptions> configure)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            builder.AddHttp()
                .ConfigureServices(c => c.Configure(configure));

            return builder;
        }
    }
}
