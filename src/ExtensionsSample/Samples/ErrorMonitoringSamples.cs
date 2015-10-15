// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System.IO;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions;
using Microsoft.Azure.WebJobs.Host;

namespace ExtensionsSample.Samples
{
    /// <summary>
    /// Example error trigger functions demonstrating how <see cref="ErrorTriggerAttribute"/>
    /// can be used to declare a job function that will automatically be called when other job
    /// functions error.
    /// </summary>
    public static class ErrorMonitoringSamples
    {
        private static ErrorNotifier _notifier = new ErrorNotifier(new SendGridConfiguration());

        /// <summary>
        /// Global error monitor function that will be triggered whenever errors
        /// pass the specified sliding window threshold.
        /// </summary>
        /// <param name="filter">The <see cref="TraceFilter"/> that caused the error
        /// trigger to fire.</param>
        public static void ErrorMonitor(
            [ErrorTrigger("00:30:00", 10)] TraceFilter filter, TextWriter log)
        {
            // send a SMS notification
            _notifier.WebNotify(filter);

            // log last 5 detailed errors to the Dashboard
            log.WriteLine(filter.GetDetailedMessage(5));
        }
    }
}
