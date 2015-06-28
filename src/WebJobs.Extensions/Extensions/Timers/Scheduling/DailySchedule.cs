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
        public override DateTime GetNextOccurrence(DateTime now)
        {
            if (schedule.Count == 0)
            {
                throw new InvalidOperationException("The schedule is empty.");
            }

            int idx = schedule.FindIndex(p => now.TimeOfDay <= p);
            if (idx == -1)
            {
                idx = 0;
            }

            TimeSpan nextTime = schedule[idx];
            return new DateTime(now.Year, now.Month, now.Day, nextTime.Hours, nextTime.Minutes, nextTime.Seconds);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return string.Format("Daily: {0} occurrences", schedule.Count);
        }
    }
}
