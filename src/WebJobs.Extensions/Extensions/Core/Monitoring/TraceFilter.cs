// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// Defines a filter used by <see cref="TraceMonitor"/> to filter trace messages.
    /// </summary>
    public abstract class TraceFilter
    {
        /// <summary>
        /// Gets the notification message for this filter that should be used
        /// when performing subscription notifications.
        /// </summary>
        public abstract string Message { get; }

        /// <summary>
        /// Gets the current accumulated collection of <see cref="TraceEvent"/>s that have
        /// passed the filter.
        /// </summary>
        public abstract Collection<TraceEvent> Traces { get; }

        /// <summary>
        /// Inspects the specified <see cref="TraceEvent"/> and accumulates it
        /// as needed if it matches the filter.
        /// </summary>
        /// <param name="traceEvent">The <see cref="TraceEvent"/> to filter.</param>
        /// <returns>True if the trace message should result in all subscribers being
        /// notified, false otherwise.</returns>
        public abstract bool Filter(TraceEvent traceEvent);

        /// <summary>
        /// Creates an anonymous <see cref="TraceFilter"/>.
        /// </summary>
        /// <param name="predicate">The function to use to filter events.</param>
        /// <param name="message">The optional subscription notification message to use.</param>
        /// <returns></returns>
        public static TraceFilter Create(Func<TraceEvent, bool> predicate = null, string message = null)
        {
            return new AnonymousTraceFilter(predicate, message);
        }

        /// <summary>
        /// Returns a formatted string containing the notification <see cref="Message"/> as well
        /// as full details on the last <paramref name="count"/> events.
        /// </summary>
        /// <param name="count">The number of detailed events to include (starting from
        /// the most recent).</param>
        /// <returns>The formatted string.</returns>
        public virtual string GetDetailedMessage(int count)
        {
            if (count <= 0)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            StringBuilder builder = new StringBuilder();
            builder.AppendLine(Message);

            if (Traces.Count > 0)
            {
                foreach (TraceEvent traceEvent in Traces.Reverse().Take(count))
                {
                    builder.AppendLine();
                    builder.AppendLine(traceEvent.ToString()); 
                }
            }
            
            return builder.ToString();
        }

        internal class AnonymousTraceFilter : TraceFilter
        {
            private readonly string _message;
            private readonly Func<TraceEvent, bool> _predicate;
            private Collection<TraceEvent> _traces = new Collection<TraceEvent>();
            
            public AnonymousTraceFilter(Func<TraceEvent, bool> predicate, string message = null)
            {
                _predicate = predicate;
                _message = message ?? "WebJob failure detected.";
            }

            public override string Message
            {
                get
                {
                    return _message;
                }
            }

            public override Collection<TraceEvent> Traces
            {
                get
                {
                    return _traces;
                }
            }

            public override bool Filter(TraceEvent traceEvent)
            {
                if (_predicate == null || _predicate(traceEvent))
                {
                    // this filter does not accumulate - it only keeps track
                    // of the last event, so we reset the collection each time.
                    _traces.Clear();
                    _traces.Add(traceEvent);

                    return true;
                }

                return false;
            }
        }
    }
}
