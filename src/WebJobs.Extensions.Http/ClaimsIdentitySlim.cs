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
    internal struct ClaimsIdentitySlim : IIdentityPrincipal
    {
        [DataMember(Name = "auth_typ")]
        internal string AuthenticationType;

        [DataMember(Name = "name_typ")]
        internal string NameClaimType;

        [DataMember(Name = "role_typ")]
        internal string RoleClaimType;

        [DataMember(Name = "claims")]
        internal List<ClaimSlim> Claims;

        public ClaimsIdentity ToClaimsIdentity()
        {
            ClaimsIdentity identity = new ClaimsIdentity(this.AuthenticationType, this.NameClaimType, this.RoleClaimType);
            foreach (ClaimSlim claimSlim in this.Claims)
            {
                identity.AddClaim(claimSlim.ToClaim());
            }

            return identity;
        }

        public static ClaimsIdentitySlim FromClaimsIdentity(ClaimsIdentity identity)
        {
            var result = new ClaimsIdentitySlim
            {
                AuthenticationType = identity.AuthenticationType,
                NameClaimType = identity.NameClaimType,
                RoleClaimType = identity.RoleClaimType,
                Claims = new List<ClaimSlim>()
            };

            foreach (Claim claim in identity.Claims)
            {
                result.Claims.Add(new ClaimSlim(claim));
            }

            return result;
        }

        internal JObject ToJObject()
        {
            var jObj = new JObject();
            jObj["authenticationType"] = AuthenticationType;
            jObj["nameClaimType"] = NameClaimType;
            jObj["roleClaimType"] = RoleClaimType;
            jObj["claims"] = new JArray(Claims.Select(claim => claim.ToJObject()).ToArray());
            return jObj;
        }
    }
}
