// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public class WebJobsRouteBuilder
    {
        private readonly IWebJobsRouteHandler _handler;
        private readonly List<Route> _routes = new List<Route>();
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
                { HttpExtensionConstants.FunctionNameRouteTokenKey, functionName }
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

            var routePrecedence = Comparer<Route>.Create(RouteComparison);
            _routes.Sort(routePrecedence);

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

        // intelligently order routes by order of specificity:
        // - prefer static segments over parameterized segments
        // - prefer more specific (longer) routes over less specific (shorter) routes
        private static int RouteComparison(Route x, Route y)
        {
            var xTemplate = x.ParsedTemplate;
            var yTemplate = y.ParsedTemplate;

            for (var i = 0; i < xTemplate.Segments.Count; i++)
            {
                if (yTemplate.Segments.Count <= i)
                {
                    return -1;
                }

                var xSegment = xTemplate.Segments[i].Parts[0];
                var ySegment = yTemplate.Segments[i].Parts[0];
                if (!xSegment.IsParameter && ySegment.IsParameter)
                {
                    return -1;
                }
                if (xSegment.IsParameter && !ySegment.IsParameter)
                {
                    return 1;
                }

                if (xSegment.IsParameter)
                {
                    if (xSegment.InlineConstraints.Count() > ySegment.InlineConstraints.Count())
                    {
                        return -1;
                    }
                    else if (xSegment.InlineConstraints.Count() < ySegment.InlineConstraints.Count())
                    {
                        return 1;
                    }
                }
                else
                {
                    var comparison = string.Compare(xSegment.Text, ySegment.Text, StringComparison.OrdinalIgnoreCase);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }
            }
            if (yTemplate.Segments.Count > xTemplate.Segments.Count)
            {
                return 1;
            }
            return 0;
        }
    }
}
