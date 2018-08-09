// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Extensions.MobileApps;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;

[assembly: WebJobsStartup(typeof(MobileAppsWebJobsStartup), "Mobile Apps")]

namespace Microsoft.Azure.WebJobs.Extensions.MobileApps
{
    public class MobileAppsWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IWebJobsBuilder builder)
        {
            builder.AddMobileApps();
        }
    }
}
