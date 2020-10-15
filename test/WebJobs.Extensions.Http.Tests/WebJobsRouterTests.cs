// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http;
using Moq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Tests
{
    public class WebJobsRouterTests
    {
        [Fact]
        public void GetFunctionRoutes()
        {
            // Arrange
            var constraintResolver = new Mock<IInlineConstraintResolver>();
            var handler = new Mock<IWebJobsRouteHandler>();
            IWebJobsRouter router = new WebJobsRouter(constraintResolver.Object);

            var builder = router.CreateBuilder(handler.Object, "api");
            builder.MapFunctionRoute("testfunction", "test/{token}", "testfunction");

            router.AddFunctionRoutes(builder.Build(), null, null);

            // Act
            string routeTemplate = router.GetFunctionRouteTemplate("testfunction");

            // Assert
            Assert.Equal("api/test/{token}", routeTemplate);
        }

        [Fact]
        public void GetFunctionWithWarmupRoute()
        {
            var constraintResolver = new Mock<IInlineConstraintResolver>();
            var handler = new Mock<IWebJobsRouteHandler>();
            IWebJobsRouter router = new WebJobsRouter(constraintResolver.Object);

            var builder = router.CreateBuilder(handler.Object, "api");
            builder.MapFunctionRoute("warmuproute", "warmup", "warmuproute");

            var warmupBuilder = router.CreateBuilder(handler.Object, "admin");
            warmupBuilder.MapFunctionRoute("warmup", "warmup", "warmup");

            router.AddFunctionRoutes(builder.Build(), null, warmupBuilder.Build());

            string routeTemplate = router.GetFunctionRouteTemplate("warmuproute");

            Assert.Equal("api/warmup", routeTemplate);

            var req = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/admin/host/ping");
            RouteContext rc = new RouteContext(req.HttpContext);
            var r = router.RouteAsync(rc);
            Assert.True(r.IsCompletedSuccessfully == true);

            req = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/admin/warmup");
            rc = new RouteContext(req.HttpContext);
            r = router.RouteAsync(rc);
            Assert.True(r.IsCompletedSuccessfully == false);

            req = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/api/warmup");
            rc = new RouteContext(req.HttpContext);
            r = router.RouteAsync(rc);
            Assert.True(r.IsCompletedSuccessfully == false);
        }
    }
}
