// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public class WebJobsRouter : IWebJobsRouter
    {
        private readonly IInlineConstraintResolver _constraintResolver;
        private RouteCollection _functionRoutes;
        private RouteCollection _proxyRoutes;
        private RouteCollection _routeCollection;
        private RouteCollection _routeCollectionReverse;

        public WebJobsRouter(IInlineConstraintResolver constraintResolver)
        {
            InitializeRouteCollections();

            _constraintResolver = constraintResolver;
        }

        public IInlineConstraintResolver ConstraintResolver => _constraintResolver;

        private void InitializeRouteCollections()
        {
            _functionRoutes = new RouteCollection();
            _proxyRoutes = new RouteCollection();

            // Default route collection
            _routeCollection = new RouteCollection();
            _routeCollection.Add(_proxyRoutes);
            _routeCollection.Add(_functionRoutes);

            // Reverse route collection (functions taking priority)
            _routeCollectionReverse = new RouteCollection();
            _routeCollectionReverse.Add(_functionRoutes);
            _routeCollectionReverse.Add(_proxyRoutes);
        }

        public void ClearRoutes() =>
            InitializeRouteCollections();

        public VirtualPathData GetVirtualPath(VirtualPathContext context) =>
            _routeCollection.GetVirtualPath(context);

        public Task RouteAsync(RouteContext context)
        {
            // /admin/* routes should not be allowed to be overriden by proxies or function routes.
            var path = context.HttpContext.Request.Path;
            if (path.StartsWithSegments(new PathString("/admin"), System.StringComparison.OrdinalIgnoreCase))
            {
                // admin/warmup is handled by function routes
                if (!path.StartsWithSegments(new PathString("/admin/warmup"), System.StringComparison.OrdinalIgnoreCase))
                {
                    return Task.CompletedTask;
                }
            }

            // If this key is set in HttpContext, we first match against Function routes then Proxies.
            if (context.HttpContext.Items.ContainsKey(HttpExtensionConstants.AzureWebJobsUseReverseRoutesKey))
            {
                return _routeCollectionReverse.RouteAsync(context);
            }

            return _routeCollection.RouteAsync(context);
        }

        public void AddFunctionRoutes(IRouter functionRoutes, IRouter proxyRoutes)
        {
            if (proxyRoutes != null)
            {
                _proxyRoutes.Add(proxyRoutes);
            }

            if (functionRoutes != null)
            {
                _functionRoutes.Add(functionRoutes);
            }
        }

        public string GetFunctionRouteTemplate(string functionName)
        {
            if (functionName == null)
            {
                throw new ArgumentNullException(nameof(functionName));
            }

            if (_routeCollection == null)
            {
                return null;
            }

            return GetFunctionRouteTemplate(_routeCollection, functionName);
        }

        private string GetFunctionRouteTemplate(RouteCollection routes, string functionName)
        {
            for (int i = 0; i < routes.Count; i++)
            {
                switch (routes[i])
                {
                    case Route functionRoute when IsFunctionRoute(functionRoute, functionName):
                        return functionRoute.RouteTemplate;
                    case RouteCollection collection:
                        string template = GetFunctionRouteTemplate(collection, functionName);
                        if (template != null)
                        {
                            return template;
                        }
                        break;
                }
            }

            return null;
        }

        private bool IsFunctionRoute(Route functionRoute, string functionName)
        {
            return functionRoute.DataTokens.TryGetValue(HttpExtensionConstants.FunctionNameRouteTokenKey, out object routeFunctionName) &&
                string.Equals((string)routeFunctionName, functionName, StringComparison.OrdinalIgnoreCase);
        }

        public WebJobsRouteBuilder CreateBuilder(IWebJobsRouteHandler routeHandler, string routePrefix)
        {
            return new WebJobsRouteBuilder(_constraintResolver, routeHandler, routePrefix);
        }
    }
}
