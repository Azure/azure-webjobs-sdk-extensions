using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Extensions.Tests.Common;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Tests.Extensions.Http
{
    [Trait("Category", "E2E")]
    public class ClaimsPrincipalEndToEndTests
    {
        public static ClaimsPrincipal GlobalPrincipal;

        private const string UserAuthType = "aad";
        private const string UserClaimType = "name";
        private const string UserClaimValue = "Sample";
        private const string Name = "sample@microsoft.com";
        private const string UserIdentityJson = @"{
  ""auth_typ"": """ + UserAuthType + @""",
  ""claims"": [
    {
      ""typ"": """ + UserClaimType + @""",
      ""val"": """ + UserClaimValue + @"""
    },
    {
      ""typ"": ""http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"",
      ""val"": """ + Name + @"""
    },
    {
      ""typ"": ""http://schemas.xmlsoap.org/ws/2005/05/identity/claims/upn"",
      ""val"": """ + Name + @"""
    }
  ],
  ""name_typ"": ""http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"",
  ""role_typ"": ""http://schemas.microsoft.com/ws/2008/06/identity/claims/role""
}";

        private const string KeyAuthType = "key";
        private const string KeyIdClaimType = "urn:functions:keyId";
        private const string KeyIdClaimValue = "master";
        private const string KeyLevelClaimType = "urn:functions:authLevel";
        private const string KeyLevelClaimValue = "admin";
        private const string KeyIdentityJson = @"{
  ""auth_typ"": """ + KeyAuthType + @""",
  ""claims"": [
    {
      ""typ"": """ + KeyIdClaimType + @""",
      ""val"": """ + KeyIdClaimValue + @"""
    },
    {
      ""typ"": """ + KeyLevelClaimType + @""",
      ""val"": """ + KeyLevelClaimValue + @"""
    }
  ],
  ""name_typ"": ""http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name"",
  ""role_typ"": ""http://schemas.microsoft.com/ws/2008/06/identity/claims/role""
}";


        private const string OutputTestPath = @"webjobs_extensionstests\filebindinge2e_output";

        private JobHostConfiguration _config;
        private JobHost _host;

        public ClaimsPrincipalEndToEndTests()
        {
            var httpConfig = new HttpExtensionConfiguration();
            _config = new JobHostConfiguration
            {
                TypeLocator = new ExplicitTypeLocator(typeof(TestFunctions))
            };
            _config.UseHttp(httpConfig);
            _host = new JobHost(_config);
        }

        [Fact]
        public void ClaimsPrincipalBindingWithEasyAuth()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://functions.com/api/TestIdentityBindings");
            string headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(UserIdentityJson));
            request.Headers.Add("x-ms-client-principal", headerValue);

            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequest));
            _host.Call(method, new { req = request });

            Assert.Equal(Name, GlobalPrincipal.Identity.Name);
            Assert.Equal(UserAuthType, GlobalPrincipal.Identity.AuthenticationType);
            Claim testClaim = GlobalPrincipal.Claims.Where(claim => string.Equals(claim.Type, UserClaimType)).First();
            Assert.Equal(testClaim.Value, UserClaimValue);
        }

        [Fact]
        public void ClaimsPrincipalBindingWithFunctionsKey()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://functions.com/api/TestIdentityBindings");
            string headerValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(KeyIdentityJson));
            request.Headers.Add("x-ms-functions-key-identity", headerValue);

            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequest));
            _host.Call(method, new { req = request });

            Assert.Equal(KeyAuthType, GlobalPrincipal.Identity.AuthenticationType);
            Claim idClaim = GlobalPrincipal.Claims.Where(claim => string.Equals(claim.Type, KeyIdClaimType)).First();
            Assert.Equal(idClaim.Value, KeyIdClaimValue);
            Claim levelClaim = GlobalPrincipal.Claims.Where(claim => string.Equals(claim.Type, KeyLevelClaimType)).First();
            Assert.Equal(levelClaim.Value, KeyLevelClaimValue);
        }

        [Fact]
        public void CLaimsPrincipalBindingWithBothEasyAuthAndFunctionsKey()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://functions.com/api/TestIdentityBindings");
            string easyAuthHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(UserIdentityJson));
            request.Headers.Add("x-ms-client-principal", easyAuthHeaderValue);
            string keyHeaderValue = Convert.ToBase64String(Encoding.UTF8.GetBytes(KeyIdentityJson));
            request.Headers.Add("x-ms-functions-key-identity", keyHeaderValue);

            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequest));
            _host.Call(method, new { req = request });

            Assert.Equal(Name, GlobalPrincipal.Identity.Name);
            Assert.Equal(2, GlobalPrincipal.Identities.Count());
        }

        [Fact]
        public void ClaimsPrincipalBindingWithNoIdentities()
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "http://functions.com/api/TestIdentityBindings");
            var method = typeof(TestFunctions).GetMethod(nameof(TestFunctions.HttpRequest));
            _host.Call(method, new { req = request });

            Assert.Null(GlobalPrincipal.Identity);
        }

        [Fact]
        public async void ClaimsPrincipalBindingNoHttpRequestNoClaimsPrincipalProvided()
        {
            await RunTimerJobTest(
                typeof(TestFunctions),
                () =>
                {
                    return TestFunctions.InvocationCount >= 1;
                });

            Assert.Null(GlobalPrincipal.Identity);
        }
        private async Task RunTimerJobTest(Type jobClassType, Func<bool> condition)
        {
            TestTraceWriter testTrace = new TestTraceWriter(TraceLevel.Error);
            ExplicitTypeLocator locator = new ExplicitTypeLocator(jobClassType);
            var httpConfig = new HttpExtensionConfiguration();
            JobHostConfiguration config = new JobHostConfiguration
            {
                TypeLocator = locator
            };
            config.UseTimers();
            config.UseHttp(httpConfig);
            config.Tracing.Tracers.Add(testTrace);
            JobHost host = new JobHost(config);

            await host.StartAsync();

            await TestHelpers.Await(() =>
            {
                return condition();
            });

            await host.StopAsync();

            // ensure there were no errors
            Assert.Equal(0, testTrace.Events.Count);
        }


        public static class TestFunctions
        {
            public static int InvocationCount = 0;

            public static void HttpRequest(
                [HttpTrigger("get")] HttpRequestMessage req,
                ClaimsPrincipal principal)
            {
                GlobalPrincipal = principal;
            }

            public static void NoHttpRequest(
                [TimerTrigger("0 0 */2 * * *", RunOnStartup = true)] TimerInfo timer, 
                ClaimsPrincipal principal)
            {
                GlobalPrincipal = principal;
                InvocationCount++;
            }
        }
    }
}
