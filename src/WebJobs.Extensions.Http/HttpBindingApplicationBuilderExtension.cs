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
        public static IApplicationBuilder UseHttpBindingRouting(this IApplicationBuilder builder, IApplicationLifetime applicationLifetime, Action<WebJobsRouteBuilder> routes)
        {
            var router = builder.ApplicationServices.GetRequiredService<IWebJobsRouter>();

            if (routes != null)
            {
                var routeHandler = builder.ApplicationServices.GetRequiredService<IWebJobsRouteHandler>();

                var routeBuilder = new WebJobsRouteBuilder(builder, routeHandler);
                routes.Invoke(routeBuilder);

                router.AddFunctionRoute(routeBuilder.Build());
            }

            builder.UseRouter(router);

            return builder;
        }
    }
}
