// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Net.Http;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal interface IMobileServiceClientFactory
    {
        IMobileServiceClient CreateClient(Uri mobileAppUri, HttpMessageHandler[] handlers);
    }
}
