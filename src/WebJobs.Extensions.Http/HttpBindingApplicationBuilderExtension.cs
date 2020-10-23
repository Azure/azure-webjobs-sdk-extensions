// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public static class HttpBindingApplicationBuilderExtension
    {
        /// <summary>
        /// Adds the WebJobs HTTP Binding to the <seealso cref="IApplicationBuilder"/> execution pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="applicationLifetime">The application lifetime instance.</param>
        /// <param name="routes">A route configuration handler.</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseHttpBinding(this IApplicationBuilder builder, IApplicationLifetime applicationLifetime, Action<WebJobsRouteBuilder> routes)
            => UseHttpBindingRouting(builder, applicationLifetime, routes);

        /// <summary>
        /// Adds the WebJobs HTTP Binding routing feature to the <seealso cref="IApplicationBuilder"/> execution pipeline.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="applicationLifetime">The application lifetime instance.</param>
        /// <param name="routes">A route configuration handler.</param>
        /// <returns>The updated <see cref="IApplicationBuilder"/>.</returns>
        public static IApplicationBuilder UseHttpBindingRouting(this IApplicationBuilder builder, IApplicationLifetime applicationLifetime, Action<WebJobsRouteBuilder> routes)
        {
            var router = builder.ApplicationServices.GetRequiredService<IWebJobsRouter>();

            if (routes != null)
            {
                var routeHandler = builder.ApplicationServices.GetRequiredService<IWebJobsRouteHandler>();

                var routeBuilder = new WebJobsRouteBuilder(builder, routeHandler);
                routes.Invoke(routeBuilder);

                router.AddFunctionRoutes(routeBuilder.Build(), null);
            }

            builder.UseRouter(router);

            return builder;
        }
    }
}
