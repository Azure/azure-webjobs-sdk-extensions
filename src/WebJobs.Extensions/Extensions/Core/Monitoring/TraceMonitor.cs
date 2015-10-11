// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using Microsoft.Azure.WebJobs.Extensions.Core.Listener;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// <see cref="TraceWriter"/> that can be used to monitor the trace stream
    /// and notify subscribers when certain filter thresholds are reached, based
    /// on the collection of <see cref="TraceFilter"/>s and subscriptions configured.
    /// </summary>
    public class TraceMonitor : TraceWriter
    {
        private DateTime _lastNotification;
        private Exception _lastException;

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="level">The <see cref="TraceLevel"/> for this <see cref="TraceWriter"/>.
        /// Defaults to <see cref="TraceLevel.Error"/>.</param>
        public TraceMonitor(TraceLevel level = TraceLevel.Error) : base(level)
        {
            Filters = new Collection<TraceFilter>();
            Subscriptions = new Collection<Action<TraceFilter>>();
        }

        /// <summary>
        /// Gets the collection of <see cref="TraceFilter"/>s that will be used to
        /// monitor the trace stream.
        /// </summary>
        internal Collection<TraceFilter> Filters { get; private set; }

        /// <summary>
        /// Gets the collection of subscribers that will be notified
        /// when <see cref="TraceFilter"/>s trigger notifications.
        /// </summary>
        internal Collection<Action<TraceFilter>> Subscriptions { get; private set; }

        internal TimeSpan? NotificationThrottle { get; private set; }

        /// <inheritdoc/>
        public override void Trace(TraceEvent traceEvent)
        {
            if (traceEvent == null)
            {
                throw new ArgumentNullException("traceEvent");
            }

            if (ShouldIgnore(traceEvent))
            {
                return;
            }

            try
            {
                foreach (TraceFilter filter in Filters)
                {
                    // trace must be passed to all filters (even if no notifications will be
                    // performed due to throttling) to allow them to continue to accumulate their results
                    if (filter.Filter(traceEvent))
                    {
                        // Notify all subscribers
                        Notify(filter);
                    }
                }
            }
            catch
            {
                // Need to prevent infinite loops when calling out to user code
                // I.e., user code exception in error handler causes error filter/subscriber
                // to be called again.
            }
        }

        /// <summary>
        /// Notify all subscribers that the threshold for the
        /// specified filter has been reached.
        /// </summary>
        protected virtual void Notify(TraceFilter filter)
        {
            // Throttle notifications if requested
            bool shouldNotify = NotificationThrottle == null ||
                (DateTime.UtcNow - _lastNotification) > NotificationThrottle;

            if (shouldNotify)
            {
                foreach (var subscription in Subscriptions)
                {
                    subscription(filter);
                }
                _lastNotification = DateTime.UtcNow;
            }
        }

        private bool ShouldIgnore(TraceEvent traceEvent)
        {
            if (traceEvent.Exception != null)
            {
                FunctionInvocationException functionException = traceEvent.Exception as FunctionInvocationException;
                if (functionException != null && ErrorTriggerListener.ErrorHandlers.Contains(functionException.MethodName))
                {
                    // We ignore any errors coming from error handlers themselves to prevent
                    // infinite recursion
                    return true;
                }

                // often we may see multiple sequential error messages for the same
                // exception, so we want to skip the duplicates
                bool isDuplicate = Object.ReferenceEquals(_lastException, traceEvent.Exception);
                if (isDuplicate)
                {
                    return true;
                }

                _lastException = traceEvent.Exception;
            }

            return false;
        }

        /// <summary>
        /// Add the specified <see cref="TraceFilter"/>(s) to this <see cref="TraceMonitor"/>.
        /// </summary>
        /// <param name="filters">The <see cref="TraceFilter"/>(s) to add.</param>
        /// <returns>This <see cref="TraceMonitor"/> instance.</returns>
        public TraceMonitor Filter(params TraceFilter[] filters)
        {
            if (filters == null)
            {
                throw new ArgumentNullException("filters");
            }

            foreach (TraceFilter filter in filters)
            {
                Filters.Add(filter);
            }
            
            return this;
        }

        /// <summary>
        /// Add the specified filter function to this <see cref="TraceMonitor"/>.
        /// </summary>
        /// <param name="predicate">The filter predicate.</param>
        /// <param name="message">The optional subscription notification message to use.</param>
        /// <returns>This <see cref="TraceMonitor"/> instance.</returns>
        public TraceMonitor Filter(Func<TraceEvent, bool> predicate, string message = null)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException("predicate");
            }

            Filters.Add(TraceFilter.Create(predicate, message));

            return this;
        }

        /// <summary>
        /// Add the specified subscription(s) to this <see cref="TraceMonitor"/>.
        /// </summary>
        /// <param name="subscriptions">The subscription(s) to add.</param>
        /// <returns>This <see cref="TraceMonitor"/> instance.</returns>
        public TraceMonitor Subscribe(params Action<TraceFilter>[] subscriptions)
        {
            if (subscriptions == null)
            {
                throw new ArgumentNullException("subscriptions");
            }

            foreach (Action<TraceFilter> subscription in subscriptions)
            {
                Subscriptions.Add(subscription);
            }

            return this;
        }

        /// <summary>
        /// Sets the throttle limit for subscriber notifications. When set, registered
        /// subscribers be notified at most once per throttle window.
        /// </summary>
        /// <param name="throttle">The time window defining the throttle limit.</param>
        /// <returns>This <see cref="TraceMonitor"/> instance.</returns>
        public TraceMonitor Throttle(TimeSpan throttle)
        {
            NotificationThrottle = throttle;

            return this;
        }
    }
}
