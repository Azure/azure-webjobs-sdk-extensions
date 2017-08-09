// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public class WebJobsRouteBuilder
    {
        private readonly IWebJobsRouteHandler _handler;
        private readonly List<IRouter> _routes = new List<IRouter>();
        private readonly IInlineConstraintResolver _constraintResolver;
        private readonly string _routePrefix;

        public WebJobsRouteBuilder(IApplicationBuilder applicationBuilder, IWebJobsRouteHandler handler)
            : this(applicationBuilder, handler, null)
        {
        }

        public WebJobsRouteBuilder(IApplicationBuilder applicationBuilder, IWebJobsRouteHandler handler, string routePrefix)
            : this(applicationBuilder.ApplicationServices.GetRequiredService<IInlineConstraintResolver>(), handler, routePrefix)
        {
        }

        public WebJobsRouteBuilder(IInlineConstraintResolver constraintResolver, IWebJobsRouteHandler handler, string routePrefix)
        {
            _handler = handler ?? throw new ArgumentNullException(nameof(handler));
            _constraintResolver = constraintResolver;
            _routePrefix = routePrefix;
        }

        public int Count => _routes.Count;

        public void MapFunctionRoute(string name, string template, string functionName)
        {
            MapFunctionRoute(name, template, null, null, null, functionName);
        }

        public void MapFunctionRoute(string name, string template, object constraints, string functionName)
        {
            MapFunctionRoute(name, template, null, constraints, null, functionName);
        }

        public void MapFunctionRoute(
        string name,
        string template,
        object defaults,
        object constraints,
        object dataTokens,
        string functionName)
        {
            var tokens = new RouteValueDictionary(dataTokens)
            {
                { "AZUREWEBJOBS_FUNCTIONNAME", functionName }
            };

            template = BuildRouteTemplate(_routePrefix, template);

            _routes.Add(new Route(
                new RouteHandler(c => _handler.InvokeAsync(c, functionName)),
                name,
                template,
                new RouteValueDictionary(defaults),
                new RouteValueDictionary(constraints),
                tokens,
                _constraintResolver));
        }

        public IRouter Build()
        {
            var routes = new RouteCollection();

            foreach (var route in _routes)
            {
                routes.Add(route);
            }

            return routes;
        }

        private static string BuildRouteTemplate(string routePrefix, string routeTemplate)
        {
            return string.IsNullOrEmpty(routePrefix)
                ? routeTemplate
                : routePrefix + '/' + routeTemplate;
        }
    }
}
