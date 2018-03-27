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

        public WebJobsRouteBuilder CreateBuilder(IWebJobsRouteHandler routeHandler, string routePrefix)
        {
            return new WebJobsRouteBuilder(_constraintResolver, routeHandler, routePrefix);
        }
    }
}
