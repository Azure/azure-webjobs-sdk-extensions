// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using Microsoft.Azure.WebJobs.Host;

namespace Microsoft.Azure.WebJobs.Extensions
{
    /// <summary>
    /// Trigger for invoking jobs based on error events.
    /// </summary>
    /// <remarks>
    /// The method parameter type can be one of the following:
    /// <list type="bullet">
    /// <item><description><see cref="TraceFilter"/></description></item>
    /// <item><description><see cref="IEnumerable{TraceEvent}"/></description></item>
    /// <item><description><see cref="TraceEvent"/></description></item>
    /// </list>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public sealed class ErrorTriggerAttribute : Attribute
    {
        private string _throttle;

        /// <summary>
        /// Indicates that the error handler should be called on every error.
        /// </summary>
        public ErrorTriggerAttribute()
        {
        }

        /// <summary>
        /// Indicates that the error handler should be called when the specified
        /// sliding window threshold is reached. See <see cref="SlidingWindowTraceFilter"/>
        /// for details.
        /// </summary>
        /// <param name="window"></param>
        /// <param name="threshold"></param>
        public ErrorTriggerAttribute(string window, int threshold)
        {
            TimeSpan timeSpan;
            if (!TimeSpan.TryParse(window, out timeSpan))
            {
                throw new ArgumentException("Invalid TimeSpan value specified.", "window");
            }
            if (threshold < 0)
            {
                throw new ArgumentOutOfRangeException("threshold");
            }
            Window = window;
            Threshold = threshold;
        }

        /// <summary>
        /// Indicates that the error handler should be called based on the specified
        /// custom <see cref="TraceFilter"/>.
        /// </summary>
        /// <param name="filterType"></param>
        public ErrorTriggerAttribute(Type filterType)
        {
            if (filterType == null)
            {
                throw new ArgumentNullException("filterType");
            }
            FilterType = filterType;
        }

        /// <summary>
        /// Gets the custom <see cref="TraceFilter"/> type that will be used.
        /// </summary>
        public Type FilterType { get; private set; }

        /// <summary>
        /// Gets the sliding window duration. Should be expressed as a <see cref="TimeSpan"/>
        /// value (e.g. "00:30:00").
        /// </summary>
        public string Window { get; private set; }

        /// <summary>
        /// Gets the error count threshold that must be reached before the function
        /// is invoked.
        /// </summary>
        public int Threshold { get; private set; }

        /// <summary>
        /// Gets the notification throttle window. The function will be triggered at most once
       /// within this window. Should be expressed as a <see cref="TimeSpan"/> value (e.g. "00:30:00").
        /// </summary>
        public string Throttle
        {
            get
            {
                return _throttle;
            }
            set
            {
                TimeSpan timeSpan;
                if (!TimeSpan.TryParse(value, out timeSpan))
                {
                    throw new ArgumentException("Invalid TimeSpan value specified.", "value");
                }
                _throttle = value;
            }
        }

        /// <summary>
        /// Gets the error message that should be used for notifications.
        /// </summary>
        public string Message { get; set; }
    }
}
