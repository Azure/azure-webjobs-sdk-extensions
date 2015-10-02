// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Azure.WebJobs.Extensions.Timers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Trigger attribute used to declare that a job function should be invoked periodically
    /// based on a timer schedule. The parameter type must be <see cref="TimerInfo"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class TimerTriggerAttribute : Attribute
    {
        /// <summary>
        /// Constructs a new instance based on the schedule expression passed in./>
        /// </summary>
        /// <param name="expression">A schedule expression. This can either be a crontab expression 
        /// <a href="http://en.wikipedia.org/wiki/Cron#CRON_expression"/> or a <see cref="TimeSpan"/> string.</param>
        public TimerTriggerAttribute(string expression)
        {
            CronSchedule cronSchedule = null;
            if (CronSchedule.TryCreate(expression, out cronSchedule))
            {
                Schedule = cronSchedule;

                DateTime[] nextOccurrences = cronSchedule.InnerSchedule.GetNextOccurrences(DateTime.Now, DateTime.Now + TimeSpan.FromMinutes(1)).ToArray();
                if (nextOccurrences.Length > 1)
                {
                    // if there is more than one occurrence due in the next minute,
                    // assume that this is a sub-minute constant schedule and disable
                    // persistence
                    UseMonitor = false;
                }
                else
                {
                    UseMonitor = true;
                }
            }
            else
            {
                TimeSpan periodTimespan = TimeSpan.Parse(expression);
                Schedule = new ConstantSchedule(periodTimespan);

                // for very frequent constant schedules, we want to disable persistence
                UseMonitor = periodTimespan.TotalMinutes >= 1;
            }
        }

        /// <summary>
        /// Constructs a new instance using the specified <see cref="TimerSchedule"/> type.
        /// </summary>
        /// <param name="scheduleType">The type of schedule to create.</param>
        public TimerTriggerAttribute(Type scheduleType)
        {
            Schedule = (TimerSchedule)Activator.CreateInstance(scheduleType);
            UseMonitor = true;
        }

        /// <summary>
        /// Gets the Schedule.
        /// </summary>
        public TimerSchedule Schedule { get; private set; }

        /// <summary>
        /// Gets or sets a value indicating whether the schedule should be monitored.
        /// Schedule monitoring persists schedule occurrences to aid in ensuring the
        /// schedule is maintained correctly even when roles restart.
        /// This will default to true for schedules that have a recurrence interval greater
        /// than 1 minute (i.e., for constant schedules that occur more than once per minute,
        /// persistence is disabled by default).
        /// </summary>
        public bool UseMonitor { get; set; }
    }
}
