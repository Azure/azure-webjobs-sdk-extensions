// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// <see cref="TraceFilter"/> that maintains a sliding time window, and only triggers
    /// notifications if the number of matching trace messages within that time window
    /// exceeds a threshold.
    /// </summary>
    public class SlidingWindowTraceFilter : TraceFilter
    {
        private readonly TimeSpan _window;
        private readonly TraceLevel _level;
        private readonly Func<TraceEvent, bool> _filter;
        private readonly object _syncLock = new object();
        private readonly string _notificationMessage;
        private Collection<TraceEvent> _events = new Collection<TraceEvent>();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="window">The duration of the sliding window.</param>
        /// <param name="threshold">The maximum number of matching trace messages to allow before triggering a notification.</param>
        /// <param name="filter">The optional event filter to apply on each <see cref="TraceEvent"/>.</param>
        /// <param name="message">The optional message to use for notifications.</param>
        /// <param name="level">The maximum trace level of trace messages that will match the filter.</param>
        public SlidingWindowTraceFilter(TimeSpan window, int threshold, Func<TraceEvent, bool> filter = null, string message = null, TraceLevel level = TraceLevel.Error)
        {
            _window = window;
            _level = level;
            Threshold = threshold;
            _filter = filter;
            _notificationMessage = message ?? string.Format("{0} events at level '{1}' or lower have occurred within time window {2}.", threshold, level.ToString(), _window);
        }

        /// <summary>
        /// Gets the message that should be used in notifications.
        /// </summary>
        public override string Message
        {
            get
            {
                return _notificationMessage;
            }
        }

        /// <summary>
        /// Gets maximum number of matching trace messages to allow before triggering a notification.
        /// </summary>
        public int Threshold { get; private set; }

        /// <inheritdoc/>
        public override Collection<TraceEvent> Events
        {
            get
            {
                return _events;
            }
        }

        /// <inheritdoc/>
        public override bool Filter(TraceEvent traceEvent)
        {
            if (FilterCore(traceEvent))
            {
                // Ok to lock here since this filter will generally only be configured to
                // fire on errors/warnings, and those events will be rare in normal processing.
                lock (_syncLock)
                {
                    RemoveOldEvents(DateTime.Now);

                    _events.Add(traceEvent);

                    if (_events.Count >= Threshold)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Determines whether the trace matches the filter and should be counted.
        /// Can be overridden by subclasses to perform additional checks on the
        /// trace.
        /// </summary>
        /// <param name="traceEvent">The <see cref="TraceEvent"/> to filter.</param>
        /// <returns>True if the trace message passes the filter and should be counted, false otherwise.</returns>
        protected virtual bool FilterCore(TraceEvent traceEvent)
        {
            if (traceEvent == null)
            {
                throw new ArgumentNullException("traceEvent");
            }

            bool passesFilter = traceEvent.Level <= _level;
            if (_filter != null)
            {
                passesFilter &= _filter(traceEvent);
            }

            return passesFilter;
        }

        internal void RemoveOldEvents(DateTime now)
        {
            // remove any events outside of the window
            DateTime cutoff = now - _window;
            while (_events.Count > 0)
            {
                if (_events[0].Timestamp > cutoff)
                {
                    break;
                }
                _events.RemoveAt(0);
            }
        }
    }
}
