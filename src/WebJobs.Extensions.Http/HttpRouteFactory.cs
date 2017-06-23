//// Copyright (c) .NET Foundation. All rights reserved.
//// Licensed under the MIT License. See License.txt in the project root for license information.

//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Net.Http;
//using Microsoft.AspNetCore.Routing;
//using Microsoft.AspNetCore.Routing.Constraints;
//using Microsoft.AspNetCore.Routing.Template;

//namespace Microsoft.Azure.WebJobs.Extensions.Http
//{
//    /// <summary>
//    /// Used to create http routes.
//    /// </summary>
//    public class HttpRouteFactory
//    {
//        private readonly DirectRouteFactoryContext _routeFactoryContext;

//        /// <summary>
//        /// Constructs a new instance.
//        /// </summary>
//        /// <param name="prefix">The route prefix to apply to all routes.</param>
//        public HttpRouteFactory(string prefix = HttpExtensionConstants.DefaultRoutePrefix)
//        {
//            var constraintResolver = new DefaultInlineConstraintResolver(null);
//            List<HttpActionDescriptor> actionDescriptors = new List<HttpActionDescriptor>();
//            _routeFactoryContext = new DirectRouteFactoryContext(prefix, actionDescriptors, constraintResolver, false);
//        }

//        /// <summary>
//        /// Create an <see cref="IDirectRouteBuilder"/> for the specified route template.
//        /// </summary>
//        /// <param name="routeTemplate">The route template.</param>
//        /// <returns></returns>
//        public IRouteBuilder CreateRouteBuilder(string routeTemplate)
//        {
//            return _routeFactoryContext.CreateBuilder(routeTemplate);
//        }

//        /// <summary>
//        /// Try to add the specified route to the route collection.
//        /// </summary>
//        /// <param name="routeName">The name of the route.</param>
//        /// <param name="routeTemplate">The route template.</param>
//        /// <param name="methods">The optional http methods to allow for the route.</param>
//        /// <param name="routes">The routes collection to add to.</param>
//        /// <param name="route">The route that was added.</param>
//        /// <returns>True if the route was added successfully, false otherwise.</returns>
//        public bool TryAddRoute(string routeName, string routeTemplate, IEnumerable<HttpMethod> methods, RouteCollection routes, out IRouter route)
//        {
//            if (routes == null)
//            {
//                throw new ArgumentNullException(nameof(routes));
//            }

//            route = null;

//            try
//            {
//                var routeBuilder = CreateRouteBuilder(routeTemplate);
//                Dictionary<string, object> constraints = null;
//                if (methods != null)
//                {
//                    // if the methods collection is not null, apply the constraint
//                    // if the methods collection is empty, we'll create a constraint
//                    // that disallows ALL methods
//                    constraints.Add("httpMethod", new HttpMethodRouteConstraint(methods.Select(m => m.ToString()).ToArray()));
//                }
                
//                var a = new Route(routeBuilder.DefaultHandler, routeName, routeTemplate, new RouteValueDictionary(), )
//                route = routes.Add(.CreateRoute(routeBuilder.Template, routeBuilder.Defaults, constraints);
//                routes.Add(routeName, route);
//            }
//            catch
//            {
//                // catch any route parsing errors
//                return false;
//            }

//            return true;
//        }

//        /// <summary>
//        /// Gets the route parameter defined in the specified template.
//        /// </summary>
//        /// <param name="routeTemplate">The route template.</param>
//        /// <returns></returns>
//        public IEnumerable<string> GetRouteParameters(string routeTemplate)
//        {
            
//            var result = TemplateParser.Parse(routeTemplate);
//            return result.Parameters.Select(p => p.Name);
//        }
//    }
//}
