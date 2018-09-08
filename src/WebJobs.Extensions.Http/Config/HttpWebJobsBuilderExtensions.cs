// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http.Formatting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host.Bindings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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

            // Compatibility shim configuration and services
            builder.Services.TryAddSingleton<IContentNegotiator, DefaultContentNegotiator>();
            builder.Services.Configure<MvcOptions>(o =>
            {
                o.OutputFormatters.Insert(0, new HttpResponseMessageOutputFormatter());
            });

            builder.Services.Configure<WebApiCompatShimOptions>(o =>
            {
                o.Formatters.AddRange(new MediaTypeFormatterCollection());
            });

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
