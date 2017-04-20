// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Routing;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Used to create http routes.
    /// </summary>
    public class HttpRouteFactory
    {
        private readonly DirectRouteFactoryContext _routeFactoryContext;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="prefix">The route prefix to apply to all routes.</param>
        public HttpRouteFactory(string prefix = HttpExtensionConstants.DefaultRoutePrefix)
        {
            var constraintResolver = new DefaultInlineConstraintResolver();
            List<HttpActionDescriptor> actionDescriptors = new List<HttpActionDescriptor>();
            _routeFactoryContext = new DirectRouteFactoryContext(prefix, actionDescriptors, constraintResolver, false);
        }

        /// <summary>
        /// Create an <see cref="IDirectRouteBuilder"/> for the specified route template.
        /// </summary>
        /// <param name="routeTemplate">The route template.</param>
        /// <returns></returns>
        public IDirectRouteBuilder CreateRouteBuilder(string routeTemplate)
        {
            return _routeFactoryContext.CreateBuilder(routeTemplate);
        }

        /// <summary>
        /// Try to add the specified route to the route collection.
        /// </summary>
        /// <param name="routeName">The name of the route.</param>
        /// <param name="routeTemplate">The route template.</param>
        /// <param name="methods">The optional http methods to allow for the route.</param>
        /// <param name="routes">The routes collection to add to.</param>
        /// <param name="route">The route that was added.</param>
        /// <returns>True if the route was added successfully, false otherwise.</returns>
        public bool TryAddRoute(string routeName, string routeTemplate, IEnumerable<HttpMethod> methods, HttpRouteCollection routes, out IHttpRoute route)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            route = null;

            try
            {
                var routeBuilder = CreateRouteBuilder(routeTemplate);
                var constraints = routeBuilder.Constraints;
                if (methods != null)
                {
                    // if the methods collection is not null, apply the constraint
                    // if the methods collection is empty, we'll create a constraint
                    // that disallows ALL methods
                    constraints.Add("httpMethod", new HttpMethodConstraint(methods.ToArray()));
                }
                route = routes.CreateRoute(routeBuilder.Template, routeBuilder.Defaults, constraints);
                routes.Add(routeName, route);
            }
            catch
            {
                // catch any route parsing errors
                return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the route parameter defined in the specified template.
        /// </summary>
        /// <param name="routeTemplate">The route template.</param>
        /// <returns></returns>
        public IEnumerable<string> GetRouteParameters(string routeTemplate)
        {
            var routeBuilder = CreateRouteBuilder(routeTemplate);

            // this template will have any inline constraints parsed
            // out at this point
            return ParseRouteParameters(routeBuilder.Template);
        }

        private static IEnumerable<string> ParseRouteParameters(string routeTemplate)
        {
            List<string> routeParameters = new List<string>();

            if (!string.IsNullOrEmpty(routeTemplate))
            {
                string[] segments = routeTemplate.Split('/');
                foreach (string segment in segments)
                {
                    if (segment.StartsWith("{", StringComparison.OrdinalIgnoreCase) && segment.EndsWith("}", StringComparison.OrdinalIgnoreCase))
                    {
                        string parameter = segment.Substring(1, segment.Length - 2);
                        routeParameters.Add(parameter);
                    }
                }
            }

            return routeParameters;
        }
    }
}
