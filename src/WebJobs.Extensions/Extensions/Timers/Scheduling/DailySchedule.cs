// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.Azure.WebJobs.Extensions.Timers
{
    /// <summary>
    /// A simple daily schedule. The schedule repeats each day, and each day it
    /// cycles through the configured times.
    /// </summary>
    public class DailySchedule : TimerSchedule
    {
        private readonly List<TimeSpan> schedule = new List<TimeSpan>();

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public DailySchedule()
        {
        }

        /// <summary>
        /// Constructs an instance based on the specified collection of
        /// <see cref="TimeSpan"/> strings.
        /// </summary>
        /// <param name="times">The daily schedule times.</param>
        public DailySchedule(params string[] times)
        {
            schedule = times.Select(p => TimeSpan.Parse(p)).OrderBy(p => p).ToList();
        }

        /// <summary>
        /// Constructs an instance based on the specified collection of
        /// <see cref="TimeSpan"/> instances.
        /// </summary>
        /// <param name="times">The daily schedule times.</param>
        public DailySchedule(params TimeSpan[] times)
        {
            schedule = times.OrderBy(p => p).ToList();
        }

        /// <inheritdoc/>
        public override bool AdjustForDST => true;

        /// <inheritdoc />
        public override bool IsInterval => true;

        /// <inheritdoc/>
        public override DateTime GetNextOccurrence(DateTime now)
        {
            if (schedule.Count == 0)
            {
                throw new InvalidOperationException("The schedule is empty.");
            }

            // find the next occurrence in the schedule where the time
            // is strictly greater than now
            int idx = schedule.FindIndex(p => now.TimeOfDay < p);
            if (idx == -1)
            {
                // no more occurrences for today, so start back at the beginning of the
                // the schedule tomorrow
                TimeSpan nextTime = schedule[0];
                DateTime nextOccurrence = new DateTime(now.Year, now.Month, now.Day, nextTime.Hours, nextTime.Minutes, nextTime.Seconds, now.Kind);
                return nextOccurrence.AddDays(1);
            }
            else
            {
                TimeSpan nextTime = schedule[idx];
                return new DateTime(now.Year, now.Month, now.Day, nextTime.Hours, nextTime.Minutes, nextTime.Seconds, now.Kind);
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("Daily: {0} occurrences", schedule.Count);
        }
    }
}
