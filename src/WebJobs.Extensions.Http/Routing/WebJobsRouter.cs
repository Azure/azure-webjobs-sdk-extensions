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

        public WebJobsRouter(IInlineConstraintResolver constraintResolver)
        {
            _routeCollection = new RouteCollection();
            _constraintResolver = constraintResolver;
        }

        public IInlineConstraintResolver ConstraintResolver => _constraintResolver;

        public void ClearRoutes() =>
            _routeCollection = new RouteCollection();

        public VirtualPathData GetVirtualPath(VirtualPathContext context) =>
            _routeCollection.GetVirtualPath(context);

        public Task RouteAsync(RouteContext context) =>
            _routeCollection.RouteAsync(context);

        public void AddFunctionRoute(IRouter route) =>
            _routeCollection.Add(route);

        public WebJobsRouteBuilder CreateBuilder(IWebJobsRouteHandler routeHandler, string routePrefix)
        {
            return new WebJobsRouteBuilder(_constraintResolver, routeHandler, routePrefix);
        }
    }
}
