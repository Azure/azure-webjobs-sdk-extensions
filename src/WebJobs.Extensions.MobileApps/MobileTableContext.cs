// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    internal class MobileTableContext
    {
        public MobileAppsConfiguration Config { get; set; }

        public IMobileServiceClient Client { get; set; }

        public string ResolvedTableName { get; set; }

        public string ResolvedId { get; set; }
    }
}