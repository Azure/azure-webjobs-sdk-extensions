// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Security.Claims;
using Newtonsoft.Json.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Light-weight representation of a <see cref="ClaimsIdentity"/> object. This is the same 
    /// serialization as found in EasyAuth, and needs to be kept in sync with its version of this file.
    /// </summary>
    [DataContract]
    [KnownType(typeof(ClaimSlim))]
    internal struct StaticWebAppsClientPrincipal : IIdentityPrincipal
    {
        [DataMember(Name = "identityProvider")]
        internal string IdentityProvider;

        [DataMember(Name = "userId")]
        internal string UserId;

        [DataMember(Name = "userDetails")]
        internal string UserDetails;

        [DataMember(Name = "userRoles")]
        internal List<string> UserRoles;

        public ClaimsIdentity ToClaimsIdentity()
        {
            var staticWebAppsIdentity = new ClaimsIdentity(this.IdentityProvider);
            staticWebAppsIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, this.UserId));
            staticWebAppsIdentity.AddClaim(new Claim(ClaimTypes.Name, this.UserDetails));
            staticWebAppsIdentity.AddClaims(this.UserRoles.Select(r => new Claim(ClaimTypes.Role, r)));
            return staticWebAppsIdentity;
        }
    }
}
