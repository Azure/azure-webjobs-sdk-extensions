// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Routing;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public interface IWebJobsRouter : IRouter
    {
        IInlineConstraintResolver ConstraintResolver { get; }

        void AddFunctionRoutes(IRouter functionRoutes, IRouter proxyRoutes);

        void AddCustomRoutes(IRouter customRoutes);

        void ClearRoutes();

        WebJobsRouteBuilder CreateBuilder(IWebJobsRouteHandler routeHandler, string routePrefix);

        string GetFunctionRouteTemplate(string functionName);
    }
}