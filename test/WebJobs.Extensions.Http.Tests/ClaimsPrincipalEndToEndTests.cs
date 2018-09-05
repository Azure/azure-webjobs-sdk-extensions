// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Microsoft.Azure.WebJobs.Extensions.Timers;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Azure.WebJobs.Host.TestCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    [Trait("Category", "E2E")]
    public class ClaimsPrincipalEndToEndTests
    {
        private const string EasyAuthEnabledAppSetting = "WEBSITE_AUTH_ENABLED";
        private const string UserNameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";
        private const string UserRoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
        private const string UserAuthType = "aad";
        private const string UserNameClaimValue = "Connor McMahon";
        private const string UserRoleClaimValue = "Software Engineer";
      
        private JobHost GetJobHost(INameResolver resolver = null)
        {
            if (resolver == null)
            {
                var mockNameResolver = new Mock<INameResolver>();
                mockNameResolver.Setup(nameResolver => nameResolver.Resolve(EasyAuthEnabledAppSetting)).Returns("TRUE");
                resolver = mockNameResolver.Object;
            }
           
            var host = new HostBuilder()
                .ConfigureServices(configuration =>
                {
                    configuration.AddSingleton<INameResolver>(resolver);
                })
                .ConfigureDefaultTestHost(builder =>
                {
                    builder.AddHttp(o =>
                    {
                        o.SetResponse = SetResultHook;
                    })
                    .AddAzureStorageCoreServices()
                    .AddTimers()
                    .AddAzureStorage();
                }, typeof(TestFunctions))
                .Build();

            return host.GetJobHost();
        }

        [Fact]
        public async Task HttpTrigger_WithClaimsPrincipal_BindsClaimsPrincipalSuccessfully()
        {
            var request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/api/TestIdentityBindings");
            request.HttpContext.User = GetSamplePrincipal();

            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequest));
            await GetJobHost().CallAsync(method, new { req = request });

            var principal = GetResult(request);

            Assert.Equal(request.HttpContext.User, principal);
        }

        [Fact]
        public async Task HttpTrigger_WithNoIdentities_ClaimsPrincipalIsNotAuthenticated()
        {
            var request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/api/TestIdentityBindings");
            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequest));
            await GetJobHost().CallAsync(method, new { req = request });

            var principal = GetResult(request);
            Assert.False(principal.Identity.IsAuthenticated);
        }

        [Fact]
        public async Task HttpTrigger_WithClaimsPrincipal_HasIdentitiesBindingData()
        {
            var request = HttpTestHelpers.CreateHttpRequest("GET", "http://functions.com/api/TestIdentityBindings");
            request.HttpContext.User = GetSamplePrincipal();
            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequestWithIdentitiesBindingData));
            await GetJobHost().CallAsync(method, new { req = request });

            JObject[] identities = GetBindingDataResult(request);
            
            Assert.Equal(UserAuthType, identities[0]["authenticationType"]);
            var claims = identities[0]["claims"] as JArray;
            var nameClaim = claims.FirstOrDefault(claim => string.Equals(claim["type"].Value<string>(), UserNameClaimType));
            Assert.Equal(UserNameClaimValue, nameClaim["value"]);
            var roleCLaim = claims.FirstOrDefault(claim => string.Equals(claim["type"].Value<string>(), UserRoleClaimType));
            Assert.Equal(UserRoleClaimValue, roleCLaim["value"]);
        }

        [Fact]
        public async Task NonHttpTrigger_WithClaimsPrincipal_ExceptionIsThrown()
        {
            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.TimerTrigger));
            var timerInfo = new TimerInfo(new ConstantSchedule(new TimeSpan(1, 0, 0)), new ScheduleStatus() { Last = DateTime.Now.AddMinutes(-60), Next = DateTime.Now.AddMinutes(60), LastUpdated = DateTime.Now.AddDays(-1) });

            // The function throws an exception because ClaimsPrincipal can't be used with any other trigger type
            await Assert.ThrowsAsync<FunctionInvocationException>(async () => await GetJobHost().CallAsync(method, new { timer = timerInfo }));
        }

        private void SetResultHook(HttpRequest request, object result)
        {
            request.HttpContext.Items["$ret"] = result;
        }

        private ClaimsPrincipal GetResult(HttpRequest request)
        {
            return request.HttpContext.Items["$ret"] as ClaimsPrincipal;
        }

        private JObject[] GetBindingDataResult(HttpRequest request)
        {
            return request.HttpContext.Items["$ret"] as JObject[];
        }

        private ClaimsPrincipal GetSamplePrincipal()
        {
            ClaimsIdentity identity = new ClaimsIdentity(authenticationType: UserAuthType, nameType: UserNameClaimType, roleType: UserRoleClaimType);
            identity.AddClaim(new Claim(UserNameClaimType, UserNameClaimValue));
            identity.AddClaim(new Claim(UserRoleClaimType, UserRoleClaimValue));
            return new ClaimsPrincipal(identity);
        }

        public static class TestFunctions
        {
            internal static int InvocationCount { get; set; } = 0;

            public static ClaimsPrincipal HttpRequest(
                [HttpTrigger("get")] HttpRequestMessage req,
                ClaimsPrincipal principal)
            {
                return principal;
            }

            public static JObject[] HttpRequestWithIdentitiesBindingData(
                [HttpTrigger("get")] HttpRequestMessage req,
                JObject[] identities)
            {
                return identities;
            }

            public static void TimerTrigger(
                [TimerTrigger("0 0 */2 * * *", RunOnStartup = true)] TimerInfo timer,
                ClaimsPrincipal principal)
            {
            }
        }
    }
}
