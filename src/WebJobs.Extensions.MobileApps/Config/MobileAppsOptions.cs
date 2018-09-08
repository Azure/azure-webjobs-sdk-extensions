// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    public class MobileAppsOptions
    {
        /// <summary>
        /// Gets or sets the ApiKey to use with the Mobile App.
        /// </summary>
        public string ApiKey { get; set; }

        /// <summary>
        /// Gets or sets the mobile app URI.
        /// </summary>      
        public Uri MobileAppUri { get; set; }
    }
}
