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
        private readonly int _threshold;
        private readonly object _syncLock = new object();
        private readonly string _notificationMessage;
        private Collection<TraceEvent> _traces = new Collection<TraceEvent>();
        private Exception _lastException;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="window">The duration of the sliding window.</param>
        /// <param name="threshold">The maximum number of matching trace messages to allow before triggering a notification.</param>
        /// <param name="level">The maximum trace level of trace messages that will match the filter.</param>
        public SlidingWindowTraceFilter(TimeSpan window, int threshold, TraceLevel level = TraceLevel.Error)
        {
            _window = window;
            _level = level;
            _threshold = threshold;
            _notificationMessage = string.Format("{0} events at level '{1}' or lower have occurred within time window {2}.", _threshold, level.ToString(), _window);
        }

        /// <summary>
        /// Gets the notification message that should be used in notifications.
        /// </summary>
        public override string Message
        {
            get
            {
                return _notificationMessage;
            }
        }

        /// <inheritdoc/>
        public override Collection<TraceEvent> Traces
        {
            get
            {
                return _traces;
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
                    ResizeWindow();

                    _traces.Add(traceEvent);

                    if (_traces.Count > _threshold)
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
            if (passesFilter && traceEvent.Exception != null)
            {
                // often we may see multiple sequential error messages for the same
                // exception, so we want to skip the duplicates
                if (_lastException != null &&
                    Object.ReferenceEquals(_lastException, traceEvent.Exception))
                {
                    return false;
                }
                _lastException = traceEvent.Exception;
            }

            return passesFilter;
        }

        private void ResizeWindow()
        {
            // remove any events outside of the window
            int count = 0;
            DateTime cutoff = DateTime.UtcNow - _window;
            foreach (TraceEvent currTraceEvent in _traces)
            {
                if (currTraceEvent.Timestamp > cutoff)
                {
                    break;
                }
                count++;
            }

            if (count > 0)
            {
                for (int i = 0; i < count; i++)
                {
                    _traces.RemoveAt(i);
                }
            }
        }
    }
}
