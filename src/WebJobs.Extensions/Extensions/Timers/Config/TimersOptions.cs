// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// Options object for <see cref="TimerTriggerAttribute"/> decorated job functions.
    /// </summary>
    public class TimersOptions
    {
        /// <summary>
        /// Gets or sets the schedule monitor used to persist
        /// schedule occurrences and monitor execution.
        /// </summary>
        public ScheduleMonitor ScheduleMonitor { get; set; }
    }
}
