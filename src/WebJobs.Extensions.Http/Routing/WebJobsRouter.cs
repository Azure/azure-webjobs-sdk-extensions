// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

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
        private RouteCollection _routeCollection;
        private RouteCollection _routeCollectionReverse;

        public WebJobsRouter(IInlineConstraintResolver constraintResolver)
        {
            _routeCollection = new RouteCollection();
            _routeCollectionReverse = new RouteCollection();
            _constraintResolver = constraintResolver;
        }

        public IInlineConstraintResolver ConstraintResolver => _constraintResolver;

        public void ClearRoutes()
        {
            _routeCollection = new RouteCollection();
            _routeCollectionReverse = new RouteCollection();
        }

        public VirtualPathData GetVirtualPath(VirtualPathContext context) =>
            _routeCollection.GetVirtualPath(context);

        public Task RouteAsync(RouteContext context)
        {
            // If this key is set in HttpContext, we first match against Function routes then Proxies.
            if (context.HttpContext.Items.ContainsKey(HttpExtensionConstants.AzureWebJobsUseReverseRoutesKey) && _routeCollectionReverse.Count > 0)
            {
                return _routeCollectionReverse.RouteAsync(context);
            }

            return _routeCollection.RouteAsync(context);
        }

        public void AddFunctionRoutes(IRouter functionRoutes, IRouter proxyRoutes)
        {
            if (proxyRoutes != null)
            {
                _routeCollection.Add(proxyRoutes);
            }

            if (functionRoutes != null)
            {
                _routeCollection.Add(functionRoutes);
            }

            // We need the reverse collection only if both proxy and function routes are not null
            if (proxyRoutes != null && functionRoutes != null)
            {
                _routeCollectionReverse.Add(functionRoutes);
                _routeCollectionReverse.Add(proxyRoutes);
            }
        }

        public WebJobsRouteBuilder CreateBuilder(IWebJobsRouteHandler routeHandler, string routePrefix)
        {
            return new WebJobsRouteBuilder(_constraintResolver, routeHandler, routePrefix);
        }
    }
}
