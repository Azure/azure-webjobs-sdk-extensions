// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using Microsoft.Azure.WebJobs.Extensions.Timers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Attribute used to mark a job function that should be invoked periodically based on
    /// a timer schedule. The trigger parameter type must be <see cref="TimerInfo"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class TimerTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance based on the schedule expression passed in.
        /// </summary>
        /// <param name="scheduleExpression">A schedule expression. This can either be a 6 field crontab expression
        /// <a href="http://en.wikipedia.org/wiki/Cron#CRON_expression"/> or a <see cref="TimeSpan"/>
        /// string (e.g. "00:30:00").</param>
        public TimerTriggerAttribute(string scheduleExpression)
        {
            ScheduleExpression = scheduleExpression;
            UseMonitor = true;
        }

        /// <summary>
        /// Constructs a new instance using the specified <see cref="TimerSchedule"/> type.
        /// </summary>
        /// <param name="scheduleType">The type of schedule to use.</param>
        public TimerTriggerAttribute(Type scheduleType)
        {
            ScheduleType = scheduleType;
            UseMonitor = true;
        }

        /// <summary>
        /// Gets the schedule expression.
        /// </summary>
        public string ScheduleExpression { get; private set; }

        /// <summary>
        /// Gets the <see cref="TimerSchedule"/> type.
        /// </summary>
        public Type ScheduleType { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the schedule should be monitored.
        /// Schedule monitoring persists schedule occurrences to aid in ensuring the
        /// schedule is maintained correctly even when roles restart.
        /// If not set explicitly, this will default to true for schedules that have a recurrence
        /// interval greater than 1 minute (i.e., for schedules that occur more than once
        /// per minute, persistence will be disabled).
        /// </summary>
        public bool UseMonitor { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the function should be invoked
        /// immediately on startup. After the initial startup run, the function will
        /// be run on schedule thereafter.
        /// </summary>
        public bool RunOnStartup { get; set; }
    }
}
