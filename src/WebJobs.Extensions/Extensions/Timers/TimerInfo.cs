// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Azure.WebJobs.Extensions.Timers;

namespace Microsoft.Azure.WebJobs
{
    /// <summary>
    /// Provides access to timer schedule information for jobs triggered 
    /// by <see cref="TimerTriggerAttribute"/>.
    /// </summary>
    public class TimerInfo
    {
        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        /// <param name="schedule">The timer trigger schedule.</param>
        /// <param name="status">The current schedule status.</param>
        /// <param name="isPastDue">True if the schedule is past due, false otherwise.</param>
        public TimerInfo(TimerSchedule schedule, ScheduleStatus status, bool isPastDue = false)
        {
            Schedule = schedule;
            ScheduleStatus = status;
            IsPastDue = isPastDue;
        }

        /// <summary>
        /// Gets the schedule for the timer trigger.
        /// </summary>
        public TimerSchedule Schedule { get; private set; }

        /// <summary>
        /// Gets the current schedule status for this timer.
        /// If schedule monitoring is not enabled for this timer (see <see cref="TimerTriggerAttribute.UseMonitor"/>)
        /// this property will return null.
        /// </summary>
        public ScheduleStatus ScheduleStatus { get; private set; }

        /// <summary>
        /// Gets a value indicating whether this timer invocation
        /// is due to a missed schedule occurrence.
        /// </summary>
        public bool IsPastDue { get; private set; }

        /// <summary>
        /// Formats the next 'count' occurrences of the schedule into an
        /// easily loggable string.
        /// </summary>
        /// <param name="count">The number of occurrences to format.</param>
        /// <param name="now">The optional <see cref="DateTime"/> to start from.</param>
        /// <returns>A formatted string with the next occurrences.</returns>
        public string FormatNextOccurrences(int count, DateTime? now = null)
        {
            return FormatNextOccurrences(Schedule, count, now);
        }

        internal static string FormatNextOccurrences(TimerSchedule schedule, int count, DateTime? now = null, string timerName = null)
        {
            if (schedule == null)
            {
                throw new ArgumentNullException(nameof(schedule));
            }

            // If we've got a timer name, format it
            if (timerName != null)
            {
                timerName = $"'{timerName}' ";
            }

            IEnumerable<DateTime> nextOccurrences = schedule.GetNextOccurrences(count, now);
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"The next {count} occurrences of the {timerName}schedule will be:");
            foreach (DateTime occurrence in nextOccurrences)
            {
                builder.AppendLine(occurrence.ToString());
            }

            return builder.ToString();
        }
    }
}
