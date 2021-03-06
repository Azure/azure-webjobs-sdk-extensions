﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Tests
{
    public class HttpRequestExtensionTests
    {
        [Fact]
        public void GetAppServiceIdentity_XMsClientPrincipalCorrectEasyAuthFormat_ReturnsEasyAuthIdentity()
        {
            HttpRequest req = new DefaultHttpContext().Request;
            var claims = new List<Claim>();
            claims.Add(new Claim("name", "Connor McMahon"));
            claims.Add(new Claim("role", "Software Engineer"));
            var identity = new ClaimsIdentity(authenticationType: "aad", nameType: "name", roleType: "role", claims: claims);

            //Load onto header
            string json = JsonConvert.SerializeObject(ClaimsIdentitySlim.FromClaimsIdentity(identity));
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            ClaimsIdentity easyAuthIdentity = req.GetAppServiceIdentity();
            Assert.NotNull(easyAuthIdentity);
            Assert.Equal("aad", easyAuthIdentity.AuthenticationType);
            Assert.Equal("name", easyAuthIdentity.NameClaimType);
            Assert.Equal("role", easyAuthIdentity.RoleClaimType);
            var claim1 = easyAuthIdentity.Claims.ElementAt(0);
            Assert.Equal("name", claim1.Type);
            Assert.Equal("Connor McMahon", claim1.Value);
            var claim2 = easyAuthIdentity.Claims.ElementAt(1);
            Assert.Equal("role", claim2.Type);
            Assert.Equal("Software Engineer", claim2.Value);
        }

        [Fact]
        public void GetAppServiceIdentity_XMsClientPrincipalCorrectStaticWebAppsFormat_ReturnsStaticWebAppsIdentity()
        {
            HttpRequest req = new DefaultHttpContext().Request;

            var staticWebAppsClientPrincipal = new StaticWebAppsClientPrincipal
            {
                IdentityProvider = "facebook",
                UserId = "50cf51ecad1a49429e35243afde6b92b",
                UserDetails = "mikarmar@microsoft.com",
                UserRoles = new List<string>
                {
                    "admin",
                    "super_admin",
                },
            };

            //Load onto header
            string json = JsonConvert.SerializeObject(staticWebAppsClientPrincipal);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            ClaimsIdentity staticWebAppsIdentity = req.GetAppServiceIdentity();
            Assert.NotNull(staticWebAppsIdentity);
            Assert.Equal("facebook", staticWebAppsIdentity.AuthenticationType);
            var userIdClaim = staticWebAppsIdentity.Claims.ElementAt(0);
            Assert.Equal(ClaimTypes.NameIdentifier, userIdClaim.Type);
            Assert.Equal("50cf51ecad1a49429e35243afde6b92b", userIdClaim.Value);
            var userDetailsClaim = staticWebAppsIdentity.Claims.ElementAt(1);
            Assert.Equal(ClaimTypes.Name, userDetailsClaim.Type);
            Assert.Equal("mikarmar@microsoft.com", userDetailsClaim.Value);
            var rolesClaim1 = staticWebAppsIdentity.Claims.ElementAt(2);
            Assert.Equal(ClaimTypes.Role, rolesClaim1.Type);
            Assert.Equal("admin", rolesClaim1.Value);
            var rolesClaim2 = staticWebAppsIdentity.Claims.ElementAt(3);
            Assert.Equal(ClaimTypes.Role, rolesClaim2.Type);
            Assert.Equal("super_admin", rolesClaim2.Value);
        }

        [Fact]
        public void GetAppServiceIdentity_XMsClientPrincipalInvalidJson_ReturnsNull()
        {
            HttpRequest req = new DefaultHttpContext().Request;
            var claims = new List<Claim>();
            claims.Add(new Claim("name", "Connor McMahon"));
            claims.Add(new Claim("role", "Software Engineer"));
            var identity = new ClaimsIdentity(authenticationType: "aad", nameType: "name", roleType: "role", claims: claims);

            //Load onto header
            string json = JsonConvert.SerializeObject(ClaimsIdentitySlim.FromClaimsIdentity(identity));
            json = json.Replace("[", "{");
            json = json.Replace("]", "}");
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            Assert.Null(req.GetAppServiceIdentity());
        }

        [Fact]
        public void GetAppServiceIdentity_DefaultClaimsIdentity_ReturnsNull()
        {
            HttpRequest req = new DefaultHttpContext().Request;
            var identitySlim = default(ClaimsIdentitySlim);

            //Load onto header
            string json = JsonConvert.SerializeObject(identitySlim);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            Assert.Null(req.GetAppServiceIdentity());
        }

        [Fact]
        public void GetAppServiceIdentity_DefaultStaticWebAppsClientPrincipal_ReturnsNull()
        {
            HttpRequest req = new DefaultHttpContext().Request;
            var staticWebAppsClientPrincipal = default(StaticWebAppsClientPrincipal);

            //Load onto header
            string json = JsonConvert.SerializeObject(staticWebAppsClientPrincipal);
            byte[] bytes = Encoding.UTF8.GetBytes(json);
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            Assert.Null(req.GetAppServiceIdentity());
        }

        [Fact]
        public void GetAppServiceIdentity_EmptyStringClientPrincipal_ReturnsNull()
        {
            HttpRequest req = new DefaultHttpContext().Request;

            //Load onto header
            byte[] bytes = Encoding.UTF8.GetBytes(string.Empty);
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            Assert.Null(req.GetAppServiceIdentity());
        }

        [Fact]
        public void GetAppServiceIdentity_EmptyObjectClientPrincipal_ReturnsNull()
        {
            HttpRequest req = new DefaultHttpContext().Request;

            //Load onto header
            byte[] bytes = Encoding.UTF8.GetBytes("{}");
            string encodedHeaderValue = Convert.ToBase64String(bytes);
            req.Headers["x-ms-client-principal"] = encodedHeaderValue;

            Assert.Null(req.GetAppServiceIdentity());
        }

        [Fact]
        public void GetAppServiceIdentity_NoXMsClientPrincipalHeader_ReturnsNull()
        {
            HttpRequest req = new DefaultHttpContext().Request;
            Assert.Null(req.GetAppServiceIdentity());
        }
    }
}
