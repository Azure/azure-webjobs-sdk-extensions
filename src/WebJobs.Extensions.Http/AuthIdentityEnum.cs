// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Security.Claims;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public enum AuthIdentityEnum
    {
        AppServiceIdentity = 0,
        StaticWebAppsIdentity = 1,
    }
}