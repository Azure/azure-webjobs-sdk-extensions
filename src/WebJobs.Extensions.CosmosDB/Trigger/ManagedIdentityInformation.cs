// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.WebJobs.Extensions.CosmosDB.Trigger
{
    public enum ManagedIdentityType
    {
        None = 0,
        SystemAssigned = 1,
        UserAssigned = 2
    }

    public class ManagedIdentityInformation
    {
        public ManagedIdentityInformation(ManagedIdentityType type,
                                string tenantId,
                                string clientId,
                                string principalId,
                                string identityUrl,
                                string authenticationEndpoint,
                                byte[] certBytes,
                                string resourceId = null)
        {
            this.Type = type;
            this.TenantId = tenantId;
            this.ClientId = clientId;
            this.PrincipalId = principalId;
            this.IdentityUrl = identityUrl;
            this.AuthenticationEndpoint = authenticationEndpoint;
            this.CertificateBytes = certBytes;
            this.ResourceId = resourceId;
        }

        public ManagedIdentityType Type { get; }

        public string TenantId { get; }

        public string ClientId { get; }

        public string PrincipalId { get; }

        public string IdentityUrl { get; }

        public string AuthenticationEndpoint { get; }

        public byte[] CertificateBytes { get; }

        public string ResourceId { get; }
    }
}
