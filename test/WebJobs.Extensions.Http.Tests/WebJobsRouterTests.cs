// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.AspNetCore.Routing;
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
            Mock<IInlineConstraintResolver> constraintResolver = new();
            Mock<IWebJobsRouteHandler> handler = new();
            WebJobsRouter router = new(constraintResolver.Object);

            var builder = router.CreateBuilder(handler.Object, "api");
            builder.MapFunctionRoute("testfunction", "test/{token}", "testfunction");

            router.AddFunctionRoutes(builder.Build(), null);

            // Act
            string routeTemplate = router.GetFunctionRouteTemplate("testfunction");

            // Assert
            Assert.Equal("api/test/{token}", routeTemplate);
        }

        [Fact]
        public void GetFunctionWithCustomRoute()
        {
            Mock<IInlineConstraintResolver> constraintResolver = new();
            Mock<IWebJobsRouteHandler> handler = new();
            WebJobsRouter router = new(constraintResolver.Object);

            var builder = router.CreateBuilder(handler.Object, "api");
            builder.MapFunctionRoute("warmuproute", "warmup", "warmuproute");

            var customBuilder = router.CreateBuilder(handler.Object, "admin");
            customBuilder.MapFunctionRoute("warmup", "warmup", "warmup");

            router.AddFunctionRoutes(builder.Build(), null);
            router.AddFunctionRoutes(customBuilder.Build(), null);

            string routeTemplate = router.GetFunctionRouteTemplate("warmuproute");

            Assert.Equal("api/warmup", routeTemplate);

            routeTemplate = router.GetFunctionRouteTemplate("warmup");
            Assert.Equal("admin/warmup", routeTemplate);
        }
    }
}
