// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    using System.Collections.Generic;
    using System.Runtime.Serialization;
    using System.Security.Claims;

    /// <summary>
    /// Light-weight representation of a <see cref="ClaimsIdentity"/> object.
    /// </summary>
    [DataContract]
    [KnownType(typeof(ClaimSlim))]
    internal struct ClaimsIdentitySlim
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
            var result = new ClaimsIdentitySlim();
            result.AuthenticationType = identity.AuthenticationType;
            result.NameClaimType = identity.NameClaimType;
            result.RoleClaimType = identity.RoleClaimType;
            result.Claims = new List<ClaimSlim>();

            foreach (Claim claim in identity.Claims)
            {
                result.Claims.Add(new ClaimSlim(claim));
            }

            return result;
        }
    }
}
