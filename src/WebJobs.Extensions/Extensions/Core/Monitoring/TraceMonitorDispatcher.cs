// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions.Core
{
    /// <summary>
    /// Manages a registered collection of <see cref="TraceMonitor"/>s and
    /// dispatches to them.
    /// </summary>
    /// <remarks>
    /// This dispatcher allows us to register a single TraceWriter early on startup
    /// while the TraceWriter collection is still malleable. Once functions are indexed
    /// we can't change the set of registered TraceWriters.
    /// </remarks>
    internal class TraceMonitorDispatcher : TraceWriter
    {
        private List<TraceMonitor> _monitors = new List<TraceMonitor>();

        public TraceMonitorDispatcher() : base(TraceLevel.Verbose)
        {
        }

        /// <summary>
        /// Register the specified monitor.
        /// </summary>
        /// <remarks>
        /// Should only be called before the host is started and functions
        /// are running, to avoid race conditions.
        /// </remarks>
        /// <param name="monitor">The <see cref="TraceMonitor"/> to register.</param>
        public void Register(TraceMonitor monitor)
        {
            _monitors.Add(monitor);
        }

        public override void Trace(TraceEvent traceEvent)
        {
            foreach (var monitor in _monitors)
            {
                if (monitor.Level >= traceEvent.Level)
                {
                    monitor.Trace(traceEvent);
                }  
            }
        }

        public override void Flush()
        {
            foreach (var monitor in _monitors)
            {
                monitor.Flush();
            }
        }
    }
}
