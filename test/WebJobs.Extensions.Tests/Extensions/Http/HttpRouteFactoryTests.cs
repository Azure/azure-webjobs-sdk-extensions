// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Routing;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    public class HttpRouteFactoryTests
    {
        [Theory]
        [InlineData(null, "")]
        [InlineData("", "")]
        [InlineData("testfunction", "")]
        [InlineData("a/{b}/c/{d}", "b,d")]
        [InlineData("a/{b:alpha}/c/{d:int?}", "b,d")]
        public void GetRouteParameters_ReturnsExpectedResult(string routeTemplate, string expected)
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");
            var parameters = routeFactory.GetRouteParameters(routeTemplate);
            var result = string.Join(",", parameters);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TryAddRoute_AmbiguousRoute_FirstRouteIsChosen()
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");

            HttpRouteCollection routes = new HttpRouteCollection();
            IHttpRoute route1, route2;
            Assert.True(routeFactory.TryAddRoute("route1", "foo/bar/baz", null, null, routes, out route1));
            Assert.True(routeFactory.TryAddRoute("route2", "foo/bar/baz", null, null, routes, out route2));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://host/api/foo/bar/baz");
            var routeData = routes.GetRouteData(request);
            Assert.Same(route1, routeData.Route);
        }

        [Fact]
        public void TryAddRoute_AppliesHttpMethodConstraint()
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");

            HttpRouteCollection routes = new HttpRouteCollection();
            IHttpRoute route1, route2;
            Assert.True(routeFactory.TryAddRoute("route1", "products/{category}/{id?}", new HttpMethod[] { HttpMethod.Get }, null, routes, out route1));
            Assert.True(routeFactory.TryAddRoute("route2", "products/{category}/{id}", new HttpMethod[] { HttpMethod.Post }, null, routes, out route2));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://host/api/products/electronics/123");
            var routeData = routes.GetRouteData(request);
            Assert.Same(route1, routeData.Route);

            request = new HttpRequestMessage(HttpMethod.Get, "http://host/api/products/electronics");
            routeData = routes.GetRouteData(request);
            Assert.Same(route1, routeData.Route);

            request = new HttpRequestMessage(HttpMethod.Post, "http://host/api/products/electronics/123");
            routeData = routes.GetRouteData(request);
            Assert.Same(route2, routeData.Route);
        }

        [Fact]
        public void TryAddRoute_MethodsCollectionNull_DoesNotApplyHttpMethodConstraint()
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");

            HttpRouteCollection routes = new HttpRouteCollection();
            IHttpRoute route = null;
            Assert.True(routeFactory.TryAddRoute("route1", "products/{category}/{id?}", null, null, routes, out route));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://host/api/products/electronics/123");
            var routeData = routes.GetRouteData(request);
            Assert.Same(route, routeData.Route);

            request = new HttpRequestMessage(HttpMethod.Post, "http://host/api/products/electronics/123");
            routeData = routes.GetRouteData(request);
            Assert.Same(route, routeData.Route);
        }

        [Fact]
        public void TryAddRoute_MethodsCollectionEmpty_AppliesHttpMethodConstraint()
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");

            HttpRouteCollection routes = new HttpRouteCollection();
            IHttpRoute route = null;
            Assert.True(routeFactory.TryAddRoute("route1", "products/{category}/{id?}", new HttpMethod[0], null, routes, out route));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://host/api/products/electronics/123");
            var routeData = routes.GetRouteData(request);
            Assert.Null(routeData);

            request = new HttpRequestMessage(HttpMethod.Post, "http://host/api/products/electronics/123");
            routeData = routes.GetRouteData(request);
            Assert.Null(routeData);
        }

        [Fact]
        public void TryAddRoute_AppliesDataTokens()
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");
            HttpRouteCollection routes = new HttpRouteCollection();
            var dataTokens = new Dictionary<string, object>
            {
                { "Test", "TestValue" }
            };
            IHttpRoute route = null;
            Assert.True(routeFactory.TryAddRoute("route1", "products/{category}/{id?}", new HttpMethod[] { HttpMethod.Get }, dataTokens, routes, out route));

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, "http://host/api/products/electronics/123");
            var routeData = routes.GetRouteData(request);
            var testToken = routeData.Route.DataTokens["Test"];
            Assert.Equal("TestValue", testToken);
        }

        [Fact]
        public void TryAddRoute_RouteParsingError_ReturnsFalse()
        {
            HttpRouteFactory routeFactory = new HttpRouteFactory("api");

            HttpRouteCollection routes = new HttpRouteCollection();
            IHttpRoute route = null;
            Assert.False(routeFactory.TryAddRoute("route1", "/", new HttpMethod[0], null, routes, out route));
            Assert.Equal(0, routes.Count);
        }
    }
}
