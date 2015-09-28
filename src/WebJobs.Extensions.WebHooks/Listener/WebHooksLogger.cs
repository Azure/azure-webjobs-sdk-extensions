// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Web.Http.Tracing;
using Microsoft.AspNet.WebHooks.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.WebHooks
{
    /// <summary>
    /// Custom <see cref="ILogger"/> implementation used for ASP.NET WebHooks SDK integration.
    /// </summary>
    internal class WebHooksLogger : ILogger
    {
        private readonly TraceWriter _traceWriter;

        public WebHooksLogger(TraceWriter traceWriter)
        {
            _traceWriter = traceWriter;
        }

        public void Log(TraceLevel level, string message, Exception ex)
        {
            // route all logs coming from the WebHooks SDK to the WebHobs SDK
            // as Verbose.
            _traceWriter.Trace(System.Diagnostics.TraceLevel.Verbose, string.Empty, message, ex);
        }
    }
}
