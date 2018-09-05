// Copyright (c) .NET Foundation. All rights reserved.
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
        public void GetAppServiceIdentity_ReturnsIdentity()
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
    }
}
