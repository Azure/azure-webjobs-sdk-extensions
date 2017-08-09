// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Http.Routing
{
    public class DefaultWebJobsRouteHandler : IWebJobsRouteHandler
    {
        public async Task InvokeAsync(HttpContext context, string functionName)
        {
            await context.Response.WriteAsync($"Default WebJobs route handler executed for function named '{functionName}'");
        }
    }
}
