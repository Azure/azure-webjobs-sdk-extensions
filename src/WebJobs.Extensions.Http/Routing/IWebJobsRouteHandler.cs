// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace Microsoft.Azure.WebJobs.Extensions.Http
{
    public interface IWebJobsRouteHandler
    {
        Task InvokeAsync(HttpContext context, string functionName);
    }
}