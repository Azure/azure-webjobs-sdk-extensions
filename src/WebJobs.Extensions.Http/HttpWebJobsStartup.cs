// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Hosting;
using Microsoft.Extensions.Hosting;

[assembly:WebJobsStartup(typeof(HttpWebJobsStartup))]

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    /// <summary>
    /// Enable dynamic HTTP registration against WebJobs 
    /// </summary>
    public class HttpWebJobsStartup : IWebJobsStartup
    {
        public void Configure(IHostBuilder builder)
        {
            builder.AddHttp();
        }
    }
}
